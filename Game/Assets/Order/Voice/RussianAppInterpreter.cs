using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Order.Contracts;

namespace Order.Voice
{
    public readonly struct Interpretation
    {
        public readonly bool HasCommand;
        public readonly AppCommand Command;
        public readonly string Message;
        public Interpretation(AppCommand command) { HasCommand = true; Command = command; Message = ""; }
        public Interpretation(string clarification) { HasCommand = false; Command = default; Message = clarification; }
    }

    // A bounded first grammar, independent of the eventual local speech-to-text adapter.
    public sealed class RussianAppInterpreter
    {
        public Interpretation Interpret(string transcript)
        {
            var text = Regex.Replace((transcript ?? "").Trim().ToLowerInvariant().Replace('ё', 'е'), @"\s+", " ").TrimEnd('.', '!', '?');
            if (text.Length == 0) return new Interpretation("Введите команду.");
            if (Regex.IsMatch(text, @"^(открой|открыть|перейди в) настройки звука$"))
                return new Interpretation(AppCommand.Navigate(AppScreen.AudioSettings));
            if (Regex.IsMatch(text, @"^(открой|открыть|перейди в|вернись в) главное меню$"))
                return new Interpretation(AppCommand.Navigate(AppScreen.MainMenu));

            var volume = Regex.Match(text,
                @"^(?:(?:открой|перейди в) настройки звука и )?(?:установи|поставь|убавь|уменьши|увеличь|выставь) (?:общую )?громкость (?:до|на) (\d+(?:[.,]\d+)?|пятьдесят|пятидесяти|ноль|нуля|сто|ста)(?:\s*%| процентов| процента)?$");
            if (volume.Success)
            {
                // Relative changes need an explicit separate intent; do not treat "на" as an absolute value.
                if (Regex.IsMatch(text, @"громкость на ")) return new Interpretation("Укажите итоговую громкость: например, «громкость до 50 процентов».");
                var number = volume.Groups[1].Value;
                float value;
                switch (number)
                {
                    case "пятьдесят": case "пятидесяти": value = 50; break;
                    case "ноль": case "нуля": value = 0; break;
                    case "сто": case "ста": value = 100; break;
                    default:
                        if (!float.TryParse(number.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
                            return new Interpretation("Не удалось разобрать громкость.");
                        break;
                }
                return new Interpretation(AppCommand.SetVolume(value / 100f, text.Contains("настройки звука")));
            }

            var match = Regex.Match(text, @"^(?:запускай|запусти|подготовь|начни) матч против бота(?: (?:(?:фракции|фракция) (1|2|один|два|первой|второй)|(первой|второй) фракции))?$");
            if (match.Success)
            {
                if (!match.Groups[1].Success && !match.Groups[2].Success) return new Interpretation("Против какой фракции: первой или второй?");
                var id = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                return new Interpretation(AppCommand.PrepareMatch(id == "1" || id == "один" || id == "первой" ? FactionId.Expedition : FactionId.Resonance));
            }
            return new Interpretation("Команда не распознана. Уточните действие или используйте кнопки меню.");
        }
    }
}
