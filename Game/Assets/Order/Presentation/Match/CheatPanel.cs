#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Order.Content;
using Order.Simulation;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Editor-only cheat menu of the test ground: the "ЧИТЫ" button (F9) in the top bar opens a panel of buttons for
    // resources, fast construction and training, unlimited supply, the whole map, invulnerability, an instant win, the
    // enemy bot (on or off, or an attack wave right now) and
    // units of either side in the middle of the view. Builds of the game have neither the button nor the panel.
    sealed class CheatPanel
    {
        readonly IMatchHost host;
        readonly Action refresh;
        readonly VisualElement panel;
        readonly Button toggle, fast, supply, map, immortal, bot;
        readonly List<Button> counts = new List<Button>();
        static readonly int[] Counts = { 1, 5, 10 };
        int count = 5;
        public bool IsOpen => panel.ClassListContains("is-open");

        public CheatPanel(VisualElement screen, VisualElement topLeft, IMatchHost host, Action refresh)
        {
            this.host = host; this.refresh = refresh;
            toggle = Hud.Button("ЧИТЫ", Toggle, "hud-button--ghost"); toggle.Add(Hud.Keycap("F9"));
            topLeft.Insert(2, toggle);
            panel = Hud.Panel("ЧИТЫ ПОЛИГОНА", "EDITOR", out var body);
            panel.AddToClassList("match-cheats"); panel.AddToClassList("match-hit");
            var match = Row(body, "МАТЧ");
            match.Add(Hud.Button("+10 000", () => Run(m => m.AddResources(10000, 10000))));
            fast = Add(match, $"СТРОЙКА ×{PrototypeMatch.FastFactor:0}", m => m.ToggleFastBuild());
            supply = Add(match, "БЕЗ ЛИМИТА", m => m.ToggleNoSupply());
            var world = Row(body, "МИР");
            map = Add(world, "ВСЯ КАРТА", m => m.ToggleReveal());
            immortal = Add(world, "БЕССМЕРТИЕ", m => m.ToggleInvulnerable());
            world.Add(Hud.Button("ПОБЕДА", () => Run(m => m.WinNow())));
            var enemy = Row(body, "БОТ");
            bot = Add(enemy, "ВКЛЮЧЁН", m => m.ToggleBot());
            enemy.Add(Hud.Button("ВОЛНА СЕЙЧАС", () => Run(m => m.SendWaveNow())));
            var amount = Row(body, "СКОЛЬКО");
            foreach (var n in Counts) { var value = n; var button = Hud.Button("×" + n, () => { count = value; refresh(); }); amount.Add(button); counts.Add(button); }
            Spawn(Row(body, "СВОИ"), PrototypeMatch.Player);
            Spawn(Row(body, "ВРАГ"), PrototypeMatch.Enemy);
            body.Add(Hud.Text("Только в редакторе. Юниты появляются в центре экрана, свои сразу выделяются.", "match-cheats__hint"));
            screen.Add(panel);
        }
        public void Toggle() { panel.EnableInClassList("is-open", !IsOpen); refresh(); }
        public void Close() { panel.RemoveFromClassList("is-open"); refresh(); }
        public void Refresh()
        {
            var match = host.Match;
            if (match == null) return;
            toggle.EnableInClassList("is-active", IsOpen);
            fast.EnableInClassList("is-active", match.FastBuild);
            supply.EnableInClassList("is-active", match.NoSupply);
            map.EnableInClassList("is-active", match.Revealed);
            immortal.EnableInClassList("is-active", match.Invulnerable);
            bot.EnableInClassList("is-active", match.BotEnabled);
            for (int i = 0; i < counts.Count; i++) counts[i].EnableInClassList("is-active", Counts[i] == count);
        }

        static VisualElement Row(VisualElement body, string caption)
        {
            var row = Hud.Box("match-cheats__row");
            row.Add(Hud.Text(caption, "match-cheats__label"));
            body.Add(row);
            return row;
        }
        Button Add(VisualElement row, string title, Action<PrototypeMatch> action)
        {
            var button = Hud.Button(title, () => Run(action));
            row.Add(button);
            return button;
        }
        void Spawn(VisualElement row, int owner)
        {
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind)))
            {
                var unit = kind;
                row.Add(Hud.Button(host.Match.Balance.Unit(kind).Name.ToUpperInvariant(), () =>
                {
                    var at = host.CameraFocus;
                    var units = host.Match.SpawnSquad(unit, count, at.x, at.y, owner);
                    if (owner == PrototypeMatch.Player && units.Count > 0) host.Selection.Set(units);
                    refresh();
                }));
            }
        }
        void Run(Action<PrototypeMatch> action) { if (host.Match != null) action(host.Match); refresh(); }
    }
}
#endif
