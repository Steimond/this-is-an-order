using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Order.Application;
using Order.Bootstrap;
using Order.Contracts;
using Order.Input;
using Order.Voice;

namespace Order.Tests.EditMode
{
    public sealed class PhrasebookTests
    {
        private sealed class SettingsMemory : ISettingsStore
        {
            public SettingsSnapshot Value = SettingsSnapshot.Default;
            public SettingsSnapshot Load() => Value;
            public void Save(SettingsSnapshot value) => Value = value;
        }
        private sealed class PhrasebookMemory : IPhrasebookStore
        {
            public List<PhraseEntry> Saved = new List<PhraseEntry>();
            public List<PhraseEntry> Pending = new List<PhraseEntry>();
            public int PendingFiles;
            public string Location => "memory/phrasebook.json";
            public string ImportFolder => "memory/import";
            public List<PhraseEntry> Load() => Saved.Select(Copy).ToList();
            public void Save(IReadOnlyList<PhraseEntry> entries) => Saved = entries.Select(Copy).ToList();
            public string Export(IReadOnlyList<PhraseEntry> entries) => "memory/export.json";
            public List<PhraseEntry> TakeImports(out int files) { files = PendingFiles; var taken = Pending; Pending = new List<PhraseEntry>(); PendingFiles = 0; return taken; }
            private static PhraseEntry Copy(PhraseEntry e) => new PhraseEntry { Phrase = e.Phrase, Command = e.Command, Learned = e.Learned, Agreements = e.Agreements, Uses = e.Uses };
        }
        // Stands in for the local model: returns a validated decision exactly like LocalIntentInterpreter does.
        private sealed class ScriptedModel : IAppIntentInterpreter
        {
            public IntentDecision Next;
            public int Calls;
            public Task<Interpretation> InterpretAsync(string text, AppCommandState state, CancellationToken cancellationToken)
            {
                Calls++;
                if (Next == null) return Task.FromResult(new Interpretation("model has no answer"));
                return Task.FromResult(IntentDecisionValidator.Validate(Next, text, state).WithDecision(Next));
            }
        }

        private AppController app;
        private PhrasebookMemory store;
        private Phrasebook book;
        private ScriptedModel model;
        private AppCommandInput input;

        [SetUp] public void SetUp()
        {
            app = new AppController(new SettingsMemory());
            store = new PhrasebookMemory();
            book = new Phrasebook(store);
            model = new ScriptedModel();
            input = new AppCommandInput(app, new PhrasebookInterpreter(new MenuCommandInterpreter(model), book));
        }

        private Task<CommandResult> Say(string text) =>
            input.SubmitTranscriptAsync(text, new CommandContext(app.Session, CommandSource.Text, app.Revision));
        private static IntentDecision Decision(string action, double? value = null, int? faction = null, string screen = "none", bool openAudio = false) =>
            new IntentDecision { Action = action, Value = value, Faction = faction, Screen = screen, OpenAudio = openAudio };
        private void Volume(float value) => app.Execute(AppCommand.SetVolume(value), new CommandContext(app.Session, CommandSource.Manual));

        [Test] public async Task LearnsAfterTwoAgreementsAndGeneralizesTheNumber()
        {
            model.Next = Decision("set_volume", 30);
            Assert.That((await Say("Поставь звук на 30")).Applied, Is.True);
            var entry = book.Find("поставь звук на {n}");
            Assert.That(entry.Learned, Is.False); Assert.That(entry.Command, Is.EqualTo("громкость {n}"));
            await Say("поставь звук на 30");
            Assert.That(entry.Learned, Is.True);
            model.Next = null;
            Assert.That((await Say("поставь звук на сорок пять")).Applied, Is.True);
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(.45f).Within(.0001));
            Assert.That(model.Calls, Is.EqualTo(2), "The learned phrase resolves without the model.");
            Assert.That(store.Saved.Single().Uses, Is.EqualTo(1), "The phrasebook is persisted.");
        }

        [Test] public async Task RelativeMeaningIsLearnedNotTheExecutedValue()
        {
            model.Next = Decision("adjust_volume", -10);
            await Say("сделай чуточку тише"); await Say("сделай чуточку тише");
            Volume(.5f); model.Next = null;
            await Say("сделай чуточку тише");
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(.4f).Within(.0001));
            Assert.That(book.Find("сделай чуточку тише").Command, Is.EqualTo("убавь громкость на 10"));
        }

        [Test] public async Task WrongMeaningIsForgotten()
        {
            model.Next = Decision("navigate", screen: "match_setup");
            await Say("в бой с машиной"); await Say("в бой с машиной");
            Assert.That(book.Find("в бой с машиной").Learned, Is.True);
            await Say("в бой с машиной");
            Assert.That((await Say("не то")).Outcome, Is.EqualTo(CommandOutcome.Answered));
            Assert.That(book.Entries, Is.Empty);
        }

        [Test] public async Task ConfirmationLearnsImmediately()
        {
            model.Next = Decision("adjust_volume", 10);
            await Say("накинь звука");
            Assert.That(book.Find("накинь звука").Learned, Is.False);
            Assert.That((await Say("верно")).Outcome, Is.EqualTo(CommandOutcome.Answered));
            Assert.That(book.Find("накинь звука").Learned, Is.True);
        }

        [Test] public async Task TeachingGoesThroughTheMenuGrammar()
        {
            Volume(.5f);
            Assert.That((await Say("запомни «тише» значит «убавь громкость на 5»")).Outcome, Is.EqualTo(CommandOutcome.Answered));
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(.5f), "Teaching does not execute the command.");
            await Say("тише");
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(.45f).Within(.0001));
            await Say("запомни: звук на 30 — громкость 30");
            await Say("звук на 70");
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(.7f).Within(.0001));
            Assert.That((await Say("запомни «шум» значит «сделай что-нибудь»")).Outcome, Is.EqualTo(CommandOutcome.NeedsClarification));
            Assert.That(book.Find("шум"), Is.Null);
            Assert.That(model.Calls, Is.Zero);
        }

        [Test] public async Task UnsafeMeaningsAreNotLearned()
        {
            model.Next = Decision("set_volume", 26, openAudio: true);
            await Say("открой аудио и громкость 26 процентов");
            model.Next = Decision("navigate", screen: "audio_settings");
            await Say("открой 2 вкладку");
            model.Next = Decision("clarify");
            await Say("что-то непонятное");
            Assert.That(book.Entries, Is.Empty);
            await Say("открой графику");
            Assert.That(book.Entries, Is.Empty, "Grammar commands never reach the phrasebook.");
        }

        [Test] public async Task ClearingNeedsConfirmation()
        {
            await Say("запомни «тише» значит «убавь громкость на 5»");
            await Say("очисти словарь");
            Assert.That(book.Entries.Count, Is.EqualTo(1));
            await Say("подтверждаю очистку");
            Assert.That(book.Entries, Is.Empty);
        }

        [Test] public async Task ImportSkipsDuplicatesAndInvalidCommands()
        {
            await Say("запомни «тише» значит «убавь громкость на 5»");
            store.PendingFiles = 1;
            store.Pending = new List<PhraseEntry>
            {
                new PhraseEntry { Phrase = "громче громче", Command = "прибавь громкость на 20" },
                new PhraseEntry { Phrase = "тише", Command = "громкость 1" },
                new PhraseEntry { Phrase = "колдуй", Command = "сделай магию" },
                new PhraseEntry { Phrase = "яркость на {n}", Command = "яркость {n}" },
            };
            var result = await Say("импортируй словарь");
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.Answered), result.Message);
            Assert.That(book.Entries.Select(e => e.Phrase), Is.EquivalentTo(new[] { "тише", "громче громче", "яркость на {n}" }));
            Assert.That(book.Find("тише").Command, Is.EqualTo("убавь громкость на 5"), "Own entries win.");
        }

        [Test] public void EditingIsValidated()
        {
            book.Teach("тише", "убавь громкость на 5", app.State, out var entry);
            Assert.That(book.Edit(entry, "тише", "сделай что-нибудь", app.State), Is.Not.Null);
            Assert.That(book.Edit(entry, "звук на {n}", "громкость 10", app.State), Is.Not.Null, "{n} must appear on both sides.");
            Assert.That(book.Edit(entry, "потише", "убавь громкость на 3", app.State), Is.Null);
            Assert.That(entry.Phrase, Is.EqualTo("потише")); Assert.That(entry.Command, Is.EqualTo("убавь громкость на 3"));
        }

        [TestCase("Поставь звук на тридцать пять процентов", "поставь звук на {n} процентов", 35)]
        [TestCase("громкость 11%", "громкость {n} %", 11)]
        [TestCase("сделай тише", "сделай тише", null)]
        [TestCase("с 10 до 20", "с 10 до 20", null)]
        public void TemplatesKeepOneNumberSlot(string text, string template, double? number)
        {
            Assert.That(Phrasebook.Template(text, out var found), Is.EqualTo(template));
            Assert.That(found, Is.EqualTo(number));
        }

        [Test] public void FileStoreRoundTripsExportsAndImports()
        {
            var root = Path.Combine(Path.GetTempPath(), "order-phrasebook-" + Guid.NewGuid().ToString("N"));
            try
            {
                var files = new FilePhrasebookStore(root);
                var entries = new List<PhraseEntry> { new PhraseEntry { Phrase = "тише", Command = "убавь громкость на 5", Learned = true, Uses = 3 } };
                files.Save(entries); files.Save(entries);
                Assert.That(files.Load().Single().Uses, Is.EqualTo(3));
                var exported = files.Export(entries);
                Directory.CreateDirectory(files.ImportFolder);
                File.Copy(exported, Path.Combine(files.ImportFolder, "friend.json"));
                Assert.That(files.TakeImports(out var count).Single().Phrase, Is.EqualTo("тише"));
                Assert.That(count, Is.EqualTo(1));
                Assert.That(files.TakeImports(out count), Is.Empty, "Imported files are moved aside.");
                File.WriteAllText(files.Location, "{ not json");
                Assert.That(files.Load(), Is.Empty);
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(files.Location), "phrasebook.damaged-*.json"), Is.Not.Empty, "A damaged file is kept.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
