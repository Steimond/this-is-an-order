using System;

namespace Order.Contracts
{
    public enum AppScreen { MainMenu, AudioSettings, MatchSetup }
    public enum FactionId { Expedition, Resonance }
    public enum AppCommandKind { Navigate, SetMasterVolume, PrepareBotMatch, SetAssistantFaction }
    public enum CommandSource { Manual, Text, Voice }
    public enum CommandOutcome { Applied, Rejected, NeedsClarification, StaleContext }

    public readonly struct CommandContext
    {
        public readonly long Session;
        public readonly CommandSource Source;
        public CommandContext(long session, CommandSource source) { Session = session; Source = source; }
    }

    public readonly struct AppCommand
    {
        public readonly AppCommandKind Kind;
        public readonly AppScreen Screen;
        public readonly float Volume;
        public readonly FactionId Faction;
        public readonly bool OpenAudioSettings;

        private AppCommand(AppCommandKind kind, AppScreen screen = default, float volume = 0,
            FactionId faction = default, bool openAudioSettings = false)
        { Kind = kind; Screen = screen; Volume = volume; Faction = faction; OpenAudioSettings = openAudioSettings; }

        public static AppCommand Navigate(AppScreen screen) => new AppCommand(AppCommandKind.Navigate, screen: screen);
        public static AppCommand SetVolume(float value, bool openAudioSettings = false) =>
            new AppCommand(AppCommandKind.SetMasterVolume, volume: value, openAudioSettings: openAudioSettings);
        public static AppCommand PrepareMatch(FactionId enemy) => new AppCommand(AppCommandKind.PrepareBotMatch, faction: enemy);
        public static AppCommand SetAssistant(FactionId faction) => new AppCommand(AppCommandKind.SetAssistantFaction, faction: faction);
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
        CommandResult Execute(AppCommand command, CommandContext context);
    }

    public readonly struct SettingsSnapshot
    {
        public readonly float MasterVolume;
        public readonly FactionId AssistantFaction;
        public SettingsSnapshot(float masterVolume, FactionId assistantFaction)
        { MasterVolume = masterVolume; AssistantFaction = assistantFaction; }
        public static SettingsSnapshot Default => new SettingsSnapshot(1f, FactionId.Expedition);
    }

    public interface ISettingsStore
    {
        SettingsSnapshot Load();
        void Save(SettingsSnapshot settings);
    }
}
