using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Order.Contracts;

namespace Order.Voice
{
    // One personal phrase: the player's wording (numbers replaced by {n}) and the menu command it stands for.
    [Serializable]
    public sealed class PhraseEntry
    {
        public string Phrase;
        public string Command;
        // Candidates are shown in the editor but not used until learned.
        public bool Learned;
        public int Agreements;
        public int Uses;
        public string Updated;
    }

    // Persistence of the phrasebook; the host decides where (local profile folder, Steam Cloud).
    public interface IPhrasebookStore
    {
        string Location { get; }
        string ImportFolder { get; }
        List<PhraseEntry> Load();
        void Save(IReadOnlyList<PhraseEntry> entries);
        string Export(IReadOnlyList<PhraseEntry> entries);
        // Reads pending import files and moves them aside so they are not imported twice.
        List<PhraseEntry> TakeImports(out int files);
    }

    // Personal phrasebook. Model-understood phrases become candidates; a candidate is learned when the model
    // understands it the same way again, or when the player confirms it. Learned phrases then resolve without
    // the model, but always through the deterministic menu grammar, so validation is unchanged.
    public sealed class Phrasebook
    {
        public const string Slot = "{n}";
        public const int AgreementsToLearn = 2;
        public const int MaxEntries = 500;
        public const int MaxLength = 120;
        private const RegexOptions Mode = RegexOptions.CultureInvariant;
        private readonly IPhrasebookStore store;
        private readonly List<PhraseEntry> entries;
        public IReadOnlyList<PhraseEntry> Entries => entries;
        public string Location => store.Location;
        public string ImportFolder => store.ImportFolder;
        // Last persistence failure, shown by the editor; null when the file is in sync.
        public string SaveError { get; private set; }
        public event Action Changed;
        public event Action<PhraseEntry> Learned;

        public Phrasebook(IPhrasebookStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            List<PhraseEntry> loaded;
            try { loaded = store.Load() ?? new List<PhraseEntry>(); }
            catch (Exception) { loaded = new List<PhraseEntry>(); }
            entries = new List<PhraseEntry>();
            foreach (var entry in loaded)
            {
                if (entry == null || !Fits(entry.Phrase) || !Fits(entry.Command) || Find(entry.Phrase) != null || entries.Count >= MaxEntries) continue;
                entries.Add(entry);
            }
        }

        // Normalized phrase with its single number replaced by {n}.
        public static string Template(string text, out double? number)
        {
            number = null;
            var normalized = Regex.Replace(MenuCommandGrammar.Normalize(text), @"(\d)%", "$1 %", Mode);
            var tokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var groups = new List<(int start, int end)>();
            for (var i = 0; i < tokens.Length; i++)
            {
                if (!IsNumber(tokens[i])) continue;
                var end = i;
                while (end + 1 < tokens.Length && IsNumber(tokens[end + 1]) && !Regex.IsMatch(tokens[end + 1], @"^\d", Mode)) end++;
                groups.Add((i, end));
                i = end;
            }
            if (groups.Count != 1) return string.Join(" ", tokens);
            var (from, to) = groups[0];
            if (!IntentGrounding.TryNumber(string.Join(" ", tokens, from, to - from + 1), out var value)) return string.Join(" ", tokens);
            number = value;
            var result = tokens.Take(from).Concat(new[] { Slot }).Concat(tokens.Skip(to + 1));
            return string.Join(" ", result);
        }

        public PhraseEntry Find(string template) => entries.FirstOrDefault(e => e.Phrase == template);

        public bool TryResolve(string text, AppCommandState state, out Interpretation result, out PhraseEntry entry)
        {
            result = default;
            entry = Find(Template(text, out var number));
            if (entry == null || !entry.Learned) return false;
            var command = Fill(entry.Command, number);
            if (command == null || !MenuCommandGrammar.TryParse(command, state, out var parsed) || !parsed.HasCommand) return false;
            entry.Uses++;
            Touch(entry);
            result = parsed;
            return true;
        }

        // Records a phrase the model understood. Returns the entry, or null when the meaning cannot be generalized safely.
        public PhraseEntry Observe(string text, IntentDecision decision)
        {
            var phrase = Template(text, out var number);
            if (!Fits(phrase)) return null;
            var command = Canonical(decision, number);
            if (command == null) return null;
            var entry = Find(phrase);
            if (entry != null && entry.Learned) return null;
            if (entry == null)
            {
                if (entries.Count >= MaxEntries) return null;
                entry = new PhraseEntry { Phrase = phrase, Command = command };
                entries.Add(entry);
            }
            if (entry.Command == command) entry.Agreements++;
            else { entry.Command = command; entry.Agreements = 1; }
            var learnedNow = entry.Agreements >= AgreementsToLearn;
            if (learnedNow) entry.Learned = true;
            Touch(entry);
            if (learnedNow) Learned?.Invoke(entry);
            return entry;
        }

        // Explicit teaching: "phrase" means "command". Returns an error, or null on success.
        public string Teach(string phraseText, string commandText, AppCommandState state, out PhraseEntry entry)
        {
            entry = null;
            var error = Pair(phraseText, commandText, state, out var phrase, out var command);
            if (error != null) return error;
            entry = Find(phrase);
            if (entry == null)
            {
                if (entries.Count >= MaxEntries) return $"Словарь заполнен: не больше {MaxEntries} фраз.";
                entry = new PhraseEntry { Phrase = phrase };
                entries.Add(entry);
            }
            entry.Command = command; entry.Learned = true;
            Touch(entry);
            return null;
        }

        public string Edit(PhraseEntry entry, string phraseText, string commandText, AppCommandState state)
        {
            if (entry == null || !entries.Contains(entry)) return "Фразы уже нет в словаре.";
            var error = Pair(phraseText, commandText, state, out var phrase, out var command);
            if (error != null) return error;
            var other = Find(phrase);
            if (other != null && other != entry) return $"Фраза «{phrase}» уже есть в словаре.";
            entry.Phrase = phrase; entry.Command = command; entry.Learned = true;
            Touch(entry);
            return null;
        }

        public void Confirm(PhraseEntry entry)
        {
            if (entry == null || entry.Learned || !entries.Contains(entry)) return;
            entry.Learned = true;
            Touch(entry);
            Learned?.Invoke(entry);
        }

        public bool Forget(PhraseEntry entry)
        {
            if (entry == null || !entries.Remove(entry)) return false;
            Save();
            return true;
        }

        public void Clear()
        {
            entries.Clear();
            Save();
        }

        public string Export() => store.Export(entries.Where(e => e.Learned).ToList());

        public string Import(AppCommandState state)
        {
            var imported = store.TakeImports(out var files);
            if (files == 0) return "В папке импорта нет файлов словаря: " + store.ImportFolder;
            int added = 0, existing = 0, invalid = 0;
            foreach (var item in imported)
            {
                if (item == null || Pair(item.Phrase, item.Command, state, out var phrase, out var command) != null) { invalid++; continue; }
                if (Find(phrase) != null) { existing++; continue; }
                if (entries.Count >= MaxEntries) { invalid++; continue; }
                entries.Add(new PhraseEntry { Phrase = phrase, Command = command, Learned = true, Updated = Now() });
                added++;
            }
            Save();
            var skipped = existing + invalid == 0 ? "" : $" Пропущено: {existing} уже есть, {invalid} не распознаётся.";
            return $"Импортировано фраз: {added} из файлов: {files}.{skipped}";
        }

        // Whether the menu grammar understands the command (with a sample number in place of {n}).
        public static string Check(string command, AppCommandState state)
        {
            if (!Fits(command)) return "Команда пустая или слишком длинная.";
            var sample = command.Replace(Slot, "50");
            if (!MenuCommandGrammar.TryParse(sample, state, out var parsed) || !parsed.HasCommand)
                return $"Ассистент не понимает команду «{command}». Укажите команду, которая работает в консоли, например «громкость {Slot}».";
            return null;
        }

        // Command text equivalent to a model decision, with {n} when the meaning uses the phrase's number.
        public static string Canonical(IntentDecision decision, double? number)
        {
            if (decision == null) return null;
            string Amount(double value)
            {
                if (!number.HasValue) return Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture);
                return Math.Abs(Math.Abs(value) - number.Value) < .0001 ? Slot : null;
            }
            string command;
            switch (decision.Action)
            {
                case "set_volume":
                    if (decision.OpenAudio || !decision.Value.HasValue) return null;
                    var target = Amount(decision.Value.Value);
                    command = target == null ? null : "громкость " + target;
                    break;
                case "adjust_volume":
                    if (decision.OpenAudio || !decision.Value.HasValue || decision.Value.Value == 0) return null;
                    var step = Amount(decision.Value.Value);
                    command = step == null ? null : (decision.Value.Value < 0 ? "убавь громкость на " : "прибавь громкость на ") + step;
                    break;
                case "navigate":
                    command = decision.Screen == "main_menu" ? "штаб" : decision.Screen == "audio_settings" ? "открой настройки звука"
                        : decision.Screen == "match_setup" ? "открой схватку" : null;
                    break;
                case "prepare_match":
                    command = decision.Faction == 1 ? "противник первая фракция" : decision.Faction == 2 ? "противник вторая фракция" : null;
                    break;
                case "assistant_faction":
                    command = decision.Faction == 1 ? "образ ассистента первая фракция" : decision.Faction == 2 ? "образ ассистента вторая фракция" : null;
                    break;
                default: return null;
            }
            // A number in the phrase that the meaning ignores must not be generalized.
            if (command == null || (number.HasValue && !command.Contains(Slot))) return null;
            return command;
        }

        private string Pair(string phraseText, string commandText, AppCommandState state, out string phrase, out string command)
        {
            phrase = Template(phraseText, out var phraseNumber);
            command = MenuCommandGrammar.Normalize(commandText);
            if (!Fits(phrase)) return "Фраза пустая или слишком длинная.";
            if (phrase.Contains(Slot) && !command.Contains(Slot))
            {
                var templated = Template(commandText, out var commandNumber);
                // A number may become {n} only when the phrase had the same number ("звук на 30" → "громкость 30").
                if (!phraseNumber.HasValue || !templated.Contains(Slot) || commandNumber != phraseNumber)
                    return "В команде должно быть {n} — место для числа из фразы.";
                command = templated;
            }
            else if (!phrase.Contains(Slot) && command.Contains(Slot)) return "В фразе нет числа для {n}.";
            return Check(command, state);
        }

        private static string Fill(string command, double? number)
        {
            if (!command.Contains(Slot)) return command;
            return number.HasValue ? command.Replace(Slot, number.Value.ToString("0.##", CultureInfo.InvariantCulture)) : null;
        }

        private static bool IsNumber(string token) =>
            Regex.IsMatch(token, @"^-?\d+(?:[.,]\d+)?$", Mode) || IntentGrounding.IsNumberWord(token);

        private static bool Fits(string text) => !string.IsNullOrWhiteSpace(text) && text.Length <= MaxLength;
        private static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        private void Touch(PhraseEntry entry)
        {
            entry.Updated = Now();
            Save();
        }

        private void Save()
        {
            try { store.Save(entries); SaveError = null; }
            catch (Exception error) { SaveError = error.Message; }
            Changed?.Invoke();
        }
    }
}
