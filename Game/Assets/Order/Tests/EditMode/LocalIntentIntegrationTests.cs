using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Order.Contracts;
using Order.Voice.Local;

namespace Order.Tests.EditMode
{
    [Category("LocalAI"), Explicit("Requires tools/Prepare-LocalAI.ps1; runs the real local CPU model.")]
    public sealed class LocalIntentIntegrationTests
    {
        private LocalIntentInterpreter interpreter;
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../.."));
        public static IEnumerable Cases()
        {
            var cases = JArray.Parse(File.ReadAllText(Path.Combine(ProjectRoot, "tools/intent-cases.json")));
            foreach (JObject item in cases) yield return new TestCaseData(item.ToString()).SetName("LocalIntent: " + (string)item["text"]);
        }
        [OneTimeSetUp] public void Setup()
        {
            var resources = Path.Combine(UnityEngine.Application.dataPath, "Order/Voice.Local/Resources");
            interpreter = new LocalIntentInterpreter(Path.Combine(ProjectRoot, ".local-ai"),
                File.ReadAllText(Path.Combine(resources, "OrderIntentPrompt.txt")),
                File.ReadAllText(Path.Combine(resources, "OrderIntentSchema.json")),
                File.ReadAllText(Path.Combine(resources, "OrderIntentExamples.json")));
        }
        [OneTimeTearDown] public void Teardown() => interpreter?.Dispose();

        [TestCaseSource(nameof(Cases))]
        public async Task CommandMeaningMatches(string json)
        {
            var item = JObject.Parse(json);
            var screen = (string)item["screen"] == "audio_settings" ? AppScreen.AudioSettings : AppScreen.MainMenu;
            var state = new AppCommandState(1, 0, screen, 0.5f, FactionId.Expedition, FactionId.Resonance);
            var watch = Stopwatch.StartNew();
            var result = await interpreter.InterpretAsync((string)item["text"], state, CancellationToken.None);
            TestContext.WriteLine("End-to-end milliseconds: " + watch.ElapsedMilliseconds);
            var action = (string)item["action"];
            if (action == "clarify" || (action == "prepare_match" && item["faction"]?.Type == JTokenType.Null))
            { Assert.That(result.HasCommand, Is.False, result.Message); return; }
            Assert.That(result.HasCommand, Is.True, result.Message);
            if (action == "set_volume" || action == "adjust_volume")
            {
                Assert.That(result.Command.Kind, Is.EqualTo(AppCommandKind.SetMasterVolume));
                var expected = (double)item["value"];
                if (action == "adjust_volume") expected = Math.Max(0, Math.Min(100, 50 + expected));
                Assert.That(result.Command.Volume, Is.EqualTo(expected / 100).Within(0.0001));
                Assert.That(result.Command.OpenAudioSettings, Is.EqualTo((bool?)item["open_audio"] ?? false));
            }
            else if (action == "navigate")
            {
                Assert.That(result.Command.Kind, Is.EqualTo(AppCommandKind.Navigate));
                Assert.That(result.Command.Screen, Is.EqualTo((string)item["target_screen"] == "audio_settings" ? AppScreen.AudioSettings : AppScreen.MainMenu));
            }
            else
            {
                Assert.That(result.Command.Kind, Is.EqualTo(action == "prepare_match" ? AppCommandKind.PrepareBotMatch : AppCommandKind.SetAssistantFaction));
                Assert.That(result.Command.Faction, Is.EqualTo((int)item["faction"] == 1 ? FactionId.Expedition : FactionId.Resonance));
            }
        }
    }
}
