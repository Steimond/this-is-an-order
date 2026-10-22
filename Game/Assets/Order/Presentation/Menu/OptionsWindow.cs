using System;
using System.Collections.Generic;
using System.Linq;
using Order.Content;
using Order.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Settings window built from MenuCatalog. Every change, tab switch and reset is an app command,
    // so the same actions are available to the assistant's text commands.
    public sealed class OptionsWindow
    {
        private const string DefaultInfo = "Наведите курсор на параметр, чтобы увидеть описание.";
        private readonly HudModal modal = new HudModal("options");
        private readonly Action<AppCommand> send;
        private readonly List<(Button button, AppScreen screen)> tabs = new List<(Button, AppScreen)>();
        private readonly List<Action<SettingsSnapshot>> sync = new List<Action<SettingsSnapshot>>();
        private readonly Dictionary<string, VisualElement> rows = new Dictionary<string, VisualElement>();
        private readonly ScrollView scroll;
        private readonly Label note, info;
        private AppScreen? shown;

        public VisualElement Layer => modal.Layer;
        public bool IsOpen => modal.IsOpen;
        public event Action CloseRequested;
        // Extra content appended to a tab after its parameters (the phrasebook editor on the voice tab).
        public Func<AppScreen, VisualElement> Extra;

        public OptionsWindow(Action<AppCommand> send)
        {
            this.send = send;
            var window = modal.Window;
            window.AddToClassList("options");
            var header = Hud.Box("options__header");
            var titles = Hud.Box("options__titles");
            titles.Add(Hud.Text("НАСТРОЙКИ", "hud-modal__title"));
            titles.Add(Hud.Text("КОНФИГУРАЦИЯ КОМАНДНОГО ЦЕНТРА", "hud-modal__subtitle"));
            header.Add(titles);
            header.Add(Hud.IconButton(HudIconKind.Close, () => CloseRequested?.Invoke()));
            window.Add(header);

            var body = Hud.Box("options__body");
            var tabColumn = Hud.Box("options__tabs");
            for (var i = 0; i < MenuCatalog.Tabs.Count; i++)
            {
                var screen = MenuCatalog.Tabs[i].Screen;
                var tab = Hud.NavItem((i + 1).ToString("00"), MenuCatalog.Tabs[i].Title, null, () => send(AppCommand.Navigate(screen)));
                tabs.Add((tab, screen)); tabColumn.Add(tab);
            }
            tabColumn.Add(Hud.Box("options__tabs-spacer"));
            foreach (var (key, action) in new[] { ("ESC", "ЗАКРЫТЬ"), ("TAB", "СЛЕДУЮЩИЙ ПАРАМЕТР"), ("← →", "ИЗМЕНИТЬ ЗНАЧЕНИЕ") })
            {
                var hint = Hud.Box("options__hint");
                hint.Add(Hud.Keycap(key));
                hint.Add(Hud.Text(action, "options__hint-text"));
                tabColumn.Add(hint);
            }
            body.Add(tabColumn);
            var content = Hud.Box("options__content");
            note = Hud.Text("", "options__note");
            content.Add(note);
            scroll = new ScrollView(ScrollViewMode.Vertical) { mouseWheelScrollSize = 48 };
            scroll.AddToClassList("hud-scroll"); scroll.AddToClassList("options__scroll");
            content.Add(scroll);
            body.Add(content);
            window.Add(body);

            var footer = Hud.Box("options__footer");
            info = Hud.Text(DefaultInfo, "options__info");
            footer.Add(info);
            footer.Add(Hud.Button("ПО УМОЛЧАНИЮ", () => { if (shown.HasValue) send(AppCommand.ResetOptions(shown.Value)); }, "hud-button--ghost"));
            var done = Hud.Button("ГОТОВО", () => CloseRequested?.Invoke(), "hud-button--primary");
            done.AddToClassList("options__done");
            footer.Add(done);
            window.Add(footer);
        }

        // Opens the window on the tab, or switches tabs; values are refreshed without rebuilding rows.
        public void Show(AppScreen tab, SettingsSnapshot settings)
        {
            if (shown != tab) Build(tab, settings);
            else Sync(settings);
            if (modal.IsOpen) return;
            modal.Open();
            tabs.FirstOrDefault(t => t.screen == tab).button?.Focus();
        }

        public void Close() { modal.Close(); }

        public void Sync(SettingsSnapshot settings) { foreach (var update in sync) update(settings); }

        // Scrolls to a parameter changed by a command and flashes its row.
        public void Reveal(string optionId)
        {
            if (!rows.TryGetValue(optionId, out var row)) return;
            scroll.ScrollTo(row);
            row.AddToClassList("is-changed");
            row.schedule.Execute(() => row.RemoveFromClassList("is-changed")).StartingIn(900);
        }

        private void Build(AppScreen tab, SettingsSnapshot settings)
        {
            shown = tab;
            foreach (var (button, screen) in tabs) button.EnableInClassList("is-active", screen == tab);
            scroll.Clear(); sync.Clear(); rows.Clear();
            note.text = MenuCatalog.Tab(tab)?.Note ?? "";
            info.text = DefaultInfo;
            string section = null;
            foreach (var option in MenuCatalog.Options)
            {
                if (option.Tab != tab && option.AlsoOn != tab) continue;
                if (option.DevelopmentOnly && !Debug.isDebugBuild) continue;
                if (option.Section != section) { section = option.Section; scroll.Add(Hud.Text(section, "hud-section-title")); }
                Row(option, Control(option, settings));
            }
            var extra = Extra?.Invoke(tab);
            if (extra != null) scroll.Add(extra);
            scroll.scrollOffset = Vector2.zero;
        }

        private VisualElement Control(MenuOption option, SettingsSnapshot settings)
        {
            var value = MenuCatalog.Value(settings, option);
            switch (option.Kind)
            {
                case OptionKind.Toggle:
                    var toggle = new HudSwitch(value >= .5f);
                    toggle.RegisterValueChangedCallback(evt => send(AppCommand.SetOption(option.Id, evt.newValue ? 1 : 0)));
                    sync.Add(s => toggle.SetValueWithoutNotify(MenuCatalog.Value(s, option) >= .5f));
                    return toggle;
                case OptionKind.Range:
                    var slider = new HudSlider(option.Min, option.Max, value, 1, v => Mathf.Round(v) + "%");
                    slider.RegisterValueChangedCallback(evt => send(AppCommand.SetOption(option.Id, evt.newValue)));
                    sync.Add(s => slider.SetValueWithoutNotify(MenuCatalog.Value(s, option)));
                    return slider;
                case OptionKind.Choice:
                    // Short fixed sets read better as segments; long or numerous labels use the cycler.
                    if (option.Choices.Count <= 4 && option.Choices.All(c => c.Length <= 10))
                    {
                        var segments = new HudSegmented(option.Choices.Select(c => c.ToUpperInvariant()).ToList(), (int)value, option.Locked.ToList());
                        segments.RegisterValueChangedCallback(evt => send(AppCommand.SetOption(option.Id, evt.newValue)));
                        sync.Add(s => segments.SetValueWithoutNotify((int)MenuCatalog.Value(s, option)));
                        return segments;
                    }
                    var selector = new HudSelector(option.Choices, (int)value);
                    selector.RegisterValueChangedCallback(evt => send(AppCommand.SetOption(option.Id, evt.newValue)));
                    sync.Add(s => selector.SetValueWithoutNotify((int)MenuCatalog.Value(s, option)));
                    return selector;
                default:
                    var bind = new HudKeyBind(MenuCatalog.Key(settings.Keys, option));
                    bind.Changed += key => send(AppCommand.SetKey(option.Id, key));
                    sync.Add(s => bind.SetValueWithoutNotify(MenuCatalog.Key(s.Keys, option)));
                    return bind;
            }
        }

        private void Row(MenuOption option, VisualElement control)
        {
            var row = Hud.Box("hud-row");
            var label = Hud.Box("hud-row__label");
            label.Add(Hud.Text(option.Title, "hud-row__title"));
            if (option.Live) { var tag = Hud.Chip("ДЕЙСТВУЕТ", "ok"); tag.AddToClassList("hud-row__live"); label.Add(tag); }
            row.Add(label);
            var holder = Hud.Box("hud-row__control");
            holder.Add(control);
            row.Add(holder);
            row.RegisterCallback<PointerEnterEvent>(_ => info.text = option.Description);
            row.RegisterCallback<PointerLeaveEvent>(_ => info.text = DefaultInfo);
            row.RegisterCallback<FocusInEvent>(_ => info.text = option.Description);
            scroll.Add(row);
            rows[option.Id] = row;
        }
    }
}
