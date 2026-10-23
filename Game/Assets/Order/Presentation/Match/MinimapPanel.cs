using System;
using System.Collections.Generic;
using System.Linq;
using Order.Content;
using Order.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Minimap module: terrain/fog texture from the host, a vector overlay (camera frame, worker, route,
    // pins, coordinate grid) and a tool rail. Left click/drag moves the camera, right click orders the worker.
    public sealed class MinimapPanel
    {
        // As in StarCraft, the minimap shows sides, not factions: own units and buildings are always green, enemies red,
        // whatever the races and the HUD theme; whatever is selected is white. Metal is a neutral blue and plasma its own
        // purple, so neither resource reads as a side.
        public static readonly Color Own = new Color(.3f, .92f, .38f), Enemy = new Color(1f, .27f, .22f), Selected = Color.white,
            Metal = new Color(.35f, .75f, 1f), Plasma = new Color(.85f, .42f, 1f);
        public readonly VisualElement Root;
        public bool GridVisible { get; private set; } = true;
        private readonly IMatchHost host;
        private readonly Image image;
        private readonly MinimapOverlay overlay;
        private readonly VisualElement map, labels, pinLabels;
        private readonly Label readout;
        private readonly Button pinTool, gridTool;
        private readonly int size;

        public MinimapPanel(IMatchHost host)
        {
            this.host = host;
            size = TempleMap.Size;
            Root = Hud.Box("match-minimap");
            Root.AddToClassList("match-hit");
            Root.Add(new HudFrame(12, 0, 0, 12));
            map = Hud.Box("match-minimap__map");
            image = new Image { image = host.MinimapTexture, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            image.AddToClassList("match-minimap__image");
            map.Add(image);
            overlay = new MinimapOverlay(host, this);
            map.Add(overlay);
            labels = Hud.Box("match-minimap__labels"); labels.pickingMode = PickingMode.Ignore;
            var squares = MapGrid.Squares(size);
            for (var i = 0; i < squares; i++)
            {
                var letter = Hud.Text(MapGrid.Letter(i), "match-minimap__letter");
                letter.style.left = Length.Percent((i + .5f) * 100f / squares);
                labels.Add(letter);
                var number = Hud.Text((i + 1).ToString(), "match-minimap__number");
                number.style.top = Length.Percent((i + .5f) * 100f / squares);
                labels.Add(number);
            }
            map.Add(labels);
            pinLabels = Hud.Box("match-minimap__labels"); pinLabels.pickingMode = PickingMode.Ignore;
            map.Add(pinLabels);
            readout = Hud.Text("", "match-minimap__readout");
            map.Add(readout);
            map.RegisterCallback<PointerDownEvent>(OnDown);
            map.RegisterCallback<PointerMoveEvent>(OnMove);
            map.RegisterCallback<PointerUpEvent>(evt => { if (map.HasPointerCapture(evt.pointerId)) map.ReleasePointer(evt.pointerId); });
            map.RegisterCallback<PointerLeaveEvent>(_ => { overlay.Hover = null; readout.text = ""; overlay.MarkDirtyRepaint(); });
            Root.Add(map);

            var rail = Hud.Box("match-minimap__rail");
            pinTool = Tool(rail, HudIconKind.Pin, () => host.SetMode(host.Mode == MatchMode.Ping ? MatchMode.None : MatchMode.Ping));
            gridTool = Tool(rail, HudIconKind.Grid, () => SetGrid(!GridVisible));
            Tool(rail, HudIconKind.Base, host.FocusBase);
            Tool(rail, HudIconKind.Target, host.FocusSelection);
            Tool(rail, HudIconKind.ClearPins, host.ClearPins);
            Root.Add(rail);
            SetGrid(true);
        }

        public event Action<string> Hovered;

        public void SetGrid(bool visible)
        {
            GridVisible = visible;
            labels.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            gridTool.EnableInClassList("is-active", visible);
            overlay.MarkDirtyRepaint();
        }

        public void Refresh()
        {
            pinTool.EnableInClassList("is-active", host.Mode == MatchMode.Ping);
            while (pinLabels.childCount < host.Pins.Count) pinLabels.Add(Hud.Text("", "match-minimap__pin-label"));
            for (var i = 0; i < pinLabels.childCount; i++)
            {
                var label = (Label)pinLabels[i];
                var visible = i < host.Pins.Count;
                label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                label.text = (i + 1).ToString();
                label.style.left = Length.Percent(host.Pins[i].x / size * 100);
                label.style.top = Length.Percent((1 - host.Pins[i].y / size) * 100);
            }
            overlay.MarkDirtyRepaint();
        }

        private Button Tool(VisualElement rail, HudIconKind icon, Action action)
        {
            var button = Hud.IconButton(icon, action);
            button.AddToClassList("match-minimap__tool");
            rail.Add(button);
            return button;
        }

        private Vector2 ToWorld(Vector2 panelPosition)
        {
            var local = map.WorldToLocal(panelPosition);
            var rect = map.contentRect;
            return new Vector2(Mathf.Clamp(local.x / rect.width * size, .5f, size - .5f), Mathf.Clamp((1 - local.y / rect.height) * size, .5f, size - .5f));
        }

        private void OnDown(PointerDownEvent evt)
        {
            var point = ToWorld(evt.position);
            if (evt.button == 1) { host.OrderTo(point); evt.StopPropagation(); return; }
            if (evt.button != 0) return;
            if (host.Mode == MatchMode.Ping || evt.altKey) host.AddPin(point);
            else if (host.Mode == MatchMode.Move || host.Mode == MatchMode.Harvest || host.Mode == MatchMode.Attack) host.OrderTo(point);
            else { host.Focus(point); map.CapturePointer(evt.pointerId); }
            evt.StopPropagation();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            var point = ToWorld(evt.position);
            overlay.Hover = point;
            readout.text = MapGrid.Name(point.x, point.y, size);
            Hovered?.Invoke(readout.text);
            if (map.HasPointerCapture(evt.pointerId)) host.Focus(point);
            overlay.MarkDirtyRepaint();
        }

        // Vector layer above the texture.
        private sealed class MinimapOverlay : HudVector
        {
            private readonly IMatchHost host;
            private readonly MinimapPanel owner;
            public Vector2? Hover;

            public MinimapOverlay(IMatchHost host, MinimapPanel panel)
            {
                this.host = host; owner = panel;
                AddToClassList("match-minimap__overlay");
                schedule.Execute(MarkDirtyRepaint).Every(50);
            }

            protected override void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                if (r.width < 10 || host.Match == null) return;
                var size = TempleMap.Size;
                Vector2 M(float x, float z) => new Vector2(r.x + x / size * r.width, r.y + (1 - z / size) * r.height);
                var p = context.painter2D;
                var squares = MapGrid.Squares(size);
                if (owner.GridVisible)
                {
                    p.lineWidth = 1; p.strokeColor = HudPaint.Alpha(Hot, .22f); p.BeginPath();
                    for (var i = 1; i < squares; i++)
                    {
                        var t = i * MapGrid.SquareSize;
                        p.MoveTo(M(t, 0)); p.LineTo(M(t, size));
                        p.MoveTo(M(0, t)); p.LineTo(M(size, t));
                    }
                    p.Stroke();
                }
                if (Hover.HasValue)
                {
                    // Detailed 3×3 split only for the hovered square (COMMANDS.md).
                    var column = Mathf.Floor(Hover.Value.x / MapGrid.SquareSize) * MapGrid.SquareSize;
                    var row = Mathf.Floor(Hover.Value.y / MapGrid.SquareSize) * MapGrid.SquareSize;
                    var third = MapGrid.SquareSize / 3;
                    p.strokeColor = HudPaint.Alpha(Accent, .55f); p.BeginPath();
                    for (var i = 1; i < 3; i++)
                    {
                        p.MoveTo(M(column + i * third, row)); p.LineTo(M(column + i * third, row + MapGrid.SquareSize));
                        p.MoveTo(M(column, row + i * third)); p.LineTo(M(column + MapGrid.SquareSize, row + i * third));
                    }
                    p.Stroke();
                    p.strokeColor = Accent; p.BeginPath();
                    var a = M(column, row); var b = M(column + MapGrid.SquareSize, row + MapGrid.SquareSize);
                    p.MoveTo(a); p.LineTo(new Vector2(b.x, a.y)); p.LineTo(b); p.LineTo(new Vector2(a.x, b.y)); p.ClosePath(); p.Stroke();
                }

                var match = host.Match;
                foreach (var unit in host.Selection.Orderable)
                {
                    var route = unit.Route.ToList();
                    if (route.Count == 0) continue;
                    p.lineWidth = 1.5f; p.strokeColor = HudPaint.Alpha(Hot, .85f); p.BeginPath();
                    p.MoveTo(M(unit.X, unit.Z));
                    foreach (var point in route) p.LineTo(M(point.X, point.Z));
                    p.Stroke();
                    var end = M(route[route.Count - 1].X, route[route.Count - 1].Z);
                    p.BeginPath(); HudPaint.Polygon(p, end, 4, 4, 45); p.Stroke();
                }

                var frame = host.ViewportOnGround();
                if (frame != null)
                {
                    p.lineWidth = 1.5f; p.strokeColor = HudPaint.Alpha(Hot, .9f); p.BeginPath();
                    p.MoveTo(M(frame[0].x, frame[0].y));
                    for (var i = 1; i < frame.Length; i++) p.LineTo(M(frame[i].x, frame[i].y));
                    p.ClosePath(); p.Stroke();
                }

                for (var i = 0; i < host.Pins.Count; i++)
                {
                    var pin = M(host.Pins[i].x, host.Pins[i].y);
                    var pulse = 1 + .25f * Mathf.Sin(Time.realtimeSinceStartup * 4 + i);
                    p.fillColor = HudPaint.Alpha(Warn, .3f); p.strokeColor = Warn; p.lineWidth = 1.5f;
                    p.BeginPath(); HudPaint.Polygon(p, pin, 5 * pulse, 4, 0); p.Fill(); p.Stroke();
                }

                // The machines' network coverage, while it shows on the ground too; a cut-off relay is grey.
                if (host.CoverageShown)
                {
                    p.lineWidth = 1;
                    foreach (var b in match.Buildings)
                    {
                        if (b.Owner != PrototypeMatch.Player || b.Stats.Coverage <= 0 || b.Ready < 1) continue;
                        var center = M(b.X + b.Width * .5f, b.Z + b.Width * .5f);
                        p.strokeColor = b.Powered ? HudPaint.Alpha(Accent, .7f) : new Color(.55f, .55f, .6f, .5f);
                        p.BeginPath(); HudPaint.Circle(p, center, b.Stats.Coverage / size * r.width); p.Stroke();
                    }
                }

                // Attacks off screen: a red ring widening from the spot, three times over the alert's life.
                var now = Time.unscaledTime;
                foreach (var (point, time) in host.Alerts)
                {
                    var age = (now - time) / TemplePrototypeView.AlertSeconds;
                    if (age < 0 || age >= 1) continue;
                    var at = M(point.x, point.y); var wave = age * 3 % 1;
                    p.strokeColor = HudPaint.Alpha(Enemy, (1 - wave) * (1 - age * .5f)); p.lineWidth = 2;
                    p.BeginPath(); HudPaint.Circle(p, at, 4 + wave * 14); p.Stroke();
                    p.fillColor = HudPaint.Alpha(Enemy, .9f * (1 - age)); p.BeginPath(); HudPaint.Circle(p, at, 3); p.Fill();
                }

                // Ground units are dots, air units diamonds. Own green, enemies red and only in sight; selected units are white
                // and drawn last, on top of the rest.
                foreach (var unit in match.Units) if (!host.Selection.Contains(unit)) Dot(p, match, unit, M);
                foreach (var unit in host.Selection.Units) Dot(p, match, unit, M);
            }
            private void Dot(Painter2D p, PrototypeMatch match, PrototypeUnit unit, Func<float, float, Vector2> M)
            {
                var own = unit.Owner == PrototypeMatch.Player;
                if (!own && !match.SeenBy(PrototypeMatch.Player, unit)) return;
                var at = M(unit.X, unit.Z);
                p.fillColor = host.Selection.Contains(unit) ? Selected : own ? Own : Enemy; p.BeginPath();
                if (unit.Stats.Flying) HudPaint.Polygon(p, at, 3.4f, 4, 0); else HudPaint.Circle(p, at, unit.Kind == UnitKind.Worker ? 2.2f : 2.6f);
                p.Fill();
            }
        }
    }
}
