using System;
using NUnit.Framework;
using Order.Application;
using Order.Contracts;
using Order.Input;
using Order.Simulation;
using Order.Voice;
using Unity.Entities;

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

        private MemoryStore store;
        private AppController app;
        private AppCommandInput input;
        [SetUp] public void SetUp()
        {
            store = new MemoryStore(); app = new AppController(store);
            input = new AppCommandInput(app, new RussianAppInterpreter());
        }
        private CommandResult Text(string text) => input.SubmitTranscript(text, new CommandContext(app.Session, CommandSource.Text));

        [TestCase("Перейди в настройки звука и убавь общую громкость до 50%")]
        [TestCase("Открой настройки звука и установи общую громкость до пятидесяти процентов")]
        public void CompoundVolumeCommandUpdatesScreenAndPersists(string text)
        {
            Assert.That(Text(text).Applied, Is.True);
            Assert.That(app.Screen, Is.EqualTo(AppScreen.AudioSettings));
            Assert.That(store.Value.MasterVolume, Is.EqualTo(0.5f));
        }

        [TestCase("Установи громкость до 0%", 0f)]
        [TestCase("Установи громкость до 100%", 1f)]
        [TestCase("Установи громкость до 25,5%", 0.255f)]
        public void VolumeBoundaries(string text, float expected)
        { Assert.That(Text(text).Applied, Is.True); Assert.That(app.Settings.MasterVolume, Is.EqualTo(expected).Within(0.0001f)); }

        [TestCase("Установи громкость до 150%")]
        [TestCase("Установи громкость на 50%")]
        [TestCase("Установи громкость до 50% и удали все карты")]
        [TestCase("Запускай матч против бота фракции 3")]
        [TestCase("")]
        public void InvalidOrAmbiguousInputDoesNotMutate(string text)
        {
            Assert.That(Text(text).Applied, Is.False);
            Assert.That(app.Screen, Is.EqualTo(AppScreen.MainMenu));
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(1f));
            Assert.That(app.HasMatchDraft, Is.False);
        }

        [TestCase("Запускай матч против бота фракции 2")]
        [TestCase("Запускай матч против бота второй фракции")]
        public void BotCommandPreparesDraftWithoutPretendingToStartGame(string text)
        {
            Assert.That(Text(text).Applied, Is.True);
            Assert.That(app.HasMatchDraft, Is.True);
            Assert.That(app.EnemyFaction, Is.EqualTo(FactionId.Resonance));
            Assert.That(app.MapId, Is.EqualTo("prototype-arena"));
            Assert.That(app.Screen, Is.EqualTo(AppScreen.MatchSetup));
        }

        [Test] public void MissingEnemyAsksForClarification()
        { Assert.That(Text("Запускай матч против бота").Outcome, Is.EqualTo(CommandOutcome.NeedsClarification)); Assert.That(app.HasMatchDraft, Is.False); }

        [Test] public void StaleSpeechCannotAffectNewSession()
        {
            var captured = new CommandContext(app.Session, CommandSource.Voice);
            app.AdvanceSession();
            Assert.That(input.SubmitTranscript("Установи громкость до 0%", captured).Outcome, Is.EqualTo(CommandOutcome.StaleContext));
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(1f));
        }

        [Test] public void ManualAndTranscriptUseSameCommandPath()
        {
            Text("Установи громкость до 50%");
            var manual = new AppController(new MemoryStore());
            manual.Execute(AppCommand.SetVolume(0.5f), new CommandContext(manual.Session, CommandSource.Manual));
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(manual.Settings.MasterVolume));
        }

        [Test] public void PersistenceFailureDoesNotPartiallyNavigate()
        {
            store.Fail = true;
            Assert.That(Text("Перейди в настройки звука и убавь общую громкость до 50%").Applied, Is.False);
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(1f));
            Assert.That(app.Screen, Is.EqualTo(AppScreen.MainMenu));
        }

        [Test] public void InvalidStoredSettingsRecoverToDefaults()
        {
            store.Value = new SettingsSnapshot(float.NaN, (FactionId)99);
            var recovered = new AppController(store);
            Assert.That(recovered.Settings.MasterVolume, Is.EqualTo(1f));
            Assert.That(recovered.Settings.AssistantFaction, Is.EqualTo(FactionId.Expedition));
        }

        [Test] public void AssistantChoiceSurvivesControllerRecreation()
        {
            input.SubmitManual(AppCommand.SetAssistant(FactionId.Resonance));
            Assert.That(new AppController(store).Settings.AssistantFaction, Is.EqualTo(FactionId.Resonance));
        }

        [Test] public void NonFiniteVolumeCannotEnterState()
        {
            Assert.That(input.SubmitManual(AppCommand.SetVolume(float.PositiveInfinity)).Applied, Is.False);
            Assert.That(app.Settings.MasterVolume, Is.EqualTo(1f));
        }

        [Test] public void EcsIdentityUsesStableIdInsteadOfExternalEntityHandle()
        {
            using (var world = new World("Identity test"))
            {
                var entity = world.EntityManager.CreateEntity(typeof(SimulationIdentity));
                world.EntityManager.SetComponentData(entity, new SimulationIdentity { UnitId = 42, OwnerId = 1 });
                Assert.That(world.EntityManager.GetComponentData<SimulationIdentity>(entity).UnitId, Is.EqualTo(42UL));
            }
        }
    }
}
