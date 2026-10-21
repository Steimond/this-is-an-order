using System;
using System.Collections.Generic;

namespace Order.Contracts
{
    // Settings screens are tabs of one options window; new values are appended to keep stored ids stable.
    public enum AppScreen { MainMenu, AudioSettings, MatchSetup, GameSettings, ControlsSettings, GraphicsSettings, VoiceSettings, InterfaceSettings }
    public enum FactionId { Expedition, Resonance }
    public enum AppCommandKind
    {
        Navigate, SetMasterVolume, PrepareBotMatch, SetAssistantFaction,
        Back, OpenSettings, SetOption, SetKey, ResetOptions, LaunchTestMap, RequestExit, ConfirmExit, CancelExit, StartMatch
    }
    public enum CommandSource { Manual, Text, Voice }
    public enum CommandOutcome { Applied, Rejected, NeedsClarification, StaleContext, Unavailable, Answered }
    // Lifecycle actions the application layer asks its host to perform.
    public enum AppRequest { LaunchTestMap, Quit }

    public static class AppScreens
    {
        public static bool IsSettings(this AppScreen screen) => screen == AppScreen.AudioSettings || screen >= AppScreen.GameSettings;
    }

    public readonly struct CommandContext
    {
        public readonly long Session;
        public readonly CommandSource Source;
        public readonly long Revision;
        public CommandContext(long session, CommandSource source, long revision = -1)
        { Session = session; Source = source; Revision = revision; }
    }

    public readonly struct AppCommandState
    {
        private static readonly IReadOnlyDictionary<string, float> NoOptions = new Dictionary<string, float>();
        private static readonly IReadOnlyDictionary<string, string> NoKeys = new Dictionary<string, string>();
        public readonly long Session;
        public readonly long Revision;
        public readonly AppScreen Screen;
        public readonly float MasterVolume;
        public readonly FactionId AssistantFaction;
        public readonly FactionId EnemyFaction;
        public readonly bool ExitPending;
        private readonly IReadOnlyDictionary<string, float> options;
        private readonly IReadOnlyDictionary<string, string> keys;
        public IReadOnlyDictionary<string, float> Options => options ?? NoOptions;
        public IReadOnlyDictionary<string, string> Keys => keys ?? NoKeys;
        public AppCommandState(long session, long revision, AppScreen screen, float masterVolume, FactionId assistantFaction, FactionId enemyFaction,
            bool exitPending = false, IReadOnlyDictionary<string, float> options = null, IReadOnlyDictionary<string, string> keys = null)
        {
            Session = session; Revision = revision; Screen = screen; MasterVolume = masterVolume; AssistantFaction = assistantFaction; EnemyFaction = enemyFaction;
            ExitPending = exitPending; this.options = options; this.keys = keys;
        }
    }

    public readonly struct AppCommand
    {
        public readonly AppCommandKind Kind;
        public readonly AppScreen Screen;
        public readonly float Volume;
        public readonly FactionId Faction;
        public readonly bool OpenAudioSettings;
        public readonly string Option;
        public readonly float Value;
        public readonly string Key;
        public readonly bool All;

        private AppCommand(AppCommandKind kind, AppScreen screen = default, float volume = 0,
            FactionId faction = default, bool openAudioSettings = false, string option = null, float value = 0, string key = null, bool all = false)
        {
            Kind = kind; Screen = screen; Volume = volume; Faction = faction; OpenAudioSettings = openAudioSettings;
            Option = option; Value = value; Key = key; All = all;
        }

        public static AppCommand Navigate(AppScreen screen) => new AppCommand(AppCommandKind.Navigate, screen: screen);
        public static AppCommand SetVolume(float value, bool openAudioSettings = false) =>
            new AppCommand(AppCommandKind.SetMasterVolume, volume: value, openAudioSettings: openAudioSettings);
        public static AppCommand PrepareMatch(FactionId enemy) => new AppCommand(AppCommandKind.PrepareBotMatch, faction: enemy);
        public static AppCommand SetAssistant(FactionId faction) => new AppCommand(AppCommandKind.SetAssistantFaction, faction: faction);
        public static AppCommand Back() => new AppCommand(AppCommandKind.Back);
        public static AppCommand OpenSettings() => new AppCommand(AppCommandKind.OpenSettings);
        public static AppCommand SetOption(string option, float value) => new AppCommand(AppCommandKind.SetOption, option: option, value: value);
        public static AppCommand SetKey(string option, string key) => new AppCommand(AppCommandKind.SetKey, option: option, key: key);
        public static AppCommand ResetOptions(AppScreen tab) => new AppCommand(AppCommandKind.ResetOptions, screen: tab);
        public static AppCommand ResetAllOptions() => new AppCommand(AppCommandKind.ResetOptions, all: true);
        public static AppCommand LaunchTestMap() => new AppCommand(AppCommandKind.LaunchTestMap);
        public static AppCommand RequestExit() => new AppCommand(AppCommandKind.RequestExit);
        public static AppCommand ConfirmExit() => new AppCommand(AppCommandKind.ConfirmExit);
        public static AppCommand CancelExit() => new AppCommand(AppCommandKind.CancelExit);
        public static AppCommand StartMatch() => new AppCommand(AppCommandKind.StartMatch);
    }

    public readonly struct CommandResult
    {
        public readonly CommandOutcome Outcome;
        public readonly string Message;
        public bool Applied => Outcome == CommandOutcome.Applied;
        public CommandResult(CommandOutcome outcome, string message) { Outcome = outcome; Message = message; }
    }

    public interface IAppCommandSink
    {
        long Session { get; }
        AppCommandState State { get; }
        CommandResult Execute(AppCommand command, CommandContext context);
    }

    public readonly struct SettingsSnapshot
    {
        private static readonly IReadOnlyDictionary<string, float> NoOptions = new Dictionary<string, float>();
        private static readonly IReadOnlyDictionary<string, string> NoKeys = new Dictionary<string, string>();
        public readonly float MasterVolume;
        public readonly FactionId AssistantFaction;
        private readonly IReadOnlyDictionary<string, float> options;
        private readonly IReadOnlyDictionary<string, string> keys;
        // Menu option values by catalog id; missing entries use catalog defaults.
        public IReadOnlyDictionary<string, float> Options => options ?? NoOptions;
        public IReadOnlyDictionary<string, string> Keys => keys ?? NoKeys;
        public SettingsSnapshot(float masterVolume, FactionId assistantFaction,
            IReadOnlyDictionary<string, float> options = null, IReadOnlyDictionary<string, string> keys = null)
        { MasterVolume = masterVolume; AssistantFaction = assistantFaction; this.options = options; this.keys = keys; }
        public static SettingsSnapshot Default => new SettingsSnapshot(1f, FactionId.Expedition);
    }

    public interface ISettingsStore
    {
        SettingsSnapshot Load();
        void Save(SettingsSnapshot settings);
    }
}
