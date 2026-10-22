using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Order.Contracts;

namespace Order.Voice
{
    // Outermost interpreter: phrasebook management phrases, then learned phrases, then the inner chain
    // (menu grammar, rules, model). Phrases the model understood are recorded as phrasebook candidates.
    public sealed class PhrasebookInterpreter : IAppIntentInterpreter
    {
        private const RegexOptions Mode = RegexOptions.CultureInvariant;
        private static readonly TimeSpan ClearWindow = TimeSpan.FromSeconds(60);
        private readonly IAppIntentInterpreter inner;
        private readonly Phrasebook book;
        private PhraseEntry last;
        private DateTime? clearRequested;

        public PhrasebookInterpreter(IAppIntentInterpreter inner, Phrasebook book)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.book = book ?? throw new ArgumentNullException(nameof(book));
        }

        public async Task<Interpretation> InterpretAsync(string text, AppCommandState state, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var managed = Manage(text ?? "", state);
            if (managed.HasValue) return Timed(managed.Value, watch);
            if (book.TryResolve(text, state, out var resolved, out var entry)) { last = entry; return Timed(resolved, watch); }
            var result = await inner.InterpretAsync(text, state, cancellationToken);
            last = result.HasCommand && result.Decision != null ? book.Observe(text, result.Decision) : null;
            return result;
        }

        private Interpretation? Manage(string text, AppCommandState state)
        {
            var t = MenuCommandGrammar.Normalize(text);
            if (Is(t, @"^(?:не то|это не то|неправильно|неверно|ошибка|забудь (?:это|эту фразу|последнюю фразу))$"))
            {
                if (last == null) return Interpretation.Answer("Нечего забывать: последняя команда не связана с личным словарём.");
                var phrase = last.Phrase;
                book.Forget(last); last = null;
                return Interpretation.Answer($"Забыл фразу «{phrase}». Само действие не отменено — исправьте его командой или в меню.");
            }
            if (Is(t, @"^(?:верно|правильно|да верно|так и есть|запомни это|запомни)$"))
            {
                if (last == null) return Interpretation.Answer("Нечего запоминать: последняя команда была не новой формулировкой.");
                if (last.Learned) return Interpretation.Answer($"Фраза «{last.Phrase}» уже в словаре.");
                book.Confirm(last);
                return Interpretation.Answer($"Запомнил: «{last.Phrase}» → «{last.Command}».");
            }
            var teach = Regex.Match(text.Trim(), @"^запомни\s*:?\s*(.+?)\s+(?:значит|означает|это|—|–|-|=)\s+(.+?)\s*$", Mode | RegexOptions.IgnoreCase);
            if (teach.Success)
            {
                var error = book.Teach(Unquote(teach.Groups[1].Value), Unquote(teach.Groups[2].Value), state, out var taught);
                if (error != null) return new Interpretation(error);
                last = taught;
                return Interpretation.Answer($"Запомнил: «{taught.Phrase}» → «{taught.Command}».");
            }
            if (Is(t, @"^(?:очисти|сбрось|удали|сотри) (?:(?:весь|мой|личн\w*) )?словар\w*$|^забудь все фразы$"))
            {
                clearRequested = DateTime.UtcNow;
                return Interpretation.Answer($"Удалить все фразы словаря ({book.Entries.Count})? Скажите «подтверждаю очистку».");
            }
            if (Is(t, @"^подтверждаю очистку(?: словаря)?$"))
            {
                if (!clearRequested.HasValue || DateTime.UtcNow - clearRequested.Value > ClearWindow)
                    return Interpretation.Answer("Сначала попросите очистить словарь.");
                clearRequested = null; last = null;
                book.Clear();
                return Interpretation.Answer("Личный словарь очищен.");
            }
            try
            {
                if (Is(t, @"^(?:экспортируй|выгрузи|сохрани|экспорт) (?:(?:мой|личн\w*) )?словар\w*$"))
                    return Interpretation.Answer("Словарь выгружен: " + book.Export());
                if (Is(t, @"^(?:импортируй|загрузи|импорт) (?:(?:мой|личн\w*) )?словар\w*$"))
                    return Interpretation.Answer(book.Import(state));
            }
            catch (Exception error) { return new Interpretation("Не удалось обменяться словарём: " + error.Message); }
            var forget = Regex.Match(t, @"^забудь (?:фразу )?(.+)$", Mode);
            if (forget.Success)
            {
                var entry = book.Find(Phrasebook.Template(forget.Groups[1].Value, out _));
                if (entry == null) return Interpretation.Answer("Такой фразы нет в словаре.");
                book.Forget(entry);
                if (last == entry) last = null;
                return Interpretation.Answer($"Забыл фразу «{entry.Phrase}».");
            }
            return null;
        }

        private static Interpretation Timed(Interpretation result, Stopwatch watch)
        {
            var elapsed = watch.Elapsed.TotalMilliseconds;
            return result.WithTiming(new IntentTiming { Route = "phrasebook", GroundingMs = elapsed, TotalMs = elapsed });
        }

        private static string Unquote(string text) => text.Trim().Trim('«', '»', '"', '„', '“', '”', '\'', ':').Trim();
        private static bool Is(string text, string pattern) => Regex.IsMatch(text, pattern, Mode);
    }
}
