using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Order.Contracts;
using Order.Voice;

namespace Order.Voice.Local
{
    public sealed class LocalIntentInterpreter : IAppIntentInterpreter, IDisposable
    {
        private readonly string directory;
        private readonly string prompt;
        private readonly JObject schema;
        private readonly JArray examples;
        private readonly HttpClient http;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private Process process;
        private Uri endpoint;
        private bool ready;
        private bool disposed;
        public double LastInferenceMilliseconds { get; private set; }

        public LocalIntentInterpreter(string directory, string prompt, string schema, string examples)
        {
            this.directory = Path.GetFullPath(directory);
            this.prompt = prompt;
            this.schema = JObject.Parse(schema);
            this.examples = JArray.Parse(examples);
            http = new HttpClient(new HttpClientHandler { UseProxy = false })
            { Timeout = System.Threading.Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 65536 };
        }

        public async Task<bool> WarmupAsync()
        {
            try
            {
                await gate.WaitAsync(lifetime.Token);
                try { await EnsureReady(lifetime.Token); return true; }
                finally { gate.Release(); }
            }
            catch (Exception) { return false; }
        }

        public async Task<Interpretation> InterpretAsync(string text, AppCommandState state, CancellationToken cancellationToken)
        {
            var totalWatch = Stopwatch.StartNew();
            var timing = new IntentTiming { Route = "direct" };
            if (disposed) throw new ObjectDisposedException(nameof(LocalIntentInterpreter));
            if (string.IsNullOrWhiteSpace(text) || text.Length > 512) return Complete(new Interpretation("Уточните короткую команду."), timing, totalWatch);
            if (IntentGrounding.TryDirect(text, out var direct))
            {
                timing.PreprocessMs = totalWatch.Elapsed.TotalMilliseconds;
                var grounding = Stopwatch.StartNew();
                var result = IntentDecisionValidator.Validate(direct, text, state);
                timing.GroundingMs = grounding.Elapsed.TotalMilliseconds;
                return Complete(result, timing, totalWatch);
            }
            timing.Route = "model";
            timing.PreprocessMs = totalWatch.Elapsed.TotalMilliseconds;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token))
            {
                linked.CancelAfter(TimeSpan.FromSeconds(30));
                var stage = Stopwatch.StartNew();
                await gate.WaitAsync(linked.Token);
                timing.QueueMs = stage.Elapsed.TotalMilliseconds;
                try
                {
                    stage.Restart();
                    timing.ProcessColdStart = !ready || process == null || process.HasExited;
                    await EnsureReady(linked.Token);
                    timing.StartupMs = stage.Elapsed.TotalMilliseconds;
                    stage.Restart();
                    var screen = state.Screen == AppScreen.AudioSettings ? "audio_settings" : state.Screen == AppScreen.MatchSetup ? "match_setup" : "main_menu";
                    var utterance = "Текущий экран: " + screen + ".\nФраза игрока: " + text;
                    var messages = new JArray(new JObject { ["role"] = "system", ["content"] = prompt });
                    foreach (var example in examples) messages.Add(example.DeepClone());
                    messages.Add(new JObject { ["role"] = "user", ["content"] = utterance });
                    var request = new JObject
                    {
                        ["messages"] = messages,
                        ["temperature"] = 0,
                        ["seed"] = 42,
                        ["max_tokens"] = 100,
                        ["stream"] = false,
                        ["cache_prompt"] = true,
                        ["chat_template_kwargs"] = new JObject { ["enable_thinking"] = false },
                        ["response_format"] = new JObject { ["type"] = "json_object", ["schema"] = schema }
                    };
                    using (var content = new StringContent(request.ToString(Formatting.None), Encoding.UTF8, "application/json"))
                    {
                        timing.RequestBuildMs = stage.Elapsed.TotalMilliseconds;
                        stage.Restart();
                        using (var response = await http.PostAsync(new Uri(endpoint, "v1/chat/completions"), content, linked.Token))
                        {
                        response.EnsureSuccessStatusCode();
                        var body = await response.Content.ReadAsStringAsync();
                        timing.HttpMs = stage.Elapsed.TotalMilliseconds;
                        LastInferenceMilliseconds = timing.HttpMs;
                        stage.Restart();
                        var envelope = JObject.Parse(body);
                        var server = envelope["timings"];
                        timing.ServerPromptMs = (double?)server?["prompt_ms"];
                        timing.ServerGenerationMs = (double?)server?["predicted_ms"];
                        timing.PromptTokens = (int?)envelope["usage"]?["prompt_tokens"];
                        timing.CachedTokens = (int?)server?["cache_n"];
                        timing.EvaluatedPromptTokens = (int?)server?["prompt_n"];
                        timing.GeneratedTokens = (int?)server?["predicted_n"];
                        if ((string)envelope["choices"]?[0]?["finish_reason"] != "stop")
                        {
                            timing.DecodeMs = stage.Elapsed.TotalMilliseconds;
                            return Complete(new Interpretation("Не удалось полностью разобрать команду. Повторите её."), timing, totalWatch);
                        }
                        var raw = (string)envelope["choices"]?[0]?["message"]?["content"];
                        var decision = Decode(raw);
                        timing.DecodeMs = stage.Elapsed.TotalMilliseconds;
                        stage.Restart();
                        var result = IntentDecisionValidator.Validate(IntentGrounding.Ground(decision, text), text, state);
                        timing.GroundingMs = stage.Elapsed.TotalMilliseconds;
                        return Complete(result, timing, totalWatch);
                        }
                    }
                }
                finally { gate.Release(); }
            }
        }

        private static Interpretation Complete(Interpretation result, IntentTiming timing, Stopwatch total)
        {
            timing.TotalMs = total.Elapsed.TotalMilliseconds;
            return result.WithTiming(timing);
        }

        public static IntentDecision Decode(string json)
        {
            var token = JObject.Parse(json ?? "", new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (token.Count != 5 || token["action"]?.Type != JTokenType.String || token["screen"]?.Type != JTokenType.String || token["open_audio"]?.Type != JTokenType.Boolean)
                throw new JsonException("Invalid intent response.");
            var value = token["value"];
            var faction = token["faction"];
            if (value == null || faction == null || (value.Type != JTokenType.Null && value.Type != JTokenType.Integer && value.Type != JTokenType.Float)
                || (faction.Type != JTokenType.Null && faction.Type != JTokenType.Integer)) throw new JsonException("Invalid intent parameters.");
            return new IntentDecision { Action = (string)token["action"], Screen = (string)token["screen"], OpenAudio = (bool)token["open_audio"],
                Value = value.Type == JTokenType.Null ? (double?)null : (double)value,
                Faction = faction.Type == JTokenType.Null ? (int?)null : (int)faction };
        }

        private async Task EnsureReady(CancellationToken cancellationToken)
        {
            if (ready && process != null && !process.HasExited) return;
            StopProcess();
            var executable = Path.Combine(directory, "llama-b11787", "llama-server.exe");
            var model = Path.Combine(directory, "qwen2.5-1.5b-instruct-q4_k_m.gguf");
            if (!File.Exists(executable) || !File.Exists(model)) throw new FileNotFoundException("Run tools/Prepare-LocalAI.ps1 first.");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            endpoint = new Uri($"http://127.0.0.1:{port}/");
            var key = Guid.NewGuid().ToString("N");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable, WorkingDirectory = Path.GetDirectoryName(executable),
                    Arguments = $"--model \"{model}\" --host 127.0.0.1 --port {port} --api-key {key} --threads 2 --threads-batch 2 --parallel 1 --ctx-size 2048 --reasoning off --reasoning-budget 0 --no-webui --log-disable",
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true, RedirectStandardError = true
                }
            };
            process.OutputDataReceived += (_, __) => { };
            process.ErrorDataReceived += (_, __) => { };
            process.Start();
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            using (var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                startup.CancelAfter(TimeSpan.FromSeconds(20));
                while (!process.HasExited)
                {
                    startup.Token.ThrowIfCancellationRequested();
                    try
                    {
                        using (var response = await http.GetAsync(new Uri(endpoint, "health"), startup.Token))
                        { if (response.IsSuccessStatusCode) { ready = true; return; } }
                    }
                    catch (HttpRequestException) { }
                    await Task.Delay(100, startup.Token);
                }
            }
            throw new InvalidOperationException("Local inference process exited.");
        }

        private void StopProcess()
        {
            ready = false;
            if (process == null) return;
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); process = null; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            StopProcess();
            http.Dispose();
            // An in-flight continuation may still release the gate after cancellation.
        }
    }
}
