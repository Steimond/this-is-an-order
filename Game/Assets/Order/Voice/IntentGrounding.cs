using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Order.Voice
{
    // Ground observable slots and domain constraints; the model handles other paraphrases.
    public static class IntentGrounding
    {
        private static readonly Dictionary<string, int> Numbers = new Dictionary<string, int>
        {
            ["ноль"]=0,["нуля"]=0,["один"]=1,["одна"]=1,["два"]=2,["две"]=2,["три"]=3,["четыре"]=4,["пять"]=5,
            ["шесть"]=6,["семь"]=7,["восемь"]=8,["девять"]=9,["десять"]=10,["одиннадцать"]=11,["двенадцать"]=12,
            ["тринадцать"]=13,["четырнадцать"]=14,["пятнадцать"]=15,["шестнадцать"]=16,["семнадцать"]=17,["восемнадцать"]=18,["девятнадцать"]=19,
            ["двадцать"]=20,["тридцать"]=30,["сорок"]=40,["пятьдесят"]=50,["пятидесяти"]=50,["шестьдесят"]=60,["семьдесят"]=70,["восемьдесят"]=80,["девяносто"]=90,["сто"]=100,["ста"]=100
        };
        private static string Normalize(string text) => (text ?? "").Trim().ToLowerInvariant().Replace('ё','е').TrimEnd('.', '!', '?');
        public static bool TryDirect(string text, out IntentDecision decision)
        {
            var normalized = Normalize(text);
            decision = null;
            if (Blocked(normalized)) { decision = Empty("clarify"); return true; }
            if (Regex.IsMatch(normalized, @"\b(?:выключи|отключи|убери)\b.*\bзвук\b"))
            { decision = Empty("set_volume"); decision.Value = 0; return true; }
            if (Regex.IsMatch(normalized, @"\bпотише\b|\bпогромче\b"))
            {
                decision = Empty("adjust_volume"); decision.Value = normalized.Contains("потише") ? -10 : 10; return true;
            }
            if (Regex.IsMatch(normalized, @"^(?:общая\s+)?(?:громкость|звук)\s+(?:-?\d+(?:[.,]\d+)?|[а-я]+(?:\s+[а-я]+)?)\s*(?:%|процент(?:а|ов)?)?$") && TryNumber(normalized, out var number))
            { decision = Empty("set_volume"); decision.Value = number; return true; }
            return false;
        }

        public static IntentDecision Ground(IntentDecision proposed, string text)
        {
            if (TryDirect(text, out var direct)) return direct;
            if (proposed == null) return Empty("clarify");
            var normalized = Normalize(text);
            var result = new IntentDecision { Action = proposed.Action, Value = proposed.Value, Screen = proposed.Screen,
                Faction = proposed.Faction, OpenAudio = proposed.OpenAudio };
            if (result.Action == "prepare_match" || result.Action == "assistant_faction")
            {
                // Never accept an invented faction when the player has not selected one in speech.
                var faction = Regex.Match(normalized, @"\b(перв\w*|втор\w*|[12])\b");
                result.Faction = !faction.Success ? (int?)null : faction.Value.StartsWith("перв", StringComparison.Ordinal) || faction.Value == "1" ? 1 : 2;
            }
            if (result.Action == "set_volume" || result.Action == "adjust_volume")
            {
                if (!TryNumber(normalized, out var amount))
                {
                    if (result.Action == "adjust_volume" && result.Value.HasValue && result.Value.Value != 0)
                    { result.Value = Math.Sign(result.Value.Value) * 10; return result; }
                    return Empty("clarify");
                }
                if (amount < 0 || amount > 100) return Empty("clarify");
                if (Regex.IsMatch(normalized, @"\bдо\s+")) result.Action = "set_volume";
                else if (result.Action == "set_volume" && Regex.IsMatch(normalized, @"\b(?:убав\w*|уменьш\w*|сниз\w*|добав\w*|увелич\w*)\b.*\bна\s+")) result.Action = "adjust_volume";
                result.Value = result.Action == "adjust_volume" && Regex.IsMatch(normalized, @"\b(?:убав\w*|уменьш\w*|сниз\w*)\b") ? -amount : amount;
                result.Screen = "none"; result.Faction = null;
            }
            return result;
        }

        private static bool Blocked(string text) => Regex.IsMatch(text,
            @"\b(?:музык\w*|микрофон\w*|эффект\w*|удали\w*|сотри\w*)\b|\bне\s+(?:меня\w*|трог\w*|став\w*|дела\w*|убав\w*|увелич\w*|откры\w*|выключ\w*|отключ\w*|сниз\w*)\b|\bесли\s+бы\b|^сколько\b");

        private static IntentDecision Empty(string action) => new IntentDecision { Action = action, Screen = "none" };

        internal static bool TryNumber(string text, out double number)
        {
            number = 0;
            var digits = Regex.Matches(text, @"(?<![\d.,])-?\d+(?:[.,]\d+)?");
            if (digits.Count == 1) return double.TryParse(digits[0].Value.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
            if (digits.Count > 1) return false;
            var found = false;
            var previous = 0;
            var ended = false;
            foreach (Match word in Regex.Matches(text, @"[а-я]+"))
            {
                if (!Numbers.TryGetValue(word.Value, out var value)) { if (found) ended = true; continue; }
                if (ended) return false;
                if (found && !(previous >= 20 && previous <= 90 && value >= 1 && value <= 9)) return false;
                number += value; previous = value; found = true;
            }
            return found;
        }
    }
}
