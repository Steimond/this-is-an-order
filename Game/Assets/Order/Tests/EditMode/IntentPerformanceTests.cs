using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Order.Application;
using Order.Contracts;
using Order.Input;
using Order.Voice.Local;

namespace Order.Tests.EditMode
{
    [Category("LocalAI.Performance"), Explicit("Runs a real CPU model and writes Temp/command-profile.json.")]
    public sealed class IntentPerformanceTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            public SettingsSnapshot Load() => new SettingsSnapshot(0.5f, FactionId.Expedition);
            public void Save(SettingsSnapshot settings) { }
        }
        private sealed class Sample
        {
            public string Text;
            public string Phase;
            public int Round;
            public CommandMeasurement Measurement;
        }

        [Test] public async Task ProfileRealCommandPipeline()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../.."));
            var resources = Path.Combine(UnityEngine.Application.dataPath, "Order/Voice.Local/Resources");
            var samples = new List<Sample>();
            using (var model = new LocalIntentInterpreter(Path.Combine(projectRoot, ".local-ai"),
                File.ReadAllText(Path.Combine(resources, "OrderIntentPrompt.txt")),
                File.ReadAllText(Path.Combine(resources, "OrderIntentSchema.json")),
                File.ReadAllText(Path.Combine(resources, "OrderIntentExamples.json"))))
            {
                var app = new AppController(new MemoryStore());
                var input = new AppCommandInput(app, model);
                async Task Measure(string text, string phase, int round)
                {
                    var result = await input.SubmitTranscriptAsync(text, new CommandContext(app.Session, CommandSource.Text));
                    samples.Add(new Sample { Text = text, Phase = phase, Round = round, Measurement = input.LastMeasurement });
                    Assert.That(result.Outcome, Is.EqualTo(text.StartsWith("Не ", StringComparison.Ordinal)
                        ? CommandOutcome.NeedsClarification : CommandOutcome.Applied), result.Message);
                }
                try
                {
                    // No WarmupAsync here: capture process startup and the first prompt separately.
                    await Measure("Выставь общую громкость на 37 процентов", "cold-model", -1);
                    var cases = new[] { "Громкость 11 процентов", "Сделай потише", "Не меняй громкость",
                        "Выставь общую громкость на 37 процентов", "Для общего звука поставь уровень 28 процентов",
                        "Покажи настройки аудио", "Давай сыграем с ботом второй расы" };
                    for (var round = 0; round < 4; round++)
                        foreach (var text in cases) await Measure(text, "warm", round);
                }
                finally
                {
                    var report = new
                    {
                        RecordedUtc = DateTime.UtcNow.ToString("O"),
                        Cpu = UnityEngine.SystemInfo.processorType,
                        LogicalProcessors = UnityEngine.SystemInfo.processorCount,
                        MemoryMb = UnityEngine.SystemInfo.systemMemorySize,
                        OperatingSystem = UnityEngine.SystemInfo.operatingSystem,
                        UnityVersion = UnityEngine.Application.unityVersion,
                        Host = "Unity Editor EditMode; memory settings store; no audio recognition; no RTS simulation",
                        Model = "Qwen2.5-1.5B-Instruct-Q4_K_M", Runtime = "llama.cpp b11787", Threads = 2,
                        Samples = samples
                    };
                    var path = Path.Combine(projectRoot, "Game/Temp/command-profile.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
                    TestContext.WriteLine("Profile saved: " + path);
                }
            }
        }
    }
}
