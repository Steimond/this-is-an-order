using System;
using System.Collections.Generic;
using System.Linq;
using Order.Contracts;
using Order.Content;

namespace Order.Application
{
    public sealed class AppController : IAppCommandSink
    {
        private const int MaxKeyLength = 24;
        private readonly ISettingsStore store;
        public long Session { get; private set; } = 1;
        public long Revision { get; private set; }
        public AppCommandState State => new AppCommandState(Session, Revision, Screen, Settings.MasterVolume, Settings.AssistantFaction, EnemyFaction,
            ExitPending, Settings.Options, Settings.Keys, PlayerFaction);
        public AppScreen Screen { get; private set; } = AppScreen.MainMenu;
        // Screen to return to from the options window, and the tab the window reopens on.
        public AppScreen ReturnScreen { get; private set; } = AppScreen.MainMenu;
        public AppScreen SettingsTab { get; private set; } = AppScreen.GameSettings;
        public SettingsSnapshot Settings { get; private set; }
        public FactionId EnemyFaction { get; private set; } = FactionId.Resonance;
        // The player's faction for the next match; the test ground plays it against EnemyFaction.
        public FactionId PlayerFaction { get; private set; } = FactionId.Expedition;
        public bool HasMatchDraft { get; private set; }
        public bool ExitPending { get; private set; }
        public string MapId => PrototypeCatalog.MapId;
        public string DifficultyId => PrototypeCatalog.DifficultyId;
        public event Action Changed;
        // Raised after a command is applied; the host performs the lifecycle action.
        public event Action<AppRequest> Requested;

        public AppController(ISettingsStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            var loaded = store.Load();
            var options = new Dictionary<string, float>();
            foreach (var pair in loaded.Options)
            {
                var option = MenuCatalog.Find(pair.Key);
                if (option != null && option.Kind != OptionKind.Key && IsStoredOption(option) && MenuCatalog.IsValid(option, pair.Value)) options[pair.Key] = pair.Value;
            }
            var keys = new Dictionary<string, string>();
            foreach (var pair in loaded.Keys)
                if (MenuCatalog.Find(pair.Key)?.Kind == OptionKind.Key && IsKey(pair.Value)) keys[pair.Key] = pair.Value;
            Settings = new SettingsSnapshot(IsVolume(loaded.MasterVolume) ? loaded.MasterVolume : 1f,
                IsFaction(loaded.AssistantFaction) ? loaded.AssistantFaction : FactionId.Expedition, options, keys);
        }

        // Invalidate pending asynchronous work when a match/session is replaced or ended.
        public void AdvanceSession() { Session++; Revision++; Changed?.Invoke(); }

        public CommandResult Execute(AppCommand command, CommandContext context)
        {
            if (context.Session != Session)
                return new CommandResult(CommandOutcome.StaleContext, "Команда относится к предыдущему сеансу.");
            if (context.Revision >= 0 && context.Revision != Revision)
                return new CommandResult(CommandOutcome.StaleContext, "Состояние изменилось. Повторите команду.");

            string message = "Готово.";
            AppRequest? request = null;
            var keepExitPrompt = false;
            switch (command.Kind)
            {
                case AppCommandKind.Navigate:
                    if (!Enum.IsDefined(typeof(AppScreen), command.Screen)) return Reject("Такого экрана нет.");
                    if (command.Screen == AppScreen.TestMap) return Execute(AppCommand.LaunchTestMap(), context);
                    Show(command.Screen);
                    message = "Открываю: " + MenuCatalog.ScreenTitle(command.Screen) + ".";
                    break;
                case AppCommandKind.OpenSettings:
                    Show(SettingsTab);
                    message = "Открываю: " + MenuCatalog.ScreenTitle(SettingsTab) + ".";
                    break;
                case AppCommandKind.Back:
                case AppCommandKind.CancelExit:
                    if (ExitPending) { message = "Выход отменён."; break; }
                    if (command.Kind == AppCommandKind.CancelExit) return Reject("Выход не запрашивался.");
                    if (Screen.IsSettings()) Show(ReturnScreen);
                    else if (Screen == AppScreen.MatchSetup) Show(AppScreen.MainMenu);
                    else if (Screen == AppScreen.TestMap) return Reject("Вы на полигоне. Вернуться — «в штаб».");
                    else return Reject("Вы уже в штабе.");
                    message = "Возвращаюсь: " + MenuCatalog.ScreenTitle(Screen) + ".";
                    break;
                case AppCommandKind.SetMasterVolume:
                    if (!IsVolume(command.Volume)) return Reject("Громкость должна быть от 0 до 100%.");
                    if (!Save(new SettingsSnapshot(command.Volume, Settings.AssistantFaction, Settings.Options, Settings.Keys)))
                        return Reject("Не удалось сохранить настройки звука.");
                    if (command.OpenAudioSettings) Show(AppScreen.AudioSettings);
                    message = $"Общая громкость: {Settings.MasterVolume * 100:0.##}%.";
                    break;
                case AppCommandKind.SetAssistantFaction:
                    if (!IsFaction(command.Faction)) return Reject("Такой фракции нет.");
                    if (!Save(new SettingsSnapshot(Settings.MasterVolume, command.Faction, Settings.Options, Settings.Keys)))
                        return Reject("Не удалось сохранить образ ассистента.");
                    message = "Образ ассистента: " + PrototypeCatalog.FactionLabel(command.Faction) + ".";
                    break;
                case AppCommandKind.SetOption:
                    {
                        var option = MenuCatalog.Find(command.Option);
                        if (option == null || option.Kind == OptionKind.Key) return Reject("Такого параметра нет.");
                        if (!MenuCatalog.IsValid(option, command.Value)) return Reject(Range(option));
                        if (option.Kind == OptionKind.Choice && option.Locked.Contains((int)command.Value))
                            return Reject($"«{option.Title}»: вариант «{option.Choices[(int)command.Value]}» пока недоступен.");
                        if (option.Id == MenuCatalog.MasterVolume) return Execute(AppCommand.SetVolume(command.Value / 100f), context);
                        if (option.Id == MenuCatalog.AssistantImage) return Execute(AppCommand.SetAssistant((FactionId)(int)command.Value), context);
                        var options = new Dictionary<string, float>(Copy(Settings.Options)) { [option.Id] = command.Value };
                        if (!Save(new SettingsSnapshot(Settings.MasterVolume, Settings.AssistantFaction, options, Settings.Keys)))
                            return Reject("Не удалось сохранить настройки.");
                        message = $"{option.Title}: {MenuCatalog.Format(option, command.Value)}." + Placeholder(option);
                        break;
                    }
                case AppCommandKind.SetKey:
                    {
                        var option = MenuCatalog.Find(command.Option);
                        if (option == null || option.Kind != OptionKind.Key) return Reject("Такого действия для клавиши нет.");
                        if (!IsKey(command.Key)) return Reject("Не удалось распознать клавишу.");
                        var keys = new Dictionary<string, string>(Copy(Settings.Keys)) { [option.Id] = command.Key };
                        if (!Save(new SettingsSnapshot(Settings.MasterVolume, Settings.AssistantFaction, Settings.Options, keys)))
                            return Reject("Не удалось сохранить назначение клавиши.");
                        message = $"{option.Title}: {command.Key}." + Placeholder(option);
                        break;
                    }
                case AppCommandKind.ResetOptions:
                    {
                        if (!command.All && !command.Screen.IsSettings()) return Reject("Укажите вкладку настроек.");
                        var options = new Dictionary<string, float>(Copy(Settings.Options));
                        var keys = new Dictionary<string, string>(Copy(Settings.Keys));
                        var volume = Settings.MasterVolume; var assistant = Settings.AssistantFaction;
                        foreach (var option in MenuCatalog.Options)
                        {
                            if (!command.All && option.Tab != command.Screen && option.AlsoOn != command.Screen) continue;
                            if (!option.Tab.IsSettings()) continue;
                            options.Remove(option.Id); keys.Remove(option.Id);
                            if (option.Id == MenuCatalog.MasterVolume) volume = 1f;
                            if (option.Id == MenuCatalog.AssistantImage) assistant = FactionId.Expedition;
                        }
                        if (!Save(new SettingsSnapshot(volume, assistant, options, keys))) return Reject("Не удалось сохранить настройки.");
                        message = command.All ? "Все настройки сброшены." : "Сброшено: " + MenuCatalog.ScreenTitle(command.Screen) + ".";
                        break;
                    }
                case AppCommandKind.PrepareBotMatch:
                    if (!IsFaction(command.Faction)) return Reject("Такой фракции нет.");
                    EnemyFaction = command.Faction;
                    HasMatchDraft = true;
                    Show(AppScreen.MatchSetup);
                    message = "Матч подготовлен. Игровая симуляция будет добавлена следующим этапом.";
                    break;
                case AppCommandKind.ChoosePlayerFaction:
                    if (!IsFaction(command.Faction)) return Reject("Такой фракции нет.");
                    PlayerFaction = command.Faction;
                    Show(AppScreen.MatchSetup);
                    message = "Ваша фракция: " + PrototypeCatalog.FactionLabel(command.Faction) + ". Полигон начнётся за неё.";
                    break;
                case AppCommandKind.StartMatch:
                    return new CommandResult(CommandOutcome.Unavailable, "Боевой прототип — следующий этап. Пока доступен полигон: «открой полигон».");
                case AppCommandKind.LaunchTestMap:
                    if (Screen == AppScreen.TestMap) return Reject("Вы уже на полигоне.");
                    Show(AppScreen.TestMap);
                    request = AppRequest.LaunchTestMap;
                    message = "Открываю полигон Temple Basin.";
                    break;
                case AppCommandKind.RequestExit:
                    ExitPending = keepExitPrompt = true;
                    message = "Подтвердите выход: «да» — выйти, «отмена» — остаться.";
                    break;
                case AppCommandKind.ConfirmExit:
                    if (!ExitPending) return Reject("Сначала попросите выйти из игры.");
                    request = AppRequest.Quit;
                    message = "Завершаю сеанс.";
                    break;
                default: return Reject("Неизвестная команда.");
            }

            // Any other applied command dismisses a pending exit prompt.
            if (!keepExitPrompt) ExitPending = false;
            Revision++;
            Changed?.Invoke();
            if (request.HasValue) Requested?.Invoke(request.Value);
            return new CommandResult(CommandOutcome.Applied, message);
        }

        private void Show(AppScreen screen)
        {
            if (screen.IsSettings()) SettingsTab = screen;
            else ReturnScreen = screen;
            Screen = screen;
        }

        private bool Save(SettingsSnapshot settings)
        {
            try { store.Save(settings); Settings = settings; return true; }
            catch (Exception) { return false; }
        }

        private static Dictionary<TKey, TValue> Copy<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> source)
        {
            var copy = new Dictionary<TKey, TValue>();
            foreach (var pair in source) copy[pair.Key] = pair.Value;
            return copy;
        }

        private static string Range(MenuOption option) => option.Kind == OptionKind.Range
            ? $"«{option.Title}»: значение от {option.Min:0} до {option.Max:0}%."
            : option.Kind == OptionKind.Toggle ? $"«{option.Title}»: только вкл или выкл." : $"«{option.Title}»: такого варианта нет.";

        private static string Placeholder(MenuOption option) => option.Live ? "" : " Параметр сохранён и начнёт действовать вместе со своей системой.";
        private static bool IsStoredOption(MenuOption option) => option.Id != MenuCatalog.MasterVolume && option.Id != MenuCatalog.AssistantImage;
        private static bool IsKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= MaxKeyLength && key.IndexOfAny(new[] { '=', ';' }) < 0;
        private static bool IsVolume(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
        private static bool IsFaction(FactionId faction) => Enum.IsDefined(typeof(FactionId), faction);
        private static CommandResult Reject(string message) => new CommandResult(CommandOutcome.Rejected, message);
    }
}
