using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Order.Application;
using Order.Content;
using Order.Contracts;
using Order.Input;
using Order.Voice;

namespace Order.Tests.EditMode
{
    public sealed class MenuCommandTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            public SettingsSnapshot Value = SettingsSnapshot.Default;
            public SettingsSnapshot Load() => Value;
            public void Save(SettingsSnapshot value) => Value = value;
        }
        private sealed class ForbiddenInterpreter : IAppIntentInterpreter
        {
            public int Calls;
            public Task<Interpretation> InterpretAsync(string text, AppCommandState state, CancellationToken cancellationToken)
            { Calls++; return Task.FromResult(new Interpretation("model")); }
        }
        private MemoryStore store;
        private AppController app;
        private readonly List<AppRequest> requests = new List<AppRequest>();
        [SetUp] public void SetUp() { store = new MemoryStore(); app = new AppController(store); requests.Clear(); app.Requested += requests.Add; }
        private CommandResult Manual(AppCommand command) => app.Execute(command, new CommandContext(app.Session, CommandSource.Manual));
        private Interpretation Parse(string text)
        {
            Assert.That(MenuCommandGrammar.TryParse(text, app.State, out var result), Is.True, "Not handled: " + text);
            return result;
        }
        private CommandResult Say(string text)
        {
            var parsed = Parse(text);
            Assert.That(parsed.HasCommand, Is.True, text + " → " + parsed.Message);
            return Manual(parsed.Command);
        }
        private float Option(string id) => MenuCatalog.Value(app.Settings, MenuCatalog.Find(id));

        [Test] public void SettingsTabsRememberReturnScreenAndLastTab()
        {
            Manual(AppCommand.Navigate(AppScreen.MatchSetup));
            Manual(AppCommand.Navigate(AppScreen.GraphicsSettings));
            Manual(AppCommand.Navigate(AppScreen.ControlsSettings));
            Assert.That(Manual(AppCommand.Back()).Applied, Is.True);
            Assert.That(app.Screen, Is.EqualTo(AppScreen.MatchSetup));
            Manual(AppCommand.OpenSettings());
            Assert.That(app.Screen, Is.EqualTo(AppScreen.ControlsSettings));
            Manual(AppCommand.Back()); Manual(AppCommand.Back());
            Assert.That(app.Screen, Is.EqualTo(AppScreen.MainMenu));
            Assert.That(Manual(AppCommand.Back()).Applied, Is.False);
        }

        [Test] public void ExitNeedsConfirmationAndCanBeCancelled()
        {
            Assert.That(Manual(AppCommand.ConfirmExit()).Applied, Is.False);
            Manual(AppCommand.RequestExit());
            Assert.That(app.ExitPending, Is.True); Assert.That(requests, Is.Empty);
            Manual(AppCommand.CancelExit());
            Assert.That(app.ExitPending, Is.False);
            Manual(AppCommand.RequestExit()); Manual(AppCommand.Navigate(AppScreen.MatchSetup));
            Assert.That(app.ExitPending, Is.False, "Another command dismisses the prompt.");
            Manual(AppCommand.RequestExit()); Manual(AppCommand.ConfirmExit());
            Assert.That(requests, Is.EqualTo(new[] { AppRequest.Quit }));
        }

        [Test] public void TestMapIsAHostRequest()
        {
            Assert.That(Manual(AppCommand.LaunchTestMap()).Applied, Is.True);
            Assert.That(requests, Is.EqualTo(new[] { AppRequest.LaunchTestMap }));
        }

        [Test] public void StartMatchIsReportedUnavailable() =>
            Assert.That(Manual(AppCommand.StartMatch()).Outcome, Is.EqualTo(CommandOutcome.Unavailable));

        [Test] public void OptionsAreValidatedAndPersisted()
        {
            Assert.That(Manual(AppCommand.SetOption("gfx.shadows", 3)).Applied, Is.True);
            Assert.That(Manual(AppCommand.SetOption("gfx.shadows", 7)).Applied, Is.False);
            Assert.That(Manual(AppCommand.SetOption("gfx.vsync", .5f)).Applied, Is.False);
            Assert.That(Manual(AppCommand.SetOption("controls.cameraSpeed", 101)).Applied, Is.False);
            Assert.That(Manual(AppCommand.SetOption("run.code", 1)).Applied, Is.False);
            Assert.That(Manual(AppCommand.SetOption(MenuCatalog.Difficulty, 2)).Applied, Is.False, "Locked choice.");
            Assert.That(Manual(AppCommand.SetKey("key.talk", "B")).Applied, Is.True);
            Assert.That(Manual(AppCommand.SetKey("key.talk", "a=b")).Applied, Is.False);
            var reloaded = new AppController(store);
            Assert.That(MenuCatalog.Value(reloaded.Settings, MenuCatalog.Find("gfx.shadows")), Is.EqualTo(3));
            Assert.That(MenuCatalog.Key(reloaded.Settings.Keys, MenuCatalog.Find("key.talk")), Is.EqualTo("B"));
        }

        [Test] public void LiveOptionsRouteToExistingSettings()
        {
            Manual(AppCommand.SetOption(MenuCatalog.MasterVolume, 40));
            Manual(AppCommand.SetOption(MenuCatalog.AssistantImage, 1));
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(.4f).Within(.0001));
            Assert.That(app.Settings.AssistantFaction, Is.EqualTo(FactionId.Resonance));
            Assert.That(store.Value.Options.ContainsKey(MenuCatalog.MasterVolume), Is.False);
        }

        [Test] public void ResetAffectsOnlyThatTab()
        {
            Manual(AppCommand.SetOption("gfx.shadows", 0)); Manual(AppCommand.SetOption("audio.music", 5)); Manual(AppCommand.SetVolume(.3f));
            Manual(AppCommand.ResetOptions(AppScreen.GraphicsSettings));
            Assert.That(Option("gfx.shadows"), Is.EqualTo(MenuCatalog.Find("gfx.shadows").Default));
            Assert.That(Option("audio.music"), Is.EqualTo(5));
            Manual(AppCommand.ResetOptions(AppScreen.AudioSettings));
            Assert.That(Option("audio.music"), Is.EqualTo(70)); Assert.That(app.Settings.MasterVolume, Is.EqualTo(1f));
        }

        [Test] public void StoredGarbageIsIgnored()
        {
            store.Value = new SettingsSnapshot(1f, FactionId.Expedition,
                new Dictionary<string, float> { ["gfx.shadows"] = 99, ["unknown"] = 1, ["ui.cursor"] = 0 }, new Dictionary<string, string> { ["key.talk"] = "" });
            var loaded = new AppController(store);
            Assert.That(loaded.Settings.Options.Keys, Is.EquivalentTo(new[] { "ui.cursor" }));
            Assert.That(loaded.Settings.Keys, Is.Empty);
        }

        [TestCase("открой графику", AppScreen.GraphicsSettings)]
        [TestCase("Открой настройки звука", AppScreen.AudioSettings)]
        [TestCase("покажи настройки управления", AppScreen.ControlsSettings)]
        [TestCase("вкладка интерфейс", AppScreen.InterfaceSettings)]
        [TestCase("штаб", AppScreen.MainMenu)]
        [TestCase("Верни меня в основное меню", AppScreen.MainMenu)]
        [TestCase("открой схватку", AppScreen.MatchSetup)]
        public void NavigationPhrases(string text, AppScreen expected)
        {
            Assert.That(Say(text).Applied, Is.True);
            Assert.That(app.Screen, Is.EqualTo(expected));
        }

        [TestCase("включи вертикальную синхронизацию", "gfx.vsync", 1)]
        [TestCase("Выключи тактический курсор", "ui.cursor", 0)]
        [TestCase("тени высокие", "gfx.shadows", 3)]
        [TestCase("сглаживание FXAA", "gfx.aa", 1)]
        [TestCase("разрешение 2560 на 1440", "gfx.resolution", 4)]
        [TestCase("режим экрана окно без рамки", "gfx.mode", 1)]
        [TestCase("ограничение кадров 144", "gfx.fps", 3)]
        [TestCase("качество ультра", "gfx.preset", 3)]
        [TestCase("скорость игры очень быстрая", "game.speed", 2)]
        [TestCase("скорость камеры 70", "controls.cameraSpeed", 70)]
        [TestCase("увеличь яркость на 15", "gfx.brightness", 65)]
        [TestCase("сделай яркость меньше", "gfx.brightness", 40)]
        [TestCase("уменьши музыку до 11 процентов", "audio.music", 11)]
        [TestCase("эффекты 50", "audio.effects", 50)]
        [TestCase("эффекты высокие", "gfx.effects", 2)]
        [TestCase("громкость 11 процентов", MenuCatalog.MasterVolume, 11)]
        [TestCase("Поменяй ассистента на вторую фракцию", MenuCatalog.AssistantImage, 1)]
        [TestCase("покажи координатную сетку", "ui.grid", 1)]
        [TestCase("переключи анимации", "ui.motion", 0)]
        public void OptionPhrases(string text, string option, float expected)
        {
            Assert.That(Say(text).Applied, Is.True);
            Assert.That(Option(option), Is.EqualTo(expected).Within(.0001));
        }

        [Test] public void KeyAndMatchPhrases()
        {
            Say("назначь разговор на B");
            Assert.That(MenuCatalog.Key(app.Settings.Keys, MenuCatalog.Find("key.talk")), Is.EqualTo("B"));
            Say("клавиша строительства пробел");
            Assert.That(MenuCatalog.Key(app.Settings.Keys, MenuCatalog.Find("key.build")), Is.EqualTo("ПРОБЕЛ"));
            Say("противник вторая фракция");
            Assert.That(app.EnemyFaction, Is.EqualTo(FactionId.Resonance)); Assert.That(app.Screen, Is.EqualTo(AppScreen.MatchSetup));
            Assert.That(Say("сложность высокая").Applied, Is.False, "Locked difficulty is refused by the controller.");
            Say("сбрось настройки управления");
            Assert.That(MenuCatalog.Key(app.Settings.Keys, MenuCatalog.Find("key.talk")), Is.EqualTo("V"));
        }

        [Test] public void AnotherVariantPhrases()
        {
            Say("переключи ассистента на другого");
            Assert.That(app.Settings.AssistantFaction, Is.EqualTo(FactionId.Resonance));
            Say("смени ассистента");
            Assert.That(app.Settings.AssistantFaction, Is.EqualTo(FactionId.Expedition));
            Say("поставь другие тени");
            Assert.That(Option("gfx.shadows"), Is.Not.EqualTo(MenuCatalog.Find("gfx.shadows").Default));
            Say("режим экрана другой");
            Assert.That(Option("gfx.mode"), Is.EqualTo(1));
            Assert.That(Say("другой режим микрофона").Applied, Is.False, "Only a locked variant remains.");
            Say("противник другой");
            Assert.That(app.EnemyFaction, Is.EqualTo(FactionId.Expedition));
            Say("смени противника");
            Assert.That(app.EnemyFaction, Is.EqualTo(FactionId.Resonance));
        }

        [Test] public void LifecyclePhrases()
        {
            Say("открой полигон");
            Assert.That(requests, Is.EqualTo(new[] { AppRequest.LaunchTestMap }));
            Say("выйди из игры");
            Assert.That(app.ExitPending, Is.True);
            Assert.That(Parse("отмена").Command.Kind, Is.EqualTo(AppCommandKind.CancelExit));
            Say("да");
            Assert.That(requests.Last(), Is.EqualTo(AppRequest.Quit));
        }

        [TestCase("помощь")]
        [TestCase("какие тени?")]
        [TestCase("кампания")]
        [TestCase("курсор")]
        public void InformationalReplies(string text)
        {
            var result = Parse(text);
            Assert.That(result.HasCommand, Is.False); Assert.That(result.IsAnswer, Is.True); Assert.That(result.Message, Is.Not.Empty);
        }

        [TestCase("тени фиолетовые")]
        [TestCase("скорость камеры 300")]
        [TestCase("противник")]
        public void MenuClarifications(string text)
        {
            var result = Parse(text);
            Assert.That(result.HasCommand, Is.False); Assert.That(result.IsAnswer, Is.False);
        }

        // Left to the existing rules and model: negation, relative volume words, compound and unrelated requests.
        [TestCase("не включай курсор")]
        [TestCase("Сделай потише")]
        [TestCase("Открой звук и поставь 11 процентов")]
        [TestCase("Какая сегодня погода?")]
        [TestCase("Удали карты и поставь громкость 11 процентов")]
        [TestCase("да")]
        public void NotOwnedByMenuGrammar(string text) => Assert.That(MenuCommandGrammar.TryParse(text, app.State, out _), Is.False);

        [Test] public async Task MenuCommandsSkipTheModel()
        {
            var model = new ForbiddenInterpreter();
            var input = new AppCommandInput(app, new MenuCommandInterpreter(model));
            var result = await input.SubmitTranscriptAsync("открой графику", new CommandContext(app.Session, CommandSource.Text, app.Revision));
            Assert.That(result.Applied, Is.True); Assert.That(app.Screen, Is.EqualTo(AppScreen.GraphicsSettings));
            Assert.That((await input.SubmitTranscriptAsync("помощь", new CommandContext(app.Session, CommandSource.Text, app.Revision))).Outcome, Is.EqualTo(CommandOutcome.Answered));
            Assert.That(model.Calls, Is.Zero);
            await input.SubmitTranscriptAsync("Сделай потише", new CommandContext(app.Session, CommandSource.Text, app.Revision));
            Assert.That(model.Calls, Is.EqualTo(1));
        }

        // Every visible parameter and tab of the menu is reachable by a text command built from its own label.
        public static IEnumerable EveryOption()
        {
            foreach (var option in MenuCatalog.Options) yield return new TestCaseData(option.Id).SetName("Command for " + option.Id);
        }

        [TestCaseSource(nameof(EveryOption))]
        public void EveryOptionIsReachableByItsTitle(string id)
        {
            var option = MenuCatalog.Find(id);
            var title = option.Title.ToLowerInvariant();
            var target = Enumerable.Range(0, Math.Max(1, option.Choices.Count)).Last(i => !option.Locked.Contains(i));
            string text;
            switch (option.Kind)
            {
                case OptionKind.Toggle: text = "включи " + title; break;
                case OptionKind.Range: text = title + " " + Math.Round((option.Min + option.Max) / 2); break;
                case OptionKind.Choice: text = title + " " + option.Choices[target]; break;
                default: text = "назначь " + title + " на K"; break;
            }
            var parsed = Parse(text);
            Assert.That(parsed.HasCommand, Is.True, text + " → " + parsed.Message);
            if (option.Kind == OptionKind.Key)
            {
                Assert.That(parsed.Command.Kind, Is.EqualTo(AppCommandKind.SetKey)); Assert.That(parsed.Command.Option, Is.EqualTo(id));
                Assert.That(parsed.Command.Key, Is.EqualTo("K"));
                return;
            }
            Assert.That(parsed.Command.Kind, Is.EqualTo(AppCommandKind.SetOption), text);
            Assert.That(parsed.Command.Option, Is.EqualTo(id), text);
            var expected = option.Kind == OptionKind.Toggle ? 1 : option.Kind == OptionKind.Range ? (float)Math.Round((option.Min + option.Max) / 2) : target;
            Assert.That(parsed.Command.Value, Is.EqualTo(expected), text);
        }

        [Test] public void EveryTabIsReachable()
        {
            foreach (var tab in MenuCatalog.Tabs)
            {
                Say("открой настройки " + tab.Title.ToLowerInvariant());
                Assert.That(app.Screen, Is.EqualTo(tab.Screen), tab.Title);
            }
        }
    }
}
