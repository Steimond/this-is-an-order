using System;
using Order.Contracts;
using Order.Content;

namespace Order.Application
{
    public sealed class AppController : IAppCommandSink
    {
        private readonly ISettingsStore store;
        public long Session { get; private set; } = 1;
        public long Revision { get; private set; }
        public AppCommandState State => new AppCommandState(Session, Revision, Screen, Settings.MasterVolume, Settings.AssistantFaction, EnemyFaction);
        public AppScreen Screen { get; private set; } = AppScreen.MainMenu;
        public SettingsSnapshot Settings { get; private set; }
        public FactionId EnemyFaction { get; private set; } = FactionId.Resonance;
        public bool HasMatchDraft { get; private set; }
        public string MapId => PrototypeCatalog.MapId;
        public string DifficultyId => PrototypeCatalog.DifficultyId;
        public event Action Changed;

        public AppController(ISettingsStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            var loaded = store.Load();
            Settings = new SettingsSnapshot(IsVolume(loaded.MasterVolume) ? loaded.MasterVolume : 1f,
                IsFaction(loaded.AssistantFaction) ? loaded.AssistantFaction : FactionId.Expedition);
        }

        // Invalidate pending asynchronous work when a match/session is replaced or ended.
        public void AdvanceSession() { Session++; Revision++; Changed?.Invoke(); }

        public CommandResult Execute(AppCommand command, CommandContext context)
        {
            if (context.Session != Session)
                return new CommandResult(CommandOutcome.StaleContext, "Команда относится к предыдущему сеансу.");
            if (context.Revision >= 0 && context.Revision != Revision)
                return new CommandResult(CommandOutcome.StaleContext, "Состояние изменилось. Повторите команду.");

            switch (command.Kind)
            {
                case AppCommandKind.Navigate:
                    if (!Enum.IsDefined(typeof(AppScreen), command.Screen)) return Reject("Такого экрана нет.");
                    Screen = command.Screen;
                    break;
                case AppCommandKind.SetMasterVolume:
                    if (!IsVolume(command.Volume)) return Reject("Громкость должна быть от 0 до 100%.");
                    if (!Save(new SettingsSnapshot(command.Volume, Settings.AssistantFaction)))
                        return Reject("Не удалось сохранить настройки звука.");
                    if (command.OpenAudioSettings) Screen = AppScreen.AudioSettings;
                    break;
                case AppCommandKind.SetAssistantFaction:
                    if (!IsFaction(command.Faction)) return Reject("Такой фракции нет.");
                    if (!Save(new SettingsSnapshot(Settings.MasterVolume, command.Faction)))
                        return Reject("Не удалось сохранить образ ассистента.");
                    break;
                case AppCommandKind.PrepareBotMatch:
                    if (!IsFaction(command.Faction)) return Reject("Такой фракции нет.");
                    EnemyFaction = command.Faction;
                    HasMatchDraft = true;
                    Screen = AppScreen.MatchSetup;
                    break;
                default: return Reject("Неизвестная команда.");
            }

            Revision++;
            Changed?.Invoke();
            var message = command.Kind == AppCommandKind.PrepareBotMatch
                ? "Матч подготовлен. Игровая симуляция будет добавлена следующим этапом."
                : command.Kind == AppCommandKind.SetMasterVolume ? $"Общая громкость: {Settings.MasterVolume * 100:0.##}%." : "Готово.";
            return new CommandResult(CommandOutcome.Applied, message);
        }

        private bool Save(SettingsSnapshot settings)
        {
            try { store.Save(settings); Settings = settings; return true; }
            catch (Exception) { return false; }
        }
        private static bool IsVolume(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
        private static bool IsFaction(FactionId faction) => Enum.IsDefined(typeof(FactionId), faction);
        private static CommandResult Reject(string message) => new CommandResult(CommandOutcome.Rejected, message);
    }
}
