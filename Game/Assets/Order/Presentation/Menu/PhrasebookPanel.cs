using System;
using System.IO;
using System.Linq;
using Order.Contracts;
using Order.Voice;
using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Personal phrasebook editor inside the voice settings tab: both columns are editable text,
    // candidates can be confirmed, entries deleted, and the phrasebook exported, imported or cleared.
    public sealed class PhrasebookPanel : IDisposable
    {
        private readonly Phrasebook book;
        private readonly Func<AppCommandState> state;
        private VisualElement root, list;
        private Label summary, status;
        private Label clearText;
        private bool clearArmed, dirty;

        public PhrasebookPanel(Phrasebook book, Func<AppCommandState> state)
        {
            this.book = book; this.state = state;
            book.Changed += OnChanged;
        }

        public VisualElement Build()
        {
            root = Hud.Box("phrasebook");
            root.Add(Hud.Text("ЛИЧНЫЙ СЛОВАРЬ", "hud-section-title"));
            root.Add(Hud.Text("Ваши формулировки и команды, которые они означают; {n} — число из фразы. Новая фраза запоминается, " +
                "когда ассистент второй раз понял её так же, или сразу после «верно». «Не то» забывает последнюю.", "phrasebook__caption"));
            var toolbar = Hud.Box("phrasebook__toolbar");
            toolbar.Add(Hud.Button("ЭКСПОРТ", Export, "hud-button--ghost"));
            toolbar.Add(Hud.Button("ИМПОРТ", Import, "hud-button--ghost"));
            toolbar.Add(Hud.Button("ПАПКА ИМПОРТА", OpenImportFolder, "hud-button--ghost"));
            toolbar.Add(Hud.Box("phrasebook__spacer"));
            var clear = Hud.Button("ОЧИСТИТЬ", Clear, "hud-button--ghost");
            clear.AddToClassList("hud-button--danger");
            clearText = clear.Q<Label>(className: "hud-button__text");
            toolbar.Add(clear);
            root.Add(toolbar);
            summary = Hud.Text("", "phrasebook__summary");
            root.Add(summary);
            status = Hud.Text("", "phrasebook__status");
            root.Add(status);

            var create = Hud.Box("phrasebook__row");
            create.AddToClassList("phrasebook__row--new");
            var phrase = Field("Ваша фраза, например «тише»", "");
            var command = Field("Команда, например «убавь громкость на 5»", "");
            create.Add(phrase);
            create.Add(Hud.Text("→", "phrasebook__arrow"));
            create.Add(command);
            create.Add(Hud.Button("ДОБАВИТЬ", () =>
            {
                if (Report(book.Teach(phrase.value, command.value, state(), out _), "Фраза добавлена.")) { phrase.value = ""; command.value = ""; }
            }, "hud-button--ghost"));
            root.Add(create);
            list = Hud.Box("phrasebook__list");
            root.Add(list);
            Fill();
            return root;
        }

        public void Dispose() => book.Changed -= OnChanged;

        private void Fill()
        {
            if (list == null) return;
            // Do not rebuild under the player's cursor; the row refreshes when the field loses focus.
            if (list.panel?.focusController?.focusedElement is VisualElement focused && list.Contains(focused)) { dirty = true; return; }
            dirty = false;
            list.Clear();
            var learned = book.Entries.Count(e => e.Learned);
            summary.text = $"Фраз: {book.Entries.Count} · выучено: {learned} · файл: {book.Location}";
            if (book.SaveError != null) Report("Не удалось сохранить словарь: " + book.SaveError, null);
            if (book.Entries.Count == 0)
            {
                list.Add(Hud.Text("Пока пусто. Скажите ассистенту что-нибудь своими словами, например «сделай чуточку тише».", "phrasebook__empty"));
                return;
            }
            foreach (var entry in book.Entries.OrderByDescending(e => e.Learned).ThenByDescending(e => e.Uses)) list.Add(Row(entry));
        }

        private VisualElement Row(PhraseEntry entry)
        {
            var row = Hud.Box("phrasebook__row");
            var phrase = Field("", entry.Phrase);
            var command = Field("", entry.Command);
            void Commit()
            {
                if (phrase.value == entry.Phrase && command.value == entry.Command) return;
                if (Report(book.Edit(entry, phrase.value, command.value, state()), "Сохранено.")) return;
                phrase.SetValueWithoutNotify(entry.Phrase);
                command.SetValueWithoutNotify(entry.Command);
            }
            phrase.RegisterCallback<FocusOutEvent>(_ => Commit());
            command.RegisterCallback<FocusOutEvent>(_ => Commit());
            row.Add(phrase);
            row.Add(Hud.Text("→", "phrasebook__arrow"));
            row.Add(command);
            row.Add(entry.Learned ? Hud.Chip("ВЫУЧЕНО · " + entry.Uses, "ok") : Hud.Chip($"КАНДИДАТ {entry.Agreements}/{Phrasebook.AgreementsToLearn}", "warn"));
            if (!entry.Learned) row.Add(Hud.IconButton(HudIconKind.Check, () => { book.Confirm(entry); Report(null, $"Запомнил: «{entry.Phrase}»."); }));
            row.Add(Hud.IconButton(HudIconKind.Close, () => { book.Forget(entry); Report(null, $"Удалено: «{entry.Phrase}»."); }));
            return row;
        }

        private TextField Field(string placeholder, string value)
        {
            var field = new TextField { value = value };
            field.AddToClassList("hud-input");
            field.AddToClassList("phrasebook__field");
            field.textEdition.placeholder = placeholder;
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                field.Blur(); evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<FocusOutEvent>(_ => field.schedule.Execute(() => { if (dirty) Fill(); }));
            return field;
        }

        private void OnChanged() => list?.schedule.Execute(Fill);

        private bool Report(string error, string success)
        {
            if (status == null) return error == null;
            status.text = error ?? success ?? "";
            status.EnableInClassList("is-error", error != null);
            return error == null;
        }

        private void Export()
        {
            try
            {
                var path = book.Export();
                Report(null, "Выгружено: " + path);
                Open(Path.GetDirectoryName(path));
            }
            catch (Exception error) { Report("Не удалось выгрузить словарь: " + error.Message, null); }
        }

        private void Import()
        {
            try { Report(null, book.Import(state())); }
            catch (Exception error) { Report("Не удалось импортировать словарь: " + error.Message, null); }
        }

        private void OpenImportFolder()
        {
            try
            {
                Directory.CreateDirectory(book.ImportFolder);
                Report(null, "Положите файлы словаря (.json) в папку и нажмите «Импорт»: " + book.ImportFolder);
                Open(book.ImportFolder);
            }
            catch (Exception error) { Report("Не удалось открыть папку: " + error.Message, null); }
        }

        private void Clear()
        {
            if (!clearArmed)
            {
                clearArmed = true;
                clearText.text = "ТОЧНО ОЧИСТИТЬ?";
                root.schedule.Execute(() => { clearArmed = false; clearText.text = "ОЧИСТИТЬ"; }).StartingIn(3000);
                return;
            }
            clearArmed = false;
            clearText.text = "ОЧИСТИТЬ";
            book.Clear();
            Report(null, "Словарь очищен.");
        }

        private static void Open(string folder) => UnityEngine.Application.OpenURL("file:///" + folder.Replace('\\', '/'));
    }
}
