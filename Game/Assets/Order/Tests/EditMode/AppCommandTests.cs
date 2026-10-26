using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Order.Application;
using Order.Contracts;
using Order.Input;
using Order.Simulation;
using Order.Voice;

namespace Order.Tests.EditMode
{
    public sealed class AppCommandTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            public SettingsSnapshot Value = SettingsSnapshot.Default;
            public bool Fail;
            public SettingsSnapshot Load() => Value;
            public void Save(SettingsSnapshot value) { if (Fail) throw new InvalidOperationException(); Value = value; }
        }
        private sealed class DeferredInterpreter : IAppIntentInterpreter
        {
            public readonly TaskCompletionSource<Interpretation> Completion = new TaskCompletionSource<Interpretation>();
            public AppCommandState Captured;
            public Task<Interpretation> InterpretAsync(string text, AppCommandState state, CancellationToken cancellationToken)
            { Captured = state; return Completion.Task; }
        }
        private MemoryStore store;
        private AppController app;
        [SetUp] public void SetUp() { store = new MemoryStore(); app = new AppController(store); }
        private CommandResult Manual(AppCommand command) => app.Execute(command, new CommandContext(app.Session, CommandSource.Manual));
        private Interpretation Validate(string action, double? value = null, string text = "", bool openAudio = false, int? faction = null, string screen = "none") =>
            IntentDecisionValidator.Validate(new IntentDecision { Action = action, Value = value, Screen = screen, Faction = faction, OpenAudio = openAudio }, text, app.State);
        [TestCase(0f)] [TestCase(0.11f)] [TestCase(1f)]
        public void ManualVolumePersists(float value)
        { Assert.That(Manual(AppCommand.SetVolume(value)).Applied, Is.True); Assert.That(store.Value.MasterVolume, Is.EqualTo(value)); }
        [TestCase(-1f)] [TestCase(1.5f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidVolumeDoesNotMutate(float value)
        { Assert.That(Manual(AppCommand.SetVolume(value)).Applied, Is.False); Assert.That(app.Settings.MasterVolume, Is.EqualTo(1f)); }
        [Test] public void ExplicitElevenPercentSurvivesValidation()
        {
            var parsed = Validate("set_volume", 11, "громкость 11 процентов");
            Assert.That(parsed.HasCommand, Is.True); Manual(parsed.Command);
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(0.11f));
        }
        [Test] public void ModelCannotSilentlyReplaceExplicitNumber()
        { Assert.That(Validate("set_volume", 50, "громкость 11 процентов").HasCommand, Is.False); }
        [Test] public void RelativeChangeUsesCapturedVolumeAndClamps()
        {
            Manual(AppCommand.SetVolume(0.5f)); Manual(Validate("adjust_volume", -11, "убавь на 11 процентов").Command);
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(0.39f).Within(0.0001));
            Manual(Validate("adjust_volume", -90).Command); Assert.That(app.Settings.MasterVolume, Is.Zero);
        }
        [Test] public void PersistenceFailureDoesNotPartiallyNavigate()
        {
            store.Fail = true;
            Assert.That(Manual(Validate("set_volume", 50, openAudio: true).Command).Applied, Is.False);
            Assert.That(app.Screen, Is.EqualTo(AppScreen.MainMenu)); Assert.That(app.Settings.MasterVolume, Is.EqualTo(1f));
        }
        [TestCase("delete_maps")] [TestCase("clarify")] [TestCase("run_code")]
        public void UnsupportedActionsNeverBecomeCommands(string action) => Assert.That(Validate(action).HasCommand, Is.False);
        [Test] public void ExtraParametersRejectDecision() => Assert.That(Validate("set_volume", 11, faction: 2).HasCommand, Is.False);
        [Test] public void MissingEnemyNeedsClarification() => Assert.That(Validate("prepare_match").HasCommand, Is.False);
        [Test] public void MatchDraftUsesExplicitFaction()
        { Manual(Validate("prepare_match", faction: 2).Command); Assert.That(app.HasMatchDraft, Is.True); Assert.That(app.EnemyFaction, Is.EqualTo(FactionId.Resonance)); }
        [Test] public void AssistantChoicePersists()
        { Manual(Validate("assistant_faction", faction: 2).Command); Assert.That(new AppController(store).Settings.AssistantFaction, Is.EqualTo(FactionId.Resonance)); }
        [Test] public void InvalidStoredSettingsRecover()
        {
            store.Value = new SettingsSnapshot(float.NaN, (FactionId)99); var recovered = new AppController(store);
            Assert.That(recovered.Settings.MasterVolume, Is.EqualTo(1f)); Assert.That(recovered.Settings.AssistantFaction, Is.EqualTo(FactionId.Expedition));
        }
        [Test] public async Task ModelCannotOverwriteManualChange()
        {
            var model = new DeferredInterpreter(); var input = new AppCommandInput(app, model);
            var pending = input.SubmitTranscriptAsync("потише", new CommandContext(app.Session, CommandSource.Text));
            Manual(AppCommand.SetVolume(0.6f)); model.Completion.SetResult(new Interpretation(AppCommand.SetVolume(0.9f)));
            Assert.That((await pending).Outcome, Is.EqualTo(CommandOutcome.StaleContext));
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(0.6f)); Assert.That(model.Captured.MasterVolume, Is.EqualTo(1f));
        }
        [Test] public async Task CancellationPreventsExecution()
        {
            var model = new DeferredInterpreter(); var input = new AppCommandInput(app, model);
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = input.SubmitTranscriptAsync("потише", new CommandContext(app.Session, CommandSource.Text), cancellation.Token);
                cancellation.Cancel(); model.Completion.SetResult(new Interpretation(AppCommand.SetVolume(0.9f)));
                Assert.That((await pending).Applied, Is.False); Assert.That(app.Settings.MasterVolume, Is.EqualTo(1f));
            }
        }
        [Test] public async Task StaleSessionCannotExecute()
        {
            var model = new DeferredInterpreter(); var input = new AppCommandInput(app, model);
            var captured = new CommandContext(app.Session, CommandSource.Voice); app.AdvanceSession();
            Assert.That((await input.SubmitTranscriptAsync("потише", captured)).Outcome, Is.EqualTo(CommandOutcome.StaleContext));
        }
        [Test] public async Task BackendFailureLeavesManualControlsAvailable()
        {
            var model = new DeferredInterpreter(); var input = new AppCommandInput(app, model);
            model.Completion.SetException(new InvalidOperationException("offline"));
            Assert.That((await input.SubmitTranscriptAsync("потише", new CommandContext(app.Session, CommandSource.Text))).Outcome, Is.EqualTo(CommandOutcome.Unavailable));
            Assert.That(input.SubmitManual(AppCommand.SetVolume(0.7f)).Applied, Is.True);
        }
    }
}
