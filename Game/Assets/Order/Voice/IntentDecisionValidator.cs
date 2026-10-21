using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Order.Contracts;

namespace Order.Voice
{
    public sealed class IntentDecision
    {
        public string Action;
        public double? Value;
        public string Screen;
        public int? Faction;
        public bool OpenAudio;
    }

    public static class IntentDecisionValidator
    {
        public static Interpretation Validate(IntentDecision intent, string transcript, AppCommandState context)
        {
            if (intent == null) return Clarify();
            switch (intent.Action)
            {
                case "set_volume":
                case "adjust_volume":
                    if (!intent.Value.HasValue || intent.Faction.HasValue || intent.Screen != "none") return Clarify();
                    var value = intent.Value.Value;
                    if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 100) return Clarify();
                    if (intent.Action == "set_volume" && value < 0) return Clarify();
                    // Explicit percentages must survive model interpretation unchanged.
                    var number = Regex.Match(transcript ?? "", @"(?<![\d.,])(-?\d+(?:[.,]\d+)?)\s*(?:%|процент)", RegexOptions.IgnoreCase);
                    if (number.Success && double.TryParse(number.Groups[1].Value.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var explicitValue) && Math.Abs(Math.Abs(explicitValue) - Math.Abs(value)) > 0.0001) return Clarify();
                    var target = intent.Action == "adjust_volume" ? Math.Max(0, Math.Min(100, context.MasterVolume * 100 + value)) : value;
                    return new Interpretation(AppCommand.SetVolume((float)(target / 100), intent.OpenAudio));
                case "navigate":
                    if (intent.Value.HasValue || intent.Faction.HasValue || intent.OpenAudio) return Clarify();
                    switch (intent.Screen)
                    {
                        case "main_menu": return new Interpretation(AppCommand.Navigate(AppScreen.MainMenu));
                        case "audio_settings": return new Interpretation(AppCommand.Navigate(AppScreen.AudioSettings));
                        case "match_setup": return new Interpretation(AppCommand.Navigate(AppScreen.MatchSetup));
                        default: return Clarify();
                    }
                case "prepare_match":
                case "assistant_faction":
                    if (intent.Value.HasValue || intent.Screen != "none" || intent.OpenAudio) return Clarify();
                    if (intent.Faction != 1 && intent.Faction != 2)
                        return new Interpretation("Уточните фракцию: первая или вторая.");
                    var faction = intent.Faction == 1 ? FactionId.Expedition : FactionId.Resonance;
                    return new Interpretation(intent.Action == "prepare_match" ? AppCommand.PrepareMatch(faction) : AppCommand.SetAssistant(faction));
                default: return Clarify();
            }
        }

        private static Interpretation Clarify() => new Interpretation("Уточните команду: что изменить или открыть?");
    }
}
