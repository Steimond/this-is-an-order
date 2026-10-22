using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Order.Content;
using Order.Contracts;

namespace Order.Voice
{
    // Deterministic text commands for every menu action and option from MenuCatalog.
    // Returns false for anything it does not own, so the existing rules and model keep handling it.
    public static class MenuCommandGrammar
    {
        public const string Help = "Управляю всем меню. Примеры: «открой графику», «тени высокие», «громкость 40», «назначь разговор на B», " +
            "«полигон», «назад», «выход». Узнать значение: «какие тени?». Научить: «запомни „тише“ значит „убавь громкость на 5“».";
        private const RegexOptions Mode = RegexOptions.CultureInvariant;
        private const string Settings = @"\b(?:настройк\w*|параметр\w*|опци\w*|вкладк\w*)\b";
        private const string OpenVerb = @"\b(?:открой|открыть|перейди|перейти|покажи|показать|зайди|иди|вернись|вернуться|верни\w*|выйди|выйти|переключись|давай)\b";
        private const string MainScreen = @"\b(?:штаб\w*|главн\w* (?:меню|экран\w*)|основн\w* меню|домой|меню)\b";
        private const string Skirmish = @"\b(?:схватк\w*|(?:матч\w*|бо[йя]|игр[уаы]) (?:против|с) (?:бот\w*|ии|компьютер\w*)|против (?:бот\w*|ии))\b";
        private const string Faction = @"\b(перв\w*|втор\w*|[12])\b";
        // "Another one": with two variants this is the other, with more — the next available.
        private const string Another = @"\b(?:друг\w*|ин(?:ой|ого|ую|ое|ая|ые|ым|ом)|смени\w*|сменить|поменяй|поменять|переключи\w*|следующ\w*)\b";
        private const string Verbs = @"\b(?:открой|покажи|поставь|выставь|установи|включи|выключи|отключи|сделай|убавь|добавь|увеличь|уменьши|снизь|выбери|назначь|сбрось|удали|запусти|смени|поменяй)\b";
        private static readonly Regex Filler = new Regex(@"\b(?:пожалуйста|please)\b|^(?:эй\s*,?\s*)?(?:ассистент|адъютант)\s*,\s*", Mode);
        private static readonly Dictionary<string, string> NamedKeys = new Dictionary<string, string>
        {
            ["пробел"] = "ПРОБЕЛ", ["space"] = "ПРОБЕЛ", ["enter"] = "ENTER", ["ввод"] = "ENTER", ["энтер"] = "ENTER", ["tab"] = "TAB", ["таб"] = "TAB",
            ["esc"] = "ESC", ["escape"] = "ESC", ["эскейп"] = "ESC", ["shift"] = "L-SHIFT", ["шифт"] = "L-SHIFT", ["ctrl"] = "L-CTRL", ["контрол"] = "L-CTRL",
            ["alt"] = "L-ALT", ["альт"] = "L-ALT", ["backspace"] = "BACKSPACE", ["бэкспейс"] = "BACKSPACE",
        };

        public static bool TryParse(string text, AppCommandState state, out Interpretation result)
        {
            result = default;
            var original = (text ?? "").Trim();
            var t = Normalize(original);
            if (t.Length == 0) return false;

            if (state.ExitPending)
            {
                if (Is(t, @"^(?:да|ага|подтверждаю|подтвердить|выйти|выход|выходим|выхожу|да,? выйти|точно|ок)$")) return Done(AppCommand.ConfirmExit(), out result);
                if (Is(t, @"^(?:нет|отмена|отмени|остаться|остаюсь|не выходить|не надо|назад)$")) return Done(AppCommand.CancelExit(), out result);
            }
            if (Is(t, @"^(?:помощь|справка|help|команды|список команд|что ты (?:умеешь|можешь)|что умеешь|что можно сделать)$"))
                return Reply(Help, out result);
            if (Unsupported(t)) return false;

            var question = original.EndsWith("?", StringComparison.Ordinal) || Is(t, @"^(?:как(?:ой|ая|ие|ое)|сколько|что с)\b");
            if (question)
            {
                var asked = FindOptions(t, state);
                if (asked.Count == 0) return false;
                return Reply(Describe(asked[0].Option, state), out result);
            }

            if (Is(t, @"^(?:выход|выйти|выйди)$") || (Is(t, @"\b(?:выйди|выйти|выход\w*|закрой|закрыть|заверши\w*|покинь|покинуть)\b")
                && Is(t, @"\b(?:из игры|игру|из приложения|приложение|сеанс)\b")))
                return Done(AppCommand.RequestExit(), out result);
            if (Is(t, @"^(?:назад|вернись|вернуться|закрой|закрыть|закрой окно|закрой настройки|закрыть настройки|выйди из настроек|выйти из настроек|готово|отмена|отмени)$"))
                return Done(AppCommand.Back(), out result);

            if (Is(t, @"\b(?:сбрось|сбросить|верни|вернуть|восстанови|восстановить)\b") && Is(t, Settings + @"|\bумолчани\w*"))
            {
                var options = FindOptions(t, state);
                if (!Is(t, Settings) && options.Count > 0) return Reset(options[0].Option, out result);
                if (Is(t, @"\b(?:все|всё|всех)\b")) return Done(AppCommand.ResetAllOptions(), out result);
                var tab = FindTab(t);
                if (tab.HasValue) return Done(AppCommand.ResetOptions(tab.Value), out result);
                if (state.Screen.IsSettings()) return Done(AppCommand.ResetOptions(state.Screen), out result);
                return Clarify("Какую вкладку настроек сбросить? Например: «сбрось настройки графики».", out result);
            }

            if (Is(t, @"\b(?:полигон\w*|тестов\w* карт\w*|temple basin|темпл бейсин)\b")) return Done(AppCommand.LaunchTestMap(), out result);
            if (Is(t, @"^(?:начни|начать|начинай|стартуй|старт)(?: матч| бой| игру)?$|^в бой$")) return Done(AppCommand.StartMatch(), out result);
            if (Is(t, @"\b(?:кампани\w*|сюжетн\w*)\b")) return Reply("Кампания появится позже. Сейчас доступны схватка с ИИ и полигон.", out result);
            if (Is(t, @"\b(?:сетев\w* игр\w*|мультиплеер\w*|по сети|онлайн|дуэл\w*)\b")) return Reply("Сетевая игра появится позже. Сейчас доступны схватка с ИИ и полигон.", out result);
            if (Is(t, @"\b(?:архив\w*|справочник\w*|обучени\w*)\b")) return Reply("Архив и обучение появятся позже.", out result);

            var faction = Regex.Match(t, Faction, Mode);
            if (Is(t, @"\b(?:противник\w*|враг\w*|соперник\w*)\b") || (Is(t, @"\b(?:матч\w*|бо[йя]|игр\w*|сыгра\w*|схватк\w*)\b") && Is(t, @"\b(?:бот\w*|ии|компьютер\w*)\b") && faction.Success))
            {
                if (faction.Success)
                    return Done(AppCommand.PrepareMatch(faction.Value.StartsWith("перв", StringComparison.Ordinal) || faction.Value == "1" ? FactionId.Expedition : FactionId.Resonance), out result);
                if (Is(t, Another)) return Done(AppCommand.PrepareMatch(state.EnemyFaction == FactionId.Expedition ? FactionId.Resonance : FactionId.Expedition), out result);
                return Clarify("Уточните фракцию противника: первая или вторая.", out result);
            }

            var found = FindOptions(t, state);
            if (Is(t, Settings))
            {
                var tab = FindTab(t);
                return Done(tab.HasValue ? AppCommand.Navigate(tab.Value) : AppCommand.OpenSettings(), out result);
            }
            if (found.Count == 0)
            {
                // Without a verb only a bare screen or tab name navigates ("штаб", "графика"), not "накинь звука".
                var verb = Is(t, OpenVerb);
                if (verb ? Is(t, MainScreen) : Is(t, "^" + MainScreen + "$")) return Done(AppCommand.Navigate(AppScreen.MainMenu), out result);
                if (verb ? Is(t, Skirmish) : Is(t, "^" + Skirmish + "$")) return Done(AppCommand.Navigate(AppScreen.MatchSetup), out result);
                var tab = FindTab(t, !verb);
                if (tab.HasValue) return Done(AppCommand.Navigate(tab.Value), out result);
            }

            if (found.Count == 0) return false;
            // "Open audio and set volume 26" is compound: the general interpreter handles open + change together.
            if (Is(t, OpenVerb) && Is(t, @"\bи\b")) return false;
            foreach (var candidate in found)
            {
                var resolved = Resolve(candidate.Option, candidate.Rest, state);
                if (resolved.HasValue) { result = resolved.Value; return true; }
            }
            return Clarify(Hint(found[0].Option), out result);
        }

        private static Interpretation? Resolve(MenuOption option, string rest, AppCommandState state)
        {
            var current = MenuCatalog.Value(state, option);
            var bare = rest.Trim().Length == 0;
            var on = Is(rest, @"\b(?:включи\w*|вкл|активируй\w*|покажи|показывай|показывать|отображай\w*|да|on)\b");
            var off = Is(rest, @"\b(?:выключи\w*|отключи\w*|выкл|убери|скрой|спрячь|деактивируй\w*|нет|off)\b");
            switch (option.Kind)
            {
                case OptionKind.Toggle:
                    if (on != off) return Set(option, on ? 1 : 0);
                    if (Is(rest, @"\bпереключи\w*\b")) return Set(option, current >= .5f ? 0 : 1);
                    return bare ? Interpretation.Answer(Describe(option, state)) : (Interpretation?)null;
                case OptionKind.Range:
                    var up = Is(rest, @"\b(?:увелич\w*|прибав\w*|добав\w*|повыс\w*|подним\w*|выше|больше|погромче)\b");
                    var down = Is(rest, @"\b(?:уменьш\w*|убав\w*|сниз\w*|пониз\w*|опусти\w*|ниже|меньше|потише)\b");
                    if (IntentGrounding.TryNumber(rest, out var number))
                    {
                        var relative = (up || down) && !Is(rest, @"\bдо\b");
                        var target = relative ? current + (down ? -number : number) : number;
                        if (relative) target = Math.Max(option.Min, Math.Min(option.Max, target));
                        if (target < option.Min || target > option.Max) return new Interpretation($"«{option.Title}»: значение от {option.Min:0} до {option.Max:0}%.");
                        return Set(option, (float)Math.Round(target));
                    }
                    if (up != down) return Set(option, Math.Max(option.Min, Math.Min(option.Max, current + (down ? -10 : 10))));
                    if (off && !on) return Set(option, option.Min);
                    if (on && !off) return Set(option, current > option.Min ? current : option.Default);
                    return bare ? Interpretation.Answer(Describe(option, state)) : (Interpretation?)null;
                case OptionKind.Choice:
                    var best = -1; var bestLength = 0;
                    for (var i = 0; i < option.Choices.Count; i++)
                    {
                        var pattern = option.ChoiceAliases != null ? option.ChoiceAliases[i] : Regex.Escape(Normalize(option.Choices[i]));
                        var match = Regex.Match(rest, @"(?<![\w])(?:" + pattern + ")", Mode);
                        if (match.Success && match.Length > bestLength) { best = i; bestLength = match.Length; }
                    }
                    if (best >= 0) return Set(option, best);
                    var back = Is(rest, @"\bпредыдущ\w*\b");
                    if ((back || Is(rest, Another)) && option.Choices.Count > 1) return Set(option, Other(option, (int)current, back ? -1 : 1));
                    return bare ? Interpretation.Answer(Describe(option, state)) : (Interpretation?)null;
                case OptionKind.Key:
                    var words = Regex.Replace(rest, @"\b(?:на|клавиш\w*|кнопк\w*|назначь\w*|поставь|установи\w*|смени\w*|измени\w*|поменяй|сделай|для)\b", " ", Mode).Trim();
                    if (words.Length == 0) return Interpretation.Answer(Describe(option, state));
                    var key = KeyName(words);
                    return key == null ? new Interpretation("Назовите клавишу латиницей или словом: например, «назначь разговор на B» или «на пробел».")
                        : new Interpretation(AppCommand.SetKey(option.Id, key));
                default: return null;
            }
        }

        // Next variant in the given direction, skipping locked ones; if all others are locked, the controller explains why.
        private static int Other(MenuOption option, int current, int step)
        {
            var count = option.Choices.Count;
            for (var offset = 1; offset < count; offset++)
            {
                var candidate = ((current + step * offset) % count + count) % count;
                if (!option.Locked.Contains(candidate)) return candidate;
            }
            return ((current + step) % count + count) % count;
        }

        private static string KeyName(string words)
        {
            if (words.Contains(" ")) return null;
            if (NamedKeys.TryGetValue(words, out var named)) return named;
            if (Regex.IsMatch(words, @"^(?:[a-z0-9]|f(?:[1-9]|1[0-2]))$", Mode)) return words.ToUpperInvariant();
            return null;
        }

        private struct Candidate { public MenuOption Option; public string Rest; public int Length; public bool OnScreen; public int Order; }

        // Options mentioned in the text: longest alias first, then options of the open tab.
        private static List<Candidate> FindOptions(string text, AppCommandState state)
        {
            var found = new List<Candidate>();
            for (var i = 0; i < MenuCatalog.Options.Count; i++)
            {
                var option = MenuCatalog.Options[i];
                var match = Regex.Match(text, @"(?<![\w])(?:" + option.Aliases + @")(?![\w])", Mode);
                if (!match.Success) continue;
                found.Add(new Candidate { Option = option, Rest = text.Remove(match.Index, match.Length), Length = match.Length,
                    OnScreen = option.Tab == state.Screen || option.AlsoOn == state.Screen, Order = i });
            }
            found.Sort((a, b) => a.Length != b.Length ? b.Length.CompareTo(a.Length) : a.OnScreen != b.OnScreen ? b.OnScreen.CompareTo(a.OnScreen) : a.Order.CompareTo(b.Order));
            return found;
        }

        private static AppScreen? FindTab(string text, bool whole = false)
        {
            foreach (var tab in MenuCatalog.Tabs)
                if (Is(text, whole ? "^(?:" + tab.Aliases + ")$" : @"(?<![\w])(?:" + tab.Aliases + @")(?![\w])")) return tab.Screen;
            return null;
        }

        private static bool Reset(MenuOption option, out Interpretation result)
        {
            result = option.Kind == OptionKind.Key ? new Interpretation(AppCommand.SetKey(option.Id, option.DefaultKey))
                : new Interpretation(AppCommand.SetOption(option.Id, option.Default));
            return true;
        }

        private static string Describe(MenuOption option, AppCommandState state)
        {
            var value = option.Kind == OptionKind.Key ? MenuCatalog.Key(state.Keys, option) : MenuCatalog.Format(option, MenuCatalog.Value(state, option));
            return $"{option.Title}: {value}." + (option.Live ? "" : " Пока не применяется — система ещё в разработке.");
        }

        private static string Hint(MenuOption option)
        {
            switch (option.Kind)
            {
                case OptionKind.Toggle: return $"«{option.Title}»: включить или выключить?";
                case OptionKind.Range: return $"«{option.Title}»: назовите значение от {option.Min:0} до {option.Max:0}%.";
                case OptionKind.Choice: return $"«{option.Title}»: варианты — {string.Join(", ", option.Choices)}.";
                default: return $"«{option.Title}»: назовите клавишу.";
            }
        }

        // Negations, hypotheticals, destructive requests and compound commands stay with the general interpreter.
        private static bool Unsupported(string text) =>
            Is(text, @"\bне\s+\w+|\bесли\s+бы\b|\b(?:удали\w*|сотри\w*)\b") || Regex.Matches(text, Verbs, Mode).Count > 1;

        private static Interpretation Set(MenuOption option, float value) => new Interpretation(AppCommand.SetOption(option.Id, value));
        private static bool Done(AppCommand command, out Interpretation result) { result = new Interpretation(command); return true; }
        private static bool Reply(string message, out Interpretation result) { result = Interpretation.Answer(message); return true; }
        private static bool Clarify(string message, out Interpretation result) { result = new Interpretation(message); return true; }
        private static bool Is(string text, string pattern) => Regex.IsMatch(text, pattern, Mode);

        internal static string Normalize(string text)
        {
            var normalized = (text ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');
            normalized = Filler.Replace(normalized, " ");
            normalized = Regex.Replace(normalized, @"[.!?,;:«»""]+", " ", Mode);
            return Regex.Replace(normalized, @"\s+", " ", Mode).Trim();
        }
    }
}
