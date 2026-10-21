using System;
using System.Collections.Generic;
using Order.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Factory for the shared HUD controls. Interactive elements carry Hud.Clickable so the cursor
    // and interface sounds can react to them uniformly.
    public static class Hud
    {
        public const string Clickable = "hud-clickable";

        public static Label Text(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        public static VisualElement Box(string className, string name = null)
        {
            var element = new VisualElement { name = name };
            element.AddToClassList(className);
            return element;
        }

        public static T Strip<T>(T button, string className) where T : Button
        {
            button.RemoveFromClassList(UnityEngine.UIElements.Button.ussClassName);
            button.AddToClassList(className);
            button.AddToClassList(Clickable);
            return button;
        }

        // Frame, hover sweep, focus bar and corner brackets shared by every HUD button.
        public static void Dress(VisualElement host, float cut = 9)
        {
            host.Add(new HudFrame(cut, cut));
            var sweep = Box("hud-button__sweep"); sweep.pickingMode = PickingMode.Ignore; host.Add(sweep);
            var bar = Box("hud-button__bar"); bar.pickingMode = PickingMode.Ignore; host.Add(bar);
            foreach (var corner in new[] { "tl", "tr", "br", "bl" })
            {
                var bracket = Box("hud-corner"); bracket.AddToClassList("hud-corner--" + corner);
                bracket.pickingMode = PickingMode.Ignore; host.Add(bracket);
            }
        }

        public static Button Button(string text, Action action, string variant = null)
        {
            var button = Strip(new Button(action), "hud-button");
            if (variant != null) button.AddToClassList(variant);
            Dress(button);
            button.Add(Text(text, "hud-button__text"));
            return button;
        }

        public static Button IconButton(HudIconKind icon, Action action)
        {
            var button = Strip(new Button(action), "hud-button");
            button.AddToClassList("hud-button--icon");
            Dress(button, 6);
            button.Add(new HudIcon(icon));
            return button;
        }

        // Menu entry: index or icon, title, caption and a chevron; locked entries show a tag instead.
        public static Button NavItem(string index, string title, string caption, Action action, string lockedTag = null, HudIconKind? icon = null)
        {
            var button = Strip(new Button(action), "hud-button");
            button.AddToClassList("hud-nav-item");
            Dress(button, 10);
            if (icon.HasValue) { var glyph = new HudIcon(icon.Value); glyph.AddToClassList("hud-nav-item__icon"); button.Add(glyph); }
            else button.Add(Text(index, "hud-nav-item__index"));
            var texts = Box("hud-nav-item__texts"); texts.pickingMode = PickingMode.Ignore;
            texts.Add(Text(title, "hud-nav-item__title"));
            if (!string.IsNullOrEmpty(caption)) texts.Add(Text(caption, "hud-nav-item__caption"));
            else button.AddToClassList("hud-nav-item--compact");
            button.Add(texts);
            if (lockedTag != null) { button.Add(Chip(lockedTag)); button.SetEnabled(false); }
            else { var chevron = new HudIcon(HudIconKind.ChevronRight); chevron.AddToClassList("hud-nav-item__chevron"); button.Add(chevron); }
            return button;
        }

        public static Label Chip(string text, string tone = null)
        {
            var chip = Text(text, "hud-chip");
            if (tone != null) chip.AddToClassList("hud-chip--" + tone);
            return chip;
        }

        public static Label Keycap(string key) => Text(key, "hud-keycap");

        public static VisualElement Panel(string title, string code, out VisualElement body)
        {
            var panel = Box("hud-panel");
            panel.Add(new HudFrame(12, 12));
            var header = Box("hud-panel__header");
            header.Add(Text(title, "hud-panel__title"));
            header.Add(Box("hud-panel__rule"));
            header.Add(Text(code, "hud-panel__code"));
            panel.Add(header);
            body = Box("hud-panel__body");
            panel.Add(body);
            return panel;
        }

        public static Button FactionCard(FactionId faction, string title, string caption, Action action)
        {
            var card = Strip(new Button(action), "hud-button");
            card.AddToClassList("faction-card");
            Dress(card, 10);
            var emblem = new FactionEmblem(faction);
            emblem.AddToClassList("faction-card__emblem");
            card.Add(emblem);
            card.Add(Text(title, "faction-card__title"));
            card.Add(Text(caption, "faction-card__caption"));
            card.Add(Text("ВЫБРАН", "faction-card__mark"));
            return card;
        }

        // Walks up to the element that owns pointer interaction (buttons, switches, sliders...).
        public static VisualElement FindInteractive(VisualElement element, VisualElement stop)
        {
            for (var current = element; current != null && current != stop; current = current.parent)
                if (current.ClassListContains(Clickable)) return current;
            return null;
        }

        public static bool IsTextInput(VisualElement element, VisualElement stop)
        {
            for (var current = element; current != null && current != stop; current = current.parent)
                if (current is TextField) return true;
            return false;
        }

        public static string KeyName(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "NUM " + (int)(key - KeyCode.Keypad0);
            switch (key)
            {
                case KeyCode.Space: return "ПРОБЕЛ";
                case KeyCode.Return: case KeyCode.KeypadEnter: return "ENTER";
                case KeyCode.Escape: return "ESC";
                case KeyCode.LeftShift: return "L-SHIFT";
                case KeyCode.RightShift: return "R-SHIFT";
                case KeyCode.LeftControl: return "L-CTRL";
                case KeyCode.RightControl: return "R-CTRL";
                case KeyCode.LeftAlt: return "L-ALT";
                case KeyCode.RightAlt: return "R-ALT";
                case KeyCode.UpArrow: return "СТРЕЛКА ВВЕРХ";
                case KeyCode.DownArrow: return "СТРЕЛКА ВНИЗ";
                case KeyCode.LeftArrow: return "СТРЕЛКА ВЛЕВО";
                case KeyCode.RightArrow: return "СТРЕЛКА ВПРАВО";
                default: return key.ToString().ToUpperInvariant();
            }
        }
    }

    public sealed class HudStatus : VisualElement
    {
        private readonly Label text;
        private string tone;

        public HudStatus(string value = "", string tone = null)
        {
            AddToClassList("hud-status");
            var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.AddToClassList("hud-status__dot"); Add(dot);
            text = Hud.Text(value, "hud-status__text"); Add(text);
            Set(value, tone);
        }

        public void Set(string value, string newTone)
        {
            text.text = value;
            if (tone != null) RemoveFromClassList("hud-status--" + tone);
            tone = newTone;
            if (tone != null) AddToClassList("hud-status--" + tone);
        }
    }

    public sealed class HudSwitch : VisualElement, INotifyValueChanged<bool>
    {
        private readonly Label state;
        private bool current;

        public HudSwitch(bool value)
        {
            AddToClassList("hud-switch"); AddToClassList(Hud.Clickable);
            focusable = true;
            var track = Hud.Box("hud-switch__track"); track.pickingMode = PickingMode.Ignore;
            var knob = Hud.Box("hud-switch__knob"); knob.pickingMode = PickingMode.Ignore;
            track.Add(knob); Add(track);
            state = Hud.Text("", "hud-switch__state"); Add(state);
            RegisterCallback<ClickEvent>(_ => Toggle());
            RegisterCallback<NavigationSubmitEvent>(_ => Toggle());
            SetValueWithoutNotify(value);
        }

        private void Toggle() { if (enabledInHierarchy) value = !current; }

        public bool value
        {
            get => current;
            set
            {
                if (current == value) return;
                var previous = current;
                SetValueWithoutNotify(value);
                using (var evt = ChangeEvent<bool>.GetPooled(previous, value)) { evt.target = this; SendEvent(evt); }
            }
        }

        public void SetValueWithoutNotify(bool newValue)
        {
            current = newValue;
            EnableInClassList("hud-switch--on", current);
            state.text = current ? "ВКЛ" : "ВЫКЛ";
        }
    }

    public sealed class HudSlider : VisualElement, INotifyValueChanged<float>
    {
        private readonly float min, max, step, keyStep;
        private readonly Func<float, string> format;
        private readonly VisualElement track, fill, thumb;
        private readonly Label readout;
        private float current;

        public HudSlider(float min, float max, float value, float step = 1, Func<float, string> format = null)
        {
            this.min = min; this.max = max; this.step = step;
            keyStep = Mathf.Max(step, (max - min) / 20);
            this.format = format ?? (v => v.ToString("0"));
            AddToClassList("hud-slider"); AddToClassList(Hud.Clickable);
            focusable = true;
            track = Hud.Box("hud-slider__track"); Add(track);
            fill = Hud.Box("hud-slider__fill"); fill.pickingMode = PickingMode.Ignore; track.Add(fill);
            track.Add(new HudTicks(10));
            thumb = Hud.Box("hud-slider__thumb"); thumb.pickingMode = PickingMode.Ignore; track.Add(thumb);
            readout = Hud.Text("", "hud-slider__value"); Add(readout);
            track.RegisterCallback<PointerDownEvent>(OnPointerDown);
            track.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            track.RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<NavigationMoveEvent>(OnNavigate);
            SetValueWithoutNotify(value);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!enabledInHierarchy || evt.button != 0) return;
            track.CapturePointer(evt.pointerId);
            Focus();
            Seek(evt.position);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt) { if (track.HasPointerCapture(evt.pointerId)) Seek(evt.position); }
        private void OnPointerUp(PointerUpEvent evt) { if (track.HasPointerCapture(evt.pointerId)) track.ReleasePointer(evt.pointerId); }

        private void Seek(Vector2 panelPosition)
        {
            var width = track.contentRect.width;
            if (width <= 0) return;
            var t = Mathf.Clamp01(track.WorldToLocal(panelPosition).x / width);
            value = Mathf.Lerp(min, max, t);
        }

        private void OnNavigate(NavigationMoveEvent evt)
        {
            if (evt.direction != NavigationMoveEvent.Direction.Left && evt.direction != NavigationMoveEvent.Direction.Right) return;
            value = current + (evt.direction == NavigationMoveEvent.Direction.Left ? -keyStep : keyStep);
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
        }

        public float value
        {
            get => current;
            set
            {
                var snapped = Mathf.Clamp(step > 0 ? Mathf.Round(value / step) * step : value, min, max);
                if (Mathf.Approximately(snapped, current)) return;
                var previous = current;
                SetValueWithoutNotify(snapped);
                using (var evt = ChangeEvent<float>.GetPooled(previous, current)) { evt.target = this; SendEvent(evt); }
            }
        }

        public void SetValueWithoutNotify(float newValue)
        {
            current = Mathf.Clamp(newValue, min, max);
            var percent = max > min ? (current - min) / (max - min) * 100 : 0;
            fill.style.width = Length.Percent(percent);
            thumb.style.left = Length.Percent(percent);
            readout.text = format(current);
        }
    }

    // "< value >" cycler, convenient with keyboard and gamepad.
    public sealed class HudSelector : VisualElement, INotifyValueChanged<int>
    {
        private readonly IReadOnlyList<string> options;
        private readonly Label label;
        private readonly List<VisualElement> pips = new List<VisualElement>();
        private int current = -1;

        public HudSelector(IReadOnlyList<string> options, int index)
        {
            this.options = options;
            AddToClassList("hud-selector");
            focusable = true;
            Add(Arrow(HudIconKind.ChevronLeft, -1));
            label = Hud.Text("", "hud-selector__value"); Add(label);
            Add(Arrow(HudIconKind.ChevronRight, 1));
            var row = Hud.Box("hud-selector__pips"); row.pickingMode = PickingMode.Ignore;
            // Position pips only while they stay readable.
            for (var i = 0; options.Count > 1 && options.Count <= 8 && i < options.Count; i++) { var pip = Hud.Box("hud-selector__pip"); pips.Add(pip); row.Add(pip); }
            Add(row);
            RegisterCallback<NavigationMoveEvent>(OnNavigate);
            SetValueWithoutNotify(index);
        }

        private Button Arrow(HudIconKind icon, int delta)
        {
            var arrow = Hud.Strip(new Button(() => Step(delta)), "hud-selector__arrow");
            arrow.focusable = false;
            arrow.Add(new HudIcon(icon));
            arrow.SetEnabled(options.Count > 1);
            return arrow;
        }

        private void Step(int delta) { if (options.Count > 1) value = (current + delta + options.Count) % options.Count; }

        private void OnNavigate(NavigationMoveEvent evt)
        {
            if (evt.direction != NavigationMoveEvent.Direction.Left && evt.direction != NavigationMoveEvent.Direction.Right) return;
            Step(evt.direction == NavigationMoveEvent.Direction.Left ? -1 : 1);
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
        }

        public int value
        {
            get => current;
            set
            {
                if (value == current || value < 0 || value >= options.Count) return;
                var previous = current;
                SetValueWithoutNotify(value);
                using (var evt = ChangeEvent<int>.GetPooled(previous, current)) { evt.target = this; SendEvent(evt); }
            }
        }

        public void SetValueWithoutNotify(int newValue)
        {
            current = Mathf.Clamp(newValue, 0, options.Count - 1);
            label.text = options[current];
            for (var i = 0; i < pips.Count; i++) pips[i].EnableInClassList("is-current", i == current);
        }
    }

    public sealed class HudSegmented : VisualElement, INotifyValueChanged<int>
    {
        private readonly List<Button> segments = new List<Button>();
        private int current = -1;

        public HudSegmented(IReadOnlyList<string> options, int index, ICollection<int> locked = null)
        {
            AddToClassList("hud-segmented");
            for (var i = 0; i < options.Count; i++)
            {
                var position = i;
                var segment = Hud.Strip(new Button(() => value = position) { text = options[i] }, "hud-segment");
                if (locked != null && locked.Contains(i)) segment.SetEnabled(false);
                segments.Add(segment); Add(segment);
            }
            SetValueWithoutNotify(index);
        }

        public int value
        {
            get => current;
            set
            {
                if (value == current || value < 0 || value >= segments.Count) return;
                var previous = current;
                SetValueWithoutNotify(value);
                using (var evt = ChangeEvent<int>.GetPooled(previous, current)) { evt.target = this; SendEvent(evt); }
            }
        }

        public void SetValueWithoutNotify(int newValue)
        {
            current = Mathf.Clamp(newValue, 0, segments.Count - 1);
            for (var i = 0; i < segments.Count; i++) segments[i].EnableInClassList("is-selected", i == current);
        }
    }

    // Click, then press a key. Esc cancels; the frame is recorded so Esc does not also close the window.
    public sealed class HudKeyBind : Button
    {
        public static int ConsumedFrame { get; private set; } = -1;
        public static bool Capturing { get; private set; }
        private string key;
        private bool capturing;
        public event Action<string> Changed;

        public HudKeyBind(string key)
        {
            this.key = key;
            Hud.Strip(this, "hud-keybind");
            clicked += Begin;
            RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            RegisterCallback<FocusOutEvent>(_ => End());
            RegisterCallback<DetachFromPanelEvent>(_ => End());
            text = key;
        }

        public void SetValueWithoutNotify(string newKey)
        {
            key = newKey;
            if (!capturing) text = key;
        }

        private void Begin()
        {
            if (capturing || ConsumedFrame == Time.frameCount) return;
            capturing = Capturing = true;
            AddToClassList("is-capturing");
            text = "НАЖМИТЕ КЛАВИШУ";
        }

        private void End()
        {
            if (!capturing) return;
            capturing = Capturing = false;
            RemoveFromClassList("is-capturing");
            text = key;
        }

        private void OnKey(KeyDownEvent evt)
        {
            if (!capturing || evt.keyCode == KeyCode.None) return;
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
            ConsumedFrame = Time.frameCount;
            if (evt.keyCode != KeyCode.Escape)
            {
                key = Hud.KeyName(evt.keyCode);
                Changed?.Invoke(key);
            }
            End();
        }
    }

    // Full-screen dimmed layer with an animated window.
    public sealed class HudModal
    {
        public readonly VisualElement Layer;
        public readonly VisualElement Window;
        public bool IsOpen { get; private set; }

        public HudModal(string name)
        {
            Layer = Hud.Box("hud-modal", name);
            Window = Hud.Box("hud-modal__window");
            Window.Add(new HudFrame(18, 18));
            Layer.Add(Window);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            Layer.pickingMode = PickingMode.Position;
            Layer.style.display = DisplayStyle.Flex;
            Layer.BringToFront();
            Layer.schedule.Execute(() => { if (IsOpen) Layer.AddToClassList("is-open"); }).StartingIn(16);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            // Let clicks through the dimmer while it fades out.
            Layer.pickingMode = PickingMode.Ignore;
            Layer.RemoveFromClassList("is-open");
            Layer.schedule.Execute(() => { if (!IsOpen) Layer.style.display = DisplayStyle.None; }).StartingIn(200);
        }
    }
}
