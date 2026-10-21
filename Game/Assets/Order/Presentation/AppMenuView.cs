using System;
using System.Collections.Generic;
using System.Threading;
using Order.Application;
using Order.Content;
using Order.Contracts;
using Order.Input;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    public sealed class AppMenuView : IDisposable
    {
        private readonly AppController app;
        private readonly AppCommandInput input;
        private readonly VisualElement root;
        private readonly VisualElement page;
        private readonly Label assistant;
        private readonly Label feedback;
        private readonly TextField transcript;
        private bool syncing;
        private Slider volume;
        private DropdownField faction;
        private int lastKeyboardSubmitFrame = -1;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private Button submitButton;
        private bool busy;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Label timingLabel;
#endif

        public AppMenuView(VisualElement root, AppController app, AppCommandInput input)
        {
            this.app = app; this.input = input;
            root.Clear();
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            root = scroll.contentContainer;
            this.root = root;
            root.AddToClassList("order-root");
            root.Add(new Label("THIS IS AN ORDER") { name = "title" });
            root.Add(new Label("КОМАНДНЫЙ ЦЕНТР") { name = "subtitle" });
            var navigation = new VisualElement { name = "navigation" };
            navigation.Add(Button("Главное меню", () => Send(AppCommand.Navigate(AppScreen.MainMenu))));
            navigation.Add(Button("Настройки звука", () => Send(AppCommand.Navigate(AppScreen.AudioSettings))));
            navigation.Add(Button("Матч против бота", () => Send(AppCommand.Navigate(AppScreen.MatchSetup))));
            root.Add(navigation);
            page = new VisualElement { name = "page" }; root.Add(page);
            assistant = new Label { name = "assistant" }; root.Add(assistant);
            var commandPanel = new VisualElement { name = "command-panel" };
            commandPanel.Add(new Label("Введите приказ ассистенту"));
            transcript = new TextField { name = "command-input" };
            transcript.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != UnityEngine.KeyCode.Return && evt.keyCode != UnityEngine.KeyCode.KeypadEnter) return;
                SubmitFromKeyboard(); evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            transcript.RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                SubmitFromKeyboard(); evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            commandPanel.Add(transcript);
            submitButton = Button("Выполнить", Submit);
            commandPanel.Add(submitButton);
            feedback = new Label("Готов к приказам.") { name = "feedback" };
            commandPanel.Add(feedback);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            timingLabel = new Label { name = "command-timing" };
            commandPanel.Add(timingLabel);
#endif
            commandPanel.Add(new Label("Например: «Громкость 11 процентов» или «Сделай потише»."));
            commandPanel.Add(new Label("Или: «Запускай матч против бота второй фракции»."));
            root.Add(commandPanel);
            root.Add(new Label("Голосовой ввод будет подключён следующим этапом. Режим: удержание кнопки.") { name = "voice-status" });
            app.Changed += Refresh;
            Refresh();
        }

        private static Button Button(string text, Action action) => new Button(action) { text = text };
        private void Send(AppCommand command) { if (!syncing) Show(input.SubmitManual(command)); }
        private async void Submit()
        {
            if (busy || lifetime.IsCancellationRequested) return;
            busy = true;
            submitButton.SetEnabled(false);
            transcript.SetEnabled(false);
            feedback.text = "Разбираю команду…";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            timingLabel.text = "";
#endif
            try
            {
                var result = await input.SubmitTranscriptAsync(transcript.value,
                    new CommandContext(app.Session, CommandSource.Text, app.Revision), lifetime.Token);
                if (!lifetime.IsCancellationRequested)
                {
                    Show(result);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var measured = input.LastMeasurement;
                    if (measured != null)
                    {
                        var intent = measured.Intent;
                        var route = intent?.Route == "model" ? "модель" : intent?.Route == "direct" ? "быстрый путь" : "без разбора";
                        timingLabel.text = $"Время: {measured.TotalMs:0.0} мс · {route} · выполнение {measured.ExecutionMs:0.00} мс";
                        if (intent?.Route == "model") timingLabel.text += $" · ожидание {intent.QueueMs:0} · запуск {intent.StartupMs:0} · запрос {intent.HttpMs:0} мс";
                    }
#endif
                }
            }
            finally
            {
                if (!lifetime.IsCancellationRequested) { busy = false; submitButton.SetEnabled(true); transcript.SetEnabled(true); }
            }
        }
        private void SubmitFromKeyboard()
        {
            if (lastKeyboardSubmitFrame == UnityEngine.Time.frameCount) return;
            lastKeyboardSubmitFrame = UnityEngine.Time.frameCount;
            Submit();
        }
        private void Show(CommandResult result) { Refresh(); feedback.text = result.Message; feedback.EnableInClassList("rejected", !result.Applied); }

        private void Refresh()
        {
            assistant.text = "Ассистент · " + PrototypeCatalog.FactionLabel(app.Settings.AssistantFaction);
            // Preserve focused controls during live slider changes.
            if ((AppScreen?)page.userData != app.Screen)
            {
                page.Clear(); volume = null; faction = null; page.userData = app.Screen;
                switch (app.Screen)
                {
                    case AppScreen.MainMenu:
                        page.Add(new Label("Главное меню") { name = "page-title" });
                        page.Add(new Label("Выберите образ ассистента. Названия рас пока временные."));
                        faction = new DropdownField("Образ ассистента", new List<string> { "Фракция 1", "Фракция 2" }, 0);
                        faction.RegisterValueChangedCallback(evt => Send(AppCommand.SetAssistant((FactionId)faction.index)));
                        page.Add(faction);
                        break;
                    case AppScreen.AudioSettings:
                        page.Add(new Label("Настройки звука") { name = "page-title" });
                        volume = new Slider("Общая громкость, %", 0, 100) { showInputField = true, name = "master-volume" };
                        volume.RegisterValueChangedCallback(evt => Send(AppCommand.SetVolume(evt.newValue / 100f)));
                        page.Add(volume);
                        break;
                    case AppScreen.MatchSetup:
                        page.Add(new Label("Матч против бота") { name = "page-title" });
                        page.Add(new Label("Прототипная арена · обычная сложность"));
                        page.Add(Button("Против фракции 1", () => Send(AppCommand.PrepareMatch(FactionId.Expedition))));
                        page.Add(Button("Против фракции 2", () => Send(AppCommand.PrepareMatch(FactionId.Resonance))));
                        page.Add(new Label { name = "match-summary" });
                        page.Add(new Label("Сейчас доступна подготовка матча. Боевой прототип — следующий этап."));
                        break;
                }
            }
            syncing = true;
            volume?.SetValueWithoutNotify(app.Settings.MasterVolume * 100f);
            if (faction != null) faction.SetValueWithoutNotify(PrototypeCatalog.FactionLabel(app.Settings.AssistantFaction));
            var summary = page.Q<Label>("match-summary");
            if (summary != null) summary.text = app.HasMatchDraft ? "Противник: " + PrototypeCatalog.FactionLabel(app.EnemyFaction) : "Выберите фракцию противника.";
            syncing = false;
        }

        public void Dispose() { lifetime.Cancel(); app.Changed -= Refresh; root.Clear(); }
    }
}
