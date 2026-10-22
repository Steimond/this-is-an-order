using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Order.Application;
using Order.Content;
using Order.Contracts;
using Order.Input;
using Order.Voice;
using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Main menu: navigation on the left, the assistant in the centre with the command console,
    // context panel on the right. Every control sends an AppController command, so the assistant's
    // text commands reach the same actions; the view only mirrors controller state.
    public sealed class AppMenuView : IDisposable
    {
        private static bool greeted;
        private readonly AppController app;
        private readonly AppCommandInput input;
        private readonly VisualElement root;
        private readonly VisualElement screen;
        private readonly VisualElement context;
        private readonly Task<bool> warmup;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly HudCursor cursor;
        private readonly HudSound sound;
        private readonly AssistantCore core;
        private readonly HudStatus mood, brain;
        private readonly Label assistantName, reply, breadcrumb, clock, session;
        private readonly VisualElement log, console, center, hints, dock;
        private readonly Label dockReply;
        private readonly TextField transcript;
        private readonly Button submitButton, navHome, navSkirmish;
        private readonly OptionsWindow options;
        private readonly HudModal exitDialog = new HudModal("exit-dialog");
        private readonly HudScanline scanline;
        private readonly Phrasebook phrasebook;
        private readonly PhrasebookPanel phrasebookPanel;
        private readonly List<(Button card, FactionId faction)> assistantCards = new List<(Button, FactionId)>();
        private readonly List<(Button card, FactionId faction)> enemyCards = new List<(Button, FactionId)>();
        private Label matchSummary;
        private HudStatus panelBrain;
        private HudSegmented difficulty;
        private AppScreen? contextScreen;
        private SettingsSnapshot shownSettings;
        private bool syncing, busy, consoleFocused;
        private int lastKeyboardSubmitFrame = -1, consoleBlurFrame = -1;
        private string speech = "";
        private int shown;
        private float speechStart, speechDone, nextSlowTick;
        private bool speechAlert;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly Label timingLabel;
#endif

        public AppMenuView(VisualElement root, AppController app, AppCommandInput input, Task<bool> assistantWarmup = null, Phrasebook phrasebook = null)
        {
            this.root = root; this.app = app; this.input = input; warmup = assistantWarmup; this.phrasebook = phrasebook;
            root.Clear();
            screen = Hud.Box("hud-root", "order-menu");
            screen.AddToClassList("hud-theme-command");
            screen.AddToClassList("order-menu");
            root.Add(screen);
            HudFonts.Apply(screen);
            screen.Add(new HudBackdrop());
            scanline = new HudScanline();
            screen.Add(scanline);

            var layout = Hud.Box("menu-layout", "menu-layout");
            screen.Add(layout);

            var top = Hud.Box("top-bar", "top-bar");
            var brand = Hud.Box("brand");
            brand.Add(Hud.Text("THIS IS AN ORDER", "brand__title"));
            brand.Add(Hud.Text("КОМАНДНЫЙ ЦЕНТР · ПРОТОТИП", "brand__subtitle"));
            top.Add(brand);
            var crumbs = Hud.Box("top-bar__center");
            breadcrumb = Hud.Text("", "breadcrumb");
            crumbs.Add(breadcrumb);
            var telemetry = new HudTelemetry(28, 3.7f); telemetry.AddToClassList("top-bar__telemetry");
            crumbs.Add(telemetry);
            top.Add(crumbs);
            var readouts = Hud.Box("top-bar__readouts");
            brain = new HudStatus();
            readouts.Add(Labeled("РАЗБОР ПРИКАЗОВ", brain));
            readouts.Add(Labeled("ГОЛОС", new HudStatus("СКОРО", null)));
            clock = Hud.Text("", "clock");
            readouts.Add(clock);
            top.Add(readouts);
            layout.Add(top);

            var body = Hud.Box("menu-body", "menu-body");
            var nav = Hud.Box("nav", "nav");
            nav.Add(Hud.Text("НАВИГАЦИЯ", "column-title"));
            var index = 0;
            string Next() => (++index).ToString("00");
            navHome = Hud.NavItem(Next(), "ШТАБ", "Командный центр", () => Send(AppCommand.Navigate(AppScreen.MainMenu)));
            nav.Add(navHome);
            nav.Add(Hud.NavItem(Next(), "КАМПАНИЯ", "Сюжетные операции", null, "СКОРО"));
            navSkirmish = Hud.NavItem(Next(), "СХВАТКА", "Матч против ИИ", () => Send(AppCommand.Navigate(AppScreen.MatchSetup)));
            nav.Add(navSkirmish);
            nav.Add(Hud.NavItem(Next(), "СЕТЕВАЯ ИГРА", "Дуэль 1 на 1", null, "СКОРО"));
            nav.Add(Hud.NavItem(Next(), "ПОЛИГОН", "Temple Basin · тестовая карта", () => Send(AppCommand.LaunchTestMap())));
            nav.Add(Hud.NavItem(Next(), "АРХИВ", "Справочник и обучение", null, "СКОРО"));
            nav.Add(Hud.Box("nav__spacer"));
            nav.Add(Hud.NavItem(null, "НАСТРОЙКИ", "Управление, графика, звук", () => Send(AppCommand.OpenSettings()), icon: HudIconKind.Gear));
            nav.Add(Hud.NavItem(null, "ВЫХОД", "Завершить сеанс", () => Send(AppCommand.RequestExit()), icon: HudIconKind.Power));
            body.Add(nav);

            center = Hud.Box("center", "center");
            var channel = Hud.Box("channel");
            channel.Add(Hud.Text("// КАНАЛ АССИСТЕНТА", "column-title"));
            mood = new HudStatus();
            channel.Add(mood);
            center.Add(channel);
            core = new AssistantCore { name = "assistant-core" };
            center.Add(core);
            assistantName = Hud.Text("", "assistant-name");
            center.Add(assistantName);
            reply = Hud.Text("", "assistant-reply");
            center.Add(reply);
            log = Hud.Box("comms-log", "comms-log");
            center.Add(log);

            console = Hud.Box("console", "console");
            console.Add(new HudFrame(10, 10));
            var mic = Hud.Strip(new Button(), "console__mic");
            mic.Add(new HudIcon(HudIconKind.Mic));
            mic.Add(Hud.Keycap("V"));
            mic.SetEnabled(false);
            console.Add(mic);
            console.Add(Hud.Text("›", "console__prompt"));
            transcript = new TextField { name = "command-input" };
            transcript.AddToClassList("hud-input");
            transcript.textEdition.placeholder = "Введите приказ ассистенту…";
            transcript.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                SubmitFromKeyboard(); evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            transcript.RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                SubmitFromKeyboard(); evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            transcript.RegisterCallback<FocusInEvent>(_ => { consoleFocused = true; console.AddToClassList("is-focused"); if (!busy && core.Mood == AssistantMood.Idle) SetMood(AssistantMood.Attentive); });
            transcript.RegisterCallback<FocusOutEvent>(_ => { consoleFocused = false; consoleBlurFrame = Time.frameCount; console.RemoveFromClassList("is-focused"); if (core.Mood == AssistantMood.Attentive) SetMood(AssistantMood.Idle); });
            console.Add(transcript);
            submitButton = Hud.Button("ОТПРАВИТЬ", Submit);
            submitButton.AddToClassList("console__submit");
            console.Add(submitButton);
            center.Add(console);
            hints = Hud.Box("console-hints");
            hints.Add(Hud.Text("Команды: «открой графику», «тени высокие», «включи курсор», «полигон», «громкость 50%». Список — «помощь».", "hud-caption"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            timingLabel = Hud.Text("", "command-timing");
            hints.Add(timingLabel);
#endif
            center.Add(hints);
            body.Add(center);

            context = Hud.Box("context", "context");
            body.Add(context);
            layout.Add(body);

            var status = Hud.Box("status-bar", "status-bar");
            status.Add(Hint("ENTER", "ОТПРАВИТЬ ПРИКАЗ"));
            status.Add(Hint("ESC", "НАЗАД"));
            status.Add(Hint("V", "ГОЛОС · СКОРО"));
            status.Add(Hud.Box("status-bar__spacer"));
            session = Hud.Text("", "status-bar__build");
            status.Add(session);
            layout.Add(status);

            options = new OptionsWindow(command => Send(command));
            options.CloseRequested += () => Send(AppCommand.Back());
            if (phrasebook != null)
            {
                phrasebookPanel = new PhrasebookPanel(phrasebook, () => app.State);
                options.Extra = tab => tab == AppScreen.VoiceSettings ? phrasebookPanel.Build() : null;
                phrasebook.Learned += OnLearned;
            }
            screen.Add(options.Layer);
            BuildExitDialog();
            screen.Add(exitDialog.Layer);
            // While a window is open the console docks above the dimmer, so commands keep working.
            dock = Hud.Box("console-dock", "console-dock");
            dockReply = Hud.Text("", "console-dock__reply");
            dock.Add(dockReply);
            screen.Add(dock);

            cursor = new HudCursor(screen, new Color(.25f, .83f, .78f), new Color(.73f, 1f, .97f), new Color(.016f, .035f, .047f), new Color(1f, .54f, .24f));
            sound = new HudSound(screen);
            screen.schedule.Execute(Tick).Every(0);
            shownSettings = app.Settings;
            app.Changed += Refresh;
            Refresh();
            Say(greeted ? "С возвращением, командир. Канал связи восстановлен." : "Командный центр в сети. Жду ваших приказов, командир.");
            greeted = true;
        }

        private static VisualElement Labeled(string caption, VisualElement value)
        {
            var box = Hud.Box("readout");
            box.Add(Hud.Text(caption, "readout__caption"));
            box.Add(value);
            return box;
        }

        private static VisualElement Hint(string key, string text)
        {
            var hint = Hud.Box("status-bar__hint");
            hint.Add(Hud.Keycap(key));
            hint.Add(Hud.Text(text, "status-bar__text"));
            return hint;
        }

        private void BuildExitDialog()
        {
            var window = exitDialog.Window;
            window.AddToClassList("exit-dialog");
            window.Add(Hud.Text("ЗАВЕРШИТЬ СЕАНС?", "hud-modal__title"));
            window.Add(Hud.Text("Командный центр будет закрыт. Настройки уже сохранены. Можно ответить ассистенту: «да» или «отмена».", "exit-dialog__text"));
            var buttons = Hud.Box("exit-dialog__buttons");
            var cancel = Hud.Button("ОТМЕНА", () => Send(AppCommand.CancelExit()));
            cancel.AddToClassList("exit-dialog__cancel");
            buttons.Add(cancel);
            buttons.Add(Hud.Button("ВЫЙТИ", () => Send(AppCommand.ConfirmExit()), "hud-button--danger"));
            window.Add(buttons);
        }

        private void Send(AppCommand command, bool speak = false)
        {
            if (syncing) return;
            var result = input.SubmitManual(command);
            if (lifetime.IsCancellationRequested) return;
            if (!result.Applied) Say(result.Message, true);
            else if (speak) Say(result.Message);
        }

        private async void Submit()
        {
            if (busy || lifetime.IsCancellationRequested) return;
            busy = true;
            submitButton.SetEnabled(false);
            transcript.SetEnabled(false);
            var text = transcript.value ?? "";
            if (!string.IsNullOrWhiteSpace(text)) AddLog(text.Trim(), true, false);
            SetMood(AssistantMood.Thinking);
            speech = ""; shown = 0;
            reply.text = "Разбираю приказ…";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            timingLabel.text = "";
#endif
            try
            {
                var result = await input.SubmitTranscriptAsync(text,
                    new CommandContext(app.Session, CommandSource.Text, app.Revision), lifetime.Token);
                if (!lifetime.IsCancellationRequested)
                {
                    busy = false;
                    var alert = !result.Applied && result.Outcome != CommandOutcome.Answered;
                    Say(result.Message, alert);
                    AddLog(result.Message, false, alert);
                    if (result.Applied || result.Outcome == CommandOutcome.Answered) transcript.SetValueWithoutNotify("");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var measured = input.LastMeasurement;
                    if (measured != null)
                    {
                        var intent = measured.Intent;
                        var route = intent?.Route == "model" ? "модель" : intent?.Route == "direct" ? "быстрый путь" : intent?.Route == "command" ? "команда меню" : intent?.Route == "phrasebook" ? "личный словарь" : "без разбора";
                        timingLabel.text = $"Время: {measured.TotalMs:0.0} мс · {route} · выполнение {measured.ExecutionMs:0.00} мс";
                        if (intent?.Route == "model") timingLabel.text += $" · ожидание {intent.QueueMs:0} · запуск {intent.StartupMs:0} · запрос {intent.HttpMs:0} мс";
                    }
#endif
                }
            }
            finally
            {
                if (!lifetime.IsCancellationRequested)
                {
                    busy = false;
                    submitButton.SetEnabled(true);
                    transcript.SetEnabled(true);
                    transcript.Focus();
                }
            }
        }

        private void SubmitFromKeyboard()
        {
            if (lastKeyboardSubmitFrame == Time.frameCount) return;
            lastKeyboardSubmitFrame = Time.frameCount;
            Submit();
        }

        private void Say(string text, bool alert = false)
        {
            speech = text ?? "";
            shown = 0;
            speechStart = Time.realtimeSinceStartup;
            speechAlert = alert;
            SetMood(alert ? AssistantMood.Alert : AssistantMood.Speaking);
            reply.EnableInClassList("is-alert", alert);
            sound?.Play(alert ? HudCue.Deny : HudCue.Reply);
        }

        private void AddLog(string text, bool player, bool alert)
        {
            var line = Hud.Text((player ? "ВЫ  › " : "АССИСТЕНТ  › ") + text, "comms-log__line");
            line.AddToClassList(player ? "comms-log__line--player" : "comms-log__line--assistant");
            line.EnableInClassList("is-alert", alert);
            log.Add(line);
            while (log.childCount > 4) log.RemoveAt(0);
            for (var i = 0; i < log.childCount; i++) log[i].EnableInClassList("is-old", i < log.childCount - 2);
        }

        private void OnLearned(PhraseEntry entry) => AddLog($"Выучил фразу «{entry.Phrase}» → «{entry.Command}».", false, false);

        private void SetMood(AssistantMood next)
        {
            core.Mood = next;
            switch (next)
            {
                case AssistantMood.Thinking: mood.Set("АНАЛИЗ ПРИКАЗА", "pending"); break;
                case AssistantMood.Speaking: mood.Set("ОТВЕТ", "ok"); break;
                case AssistantMood.Alert: mood.Set("ТРЕБУЕТСЯ УТОЧНЕНИЕ", "warn"); break;
                case AssistantMood.Attentive: mood.Set("ГОТОВ К ПРИКАЗУ", "ok"); break;
                default: mood.Set("ОЖИДАНИЕ", null); break;
            }
        }

        private void Tick()
        {
            var now = Time.realtimeSinceStartup;
            if (shown < speech.Length)
            {
                shown = Mathf.Min(speech.Length, Mathf.FloorToInt((now - speechStart) * 52) + 1);
                reply.text = speech.Substring(0, shown) + (shown < speech.Length ? "_" : "");
                core.SpeechLevel = .35f + .65f * Mathf.PerlinNoise(now * 8, 0);
                if (shown == speech.Length) speechDone = now;
            }
            else if (!busy && (core.Mood == AssistantMood.Speaking || core.Mood == AssistantMood.Alert)
                && now - speechDone > (speechAlert ? 3f : 1.4f))
                SetMood(consoleFocused ? AssistantMood.Attentive : AssistantMood.Idle);
            dockReply.text = reply.text;
            dockReply.EnableInClassList("is-alert", reply.ClassListContains("is-alert"));
            screen.EnableInClassList("hud-blink", Mathf.FloorToInt(now * 2) % 2 == 0);
            if (now >= nextSlowTick) { nextSlowTick = now + .5f; SlowTick(); }
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !HudKeyBind.Capturing && HudKeyBind.ConsumedFrame != Time.frameCount)
                OnEscape();
        }

        private void SlowTick()
        {
            clock.text = DateTime.Now.ToString("HH:mm");
            session.text = $"СБОРКА {UnityEngine.Application.version} · СЕАНС {app.Session:000} · РЕВИЗИЯ {app.Revision}";
            string text, tone;
            if (warmup == null) { text = "ОПОРНЫЕ ПРАВИЛА"; tone = "warn"; }
            else if (!warmup.IsCompleted) { text = "ПРОГРЕВ"; tone = "pending"; }
            else if (warmup.Status == TaskStatus.RanToCompletion && warmup.Result) { text = "ГОТОВ"; tone = "ok"; }
            else { text = "ОГРАНИЧЕН"; tone = "warn"; }
            brain.Set(text, tone);
            panelBrain?.Set(text, tone);
        }

        // Esc goes through the same commands as the assistant: cancel exit, close settings, go back, or ask to exit from the HQ.
        private void OnEscape()
        {
            if (consoleFocused || consoleBlurFrame == Time.frameCount) { transcript.Blur(); return; }
            if (app.ExitPending) Send(AppCommand.CancelExit());
            else if (app.Screen == AppScreen.MainMenu) Send(AppCommand.RequestExit());
            else Send(AppCommand.Back());
        }

        private void Refresh()
        {
            var place = app.Screen.IsSettings() ? app.ReturnScreen : app.Screen;
            if (contextScreen != place) BuildContext(place);
            navHome.EnableInClassList("is-active", place == AppScreen.MainMenu);
            navSkirmish.EnableInClassList("is-active", place == AppScreen.MatchSetup);
            var crumb = place == AppScreen.MatchSetup ? "ШТАБ  ›  СХВАТКА" : "ШТАБ";
            breadcrumb.text = app.Screen.IsSettings() ? crumb + "  ›  НАСТРОЙКИ  ›  " + MenuCatalog.Tab(app.Screen).Title : crumb;
            var faction = app.Settings.AssistantFaction;
            assistantName.text = "АССИСТЕНТ  ·  " + PrototypeCatalog.FactionLabel(faction).ToUpperInvariant();
            core.Faction = faction;

            if (app.Screen.IsSettings()) options.Show(app.Screen, app.Settings);
            else if (options.IsOpen) options.Close();
            // Only text commands reveal: manual edits already happen in view.
            if (options.IsOpen && busy) RevealChange(shownSettings, app.Settings);
            shownSettings = app.Settings;
            if (app.ExitPending && !exitDialog.IsOpen) { exitDialog.Open(); exitDialog.Window.Q<Button>(className: "exit-dialog__cancel")?.Focus(); }
            else if (!app.ExitPending && exitDialog.IsOpen) exitDialog.Close();
            var modal = options.IsOpen || exitDialog.IsOpen;
            screen.EnableInClassList("is-modal", modal);
            DockConsole(modal);

            syncing = true;
            foreach (var (card, id) in assistantCards) card.EnableInClassList("is-selected", id == faction);
            foreach (var (card, id) in enemyCards) card.EnableInClassList("is-selected", app.HasMatchDraft && id == app.EnemyFaction);
            difficulty?.SetValueWithoutNotify((int)MenuCatalog.Value(app.Settings, MenuCatalog.Find(MenuCatalog.Difficulty)));
            if (matchSummary != null)
                matchSummary.text = app.HasMatchDraft ? "Противник: " + PrototypeCatalog.FactionLabel(app.EnemyFaction) + " · карта и сложность выбраны." : "Выберите фракцию противника.";
            syncing = false;
            ApplyInterfaceOptions();
        }

        private void RevealChange(SettingsSnapshot before, SettingsSnapshot after)
        {
            foreach (var option in MenuCatalog.Options)
            {
                var changed = option.Kind == OptionKind.Key ? MenuCatalog.Key(before.Keys, option) != MenuCatalog.Key(after.Keys, option)
                    : MenuCatalog.Value(before, option) != MenuCatalog.Value(after, option);
                if (changed) { options.Reveal(option.Id); return; }
            }
        }

        private void DockConsole(bool docked)
        {
            if (docked) dock.BringToFront();
            if (docked == (console.parent == dock)) return;
            var focused = consoleFocused;
            if (docked) dock.Add(console);
            else center.Insert(center.IndexOf(hints), console);
            dock.EnableInClassList("is-visible", docked);
            if (focused) transcript.schedule.Execute(() => transcript.Focus());
        }

        private void ApplyInterfaceOptions()
        {
            float Option(string id) => MenuCatalog.Value(app.Settings, MenuCatalog.Find(id));
            cursor.Enabled = Option(MenuCatalog.TacticalCursor) >= .5f;
            sound.Volume = Option(MenuCatalog.InterfaceVolume) / 100f;
            scanline.style.display = Option(MenuCatalog.Scanlines) >= .5f ? DisplayStyle.Flex : DisplayStyle.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            timingLabel.style.display = Option(MenuCatalog.Diagnostics) >= .5f ? DisplayStyle.Flex : DisplayStyle.None;
#endif
        }

        private void BuildContext(AppScreen target)
        {
            contextScreen = target;
            context.Clear();
            assistantCards.Clear(); enemyCards.Clear();
            matchSummary = null; panelBrain = null; difficulty = null;
            if (target == AppScreen.MatchSetup) BuildSkirmish(); else BuildBriefing();
            SlowTick();
        }

        private void BuildBriefing()
        {
            var image = Hud.Panel("ОБРАЗ АССИСТЕНТА", "A-01", out var imageBody);
            imageBody.Add(Hud.Text("Облик, голос и манера ответов. Набор функций одинаков для обоих образов.", "hud-caption"));
            var cards = Hud.Box("faction-cards");
            foreach (var faction in new[] { FactionId.Expedition, FactionId.Resonance })
            {
                var card = Hud.FactionCard(faction, PrototypeCatalog.FactionLabel(faction).ToUpperInvariant(), "Временное название",
                    () => { if (faction != app.Settings.AssistantFaction) Send(AppCommand.SetAssistant(faction), true); });
                assistantCards.Add((card, faction));
                cards.Add(card);
            }
            imageBody.Add(cards);
            context.Add(image);

            var systems = Hud.Panel("СОСТОЯНИЕ СИСТЕМ", "S-02", out var systemsBody);
            panelBrain = new HudStatus();
            systemsBody.Add(SystemRow("Разбор приказов", panelBrain));
            systemsBody.Add(SystemRow("Голосовой ввод", new HudStatus("НЕ ПОДКЛЮЧЁН", "warn")));
            systemsBody.Add(SystemRow("Озвучка ответов", new HudStatus("НЕ ПОДКЛЮЧЕНА", "warn")));
            systemsBody.Add(SystemRow("Боевой прототип", new HudStatus("В РАЗРАБОТКЕ", null)));
            context.Add(systems);

            context.Add(Hud.Box("context__spacer"));
            var play = Hud.Button("ИГРАТЬ", () => Send(AppCommand.Navigate(AppScreen.MatchSetup)), "hud-button--primary");
            play.AddToClassList("context__play");
            context.Add(play);
        }

        private void BuildSkirmish()
        {
            var operation = Hud.Panel("СХВАТКА С ИИ", "M-01", out var operationBody);
            var preview = Hud.Box("map-card");
            preview.Add(new MapPreview());
            var mapLabel = Hud.Box("map-card__label");
            mapLabel.Add(Hud.Text("ПРОТОТИПНАЯ АРЕНА", "map-card__title"));
            mapLabel.Add(Hud.Text("1 НА 1  ·  " + PrototypeCatalog.MapId, "map-card__caption"));
            preview.Add(mapLabel);
            operationBody.Add(preview);
            operationBody.Add(Hud.Text("СЛОЖНОСТЬ", "field-title"));
            var level = MenuCatalog.Find(MenuCatalog.Difficulty);
            difficulty = new HudSegmented(new List<string>(level.Choices).ConvertAll(c => c.ToUpperInvariant()),
                (int)MenuCatalog.Value(app.Settings, level), new List<int>(level.Locked));
            difficulty.RegisterValueChangedCallback(evt => Send(AppCommand.SetOption(level.Id, evt.newValue)));
            operationBody.Add(difficulty);
            operationBody.Add(Hud.Text("ПРОТИВНИК", "field-title"));
            var cards = Hud.Box("faction-cards");
            foreach (var faction in new[] { FactionId.Expedition, FactionId.Resonance })
            {
                var card = Hud.FactionCard(faction, PrototypeCatalog.FactionLabel(faction).ToUpperInvariant(), "Бот · обычная",
                    () => Send(AppCommand.PrepareMatch(faction), true));
                card.AddToClassList("faction-card--enemy");
                enemyCards.Add((card, faction));
                cards.Add(card);
            }
            operationBody.Add(cards);
            matchSummary = Hud.Text("", "match-summary");
            operationBody.Add(matchSummary);
            context.Add(operation);

            context.Add(Hud.Box("context__spacer"));
            var start = Hud.Button("НАЧАТЬ МАТЧ", null, "hud-button--primary");
            start.AddToClassList("context__play");
            start.SetEnabled(false);
            context.Add(start);
            context.Add(Hud.Text("Боевой прототип — следующий этап. Пока доступен полигон.", "context__note"));
        }

        private static VisualElement SystemRow(string title, VisualElement status)
        {
            var row = Hud.Box("system-row");
            row.Add(Hud.Text(title, "system-row__title"));
            row.Add(status);
            return row;
        }

        public void Dispose()
        {
            lifetime.Cancel();
            app.Changed -= Refresh;
            if (phrasebook != null) phrasebook.Learned -= OnLearned;
            phrasebookPanel?.Dispose();
            cursor.Dispose();
            sound.Dispose();
            root.Clear();
        }
    }
}
