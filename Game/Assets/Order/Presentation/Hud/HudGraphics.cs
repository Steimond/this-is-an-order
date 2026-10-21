using Order.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Chamfered panel/button outline with heavier accents on the cut corners.
    public sealed class HudFrame : HudVector
    {
        private readonly float topLeft, topRight, bottomRight, bottomLeft;
        public float StrokeWidth = 1f;
        public bool CornerAccents = true;

        public HudFrame(float topLeft = 10, float bottomRight = 10, float topRight = 0, float bottomLeft = 0)
        {
            this.topLeft = topLeft; this.topRight = topRight; this.bottomRight = bottomRight; this.bottomLeft = bottomLeft;
            AddToClassList("hud-frame");
        }

        protected override void Draw(MeshGenerationContext context)
        {
            var bounds = contentRect;
            if (bounds.width < 4 || bounds.height < 4) return;
            var half = StrokeWidth * .5f;
            var r = new Rect(bounds.x + half, bounds.y + half, bounds.width - StrokeWidth, bounds.height - StrokeWidth);
            var painter = context.painter2D;
            var fill = Fill; var stroke = Stroke;
            HudPaint.Chamfer(painter, r, topLeft, topRight, bottomRight, bottomLeft);
            if (fill.a > .001f) { painter.fillColor = fill; painter.Fill(); }
            if (stroke.a <= .001f || StrokeWidth <= 0) return;
            painter.lineJoin = LineJoin.Miter;
            painter.lineWidth = StrokeWidth; painter.strokeColor = stroke; painter.Stroke();
            if (!CornerAccents) return;
            painter.lineWidth = StrokeWidth + 1.5f;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            const float tail = 9;
            if (topLeft > 0)
            {
                painter.MoveTo(new Vector2(r.xMin, r.yMin + topLeft + tail)); painter.LineTo(new Vector2(r.xMin, r.yMin + topLeft));
                painter.LineTo(new Vector2(r.xMin + topLeft, r.yMin)); painter.LineTo(new Vector2(r.xMin + topLeft + tail, r.yMin));
            }
            if (bottomRight > 0)
            {
                painter.MoveTo(new Vector2(r.xMax, r.yMax - bottomRight - tail)); painter.LineTo(new Vector2(r.xMax, r.yMax - bottomRight));
                painter.LineTo(new Vector2(r.xMax - bottomRight, r.yMax)); painter.LineTo(new Vector2(r.xMax - bottomRight - tail, r.yMax));
            }
            painter.Stroke();
        }
    }

    public enum HudIconKind { Gear, Power, Mic, ChevronLeft, ChevronRight, Close, Lock, Back, Check, Grid }

    public sealed class HudIcon : HudVector
    {
        private HudIconKind kind;
        public HudIconKind Kind { get => kind; set { kind = value; MarkDirtyRepaint(); } }
        public HudIcon(HudIconKind kind) { this.kind = kind; AddToClassList("hud-icon"); }

        protected override void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            var size = Mathf.Min(r.width, r.height);
            if (size < 4) return;
            var origin = new Vector2(r.x + (r.width - size) / 2, r.y + (r.height - size) / 2);
            Vector2 P(float x, float y) => origin + new Vector2(x, y) * size;
            var center = P(.5f, .5f);
            var p = context.painter2D;
            p.strokeColor = Stroke; p.fillColor = Stroke;
            p.lineWidth = Mathf.Max(1.3f, size * .085f);
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            p.BeginPath();
            switch (kind)
            {
                case HudIconKind.Gear:
                    HudPaint.Circle(p, center, size * .2f); p.Stroke();
                    p.BeginPath();
                    for (var i = 0; i < 8; i++) { p.MoveTo(HudPaint.Polar(center, size * .26f, i * 45 + 22.5f)); p.LineTo(HudPaint.Polar(center, size * .42f, i * 45 + 22.5f)); }
                    p.lineCap = LineCap.Butt; p.lineWidth = size * .13f; p.Stroke();
                    return;
                case HudIconKind.Power:
                    HudPaint.Arc(p, center + new Vector2(0, size * .04f), size * .32f, -55, 235);
                    p.MoveTo(P(.5f, .1f)); p.LineTo(P(.5f, .46f));
                    break;
                case HudIconKind.Mic:
                    p.MoveTo(P(.38f, .26f)); p.Arc(P(.5f, .26f), size * .12f, Angle.Degrees(180), Angle.Degrees(360));
                    p.LineTo(P(.62f, .44f)); p.Arc(P(.5f, .44f), size * .12f, Angle.Degrees(0), Angle.Degrees(180));
                    p.ClosePath();
                    HudPaint.Arc(p, P(.5f, .46f), size * .22f, 0, 180);
                    p.MoveTo(P(.5f, .68f)); p.LineTo(P(.5f, .84f));
                    p.MoveTo(P(.36f, .86f)); p.LineTo(P(.64f, .86f));
                    break;
                case HudIconKind.ChevronLeft: p.MoveTo(P(.62f, .2f)); p.LineTo(P(.36f, .5f)); p.LineTo(P(.62f, .8f)); break;
                case HudIconKind.ChevronRight: p.MoveTo(P(.38f, .2f)); p.LineTo(P(.64f, .5f)); p.LineTo(P(.38f, .8f)); break;
                case HudIconKind.Close: p.MoveTo(P(.26f, .26f)); p.LineTo(P(.74f, .74f)); p.MoveTo(P(.74f, .26f)); p.LineTo(P(.26f, .74f)); break;
                case HudIconKind.Lock:
                    p.MoveTo(P(.26f, .46f)); p.LineTo(P(.74f, .46f)); p.LineTo(P(.74f, .84f)); p.LineTo(P(.26f, .84f)); p.ClosePath();
                    p.MoveTo(P(.36f, .46f)); p.LineTo(P(.36f, .32f)); p.Arc(P(.5f, .32f), size * .14f, Angle.Degrees(180), Angle.Degrees(360)); p.LineTo(P(.64f, .46f));
                    break;
                case HudIconKind.Back: p.MoveTo(P(.76f, .5f)); p.LineTo(P(.24f, .5f)); p.MoveTo(P(.44f, .3f)); p.LineTo(P(.24f, .5f)); p.LineTo(P(.44f, .7f)); break;
                case HudIconKind.Check: p.MoveTo(P(.22f, .52f)); p.LineTo(P(.42f, .72f)); p.LineTo(P(.8f, .3f)); break;
                case HudIconKind.Grid:
                    for (var i = 0; i < 3; i++) { var t = .2f + i * .3f; p.MoveTo(P(t, .2f)); p.LineTo(P(t, .8f)); p.MoveTo(P(.2f, t)); p.LineTo(P(.8f, t)); }
                    break;
            }
            p.Stroke();
        }
    }

    public sealed class HudTicks : HudVector
    {
        private readonly int count;
        public HudTicks(int count) { this.count = count; AddToClassList("hud-slider__ticks"); }

        protected override void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 8) return;
            var p = context.painter2D;
            p.lineWidth = 1; p.strokeColor = Stroke; p.BeginPath();
            for (var i = 1; i < count; i++)
            {
                var x = Mathf.Round(r.x + r.width * i / count) + .5f;
                p.MoveTo(new Vector2(x, r.y)); p.LineTo(new Vector2(x, r.yMax));
            }
            p.Stroke();
        }
    }

    // Faction mark: engineered hexagon or organic network. Shape language, not colour, tells them apart.
    public sealed class FactionEmblem : HudVector
    {
        private FactionId faction;
        public FactionId Faction { get => faction; set { faction = value; MarkDirtyRepaint(); } }
        public FactionEmblem(FactionId faction) { this.faction = faction; AddToClassList("faction-emblem"); }

        protected override void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            var size = Mathf.Min(r.width, r.height);
            if (size < 8) return;
            var c = r.center;
            var p = context.painter2D;
            var stroke = Stroke;
            p.strokeColor = stroke; p.lineWidth = Mathf.Max(1.2f, size * .045f);
            p.lineJoin = LineJoin.Miter; p.lineCap = LineCap.Butt;
            if (faction == FactionId.Expedition)
            {
                p.BeginPath(); HudPaint.Polygon(p, c, size * .44f, 6, -90);
                p.fillColor = HudPaint.Alpha(stroke, .08f); p.Fill(); p.Stroke();
                p.BeginPath(); HudPaint.Polygon(p, c, size * .3f, 6, -90); p.strokeColor = HudPaint.Alpha(stroke, .45f); p.Stroke();
                p.strokeColor = stroke; p.lineWidth = Mathf.Max(1.5f, size * .07f); p.BeginPath();
                for (var i = 0; i < 2; i++)
                {
                    var y = c.y + size * (.02f + i * .15f);
                    p.MoveTo(new Vector2(c.x - size * .17f, y + size * .08f)); p.LineTo(new Vector2(c.x, y - size * .07f)); p.LineTo(new Vector2(c.x + size * .17f, y + size * .08f));
                }
                p.Stroke();
            }
            else
            {
                p.BeginPath();
                for (var i = 0; i <= 48; i++)
                {
                    var a = i * 7.5f;
                    var radius = size * .42f * (1 + .06f * Mathf.Sin(a * 3 * Mathf.Deg2Rad) + .04f * Mathf.Sin(a * 5 * Mathf.Deg2Rad + 1));
                    var point = HudPaint.Polar(c, radius, a);
                    if (i == 0) p.MoveTo(point); else p.LineTo(point);
                }
                p.ClosePath();
                p.fillColor = HudPaint.Alpha(stroke, .08f); p.Fill(); p.Stroke();
                p.lineWidth = Mathf.Max(1.2f, size * .04f); p.strokeColor = HudPaint.Alpha(stroke, .7f); p.BeginPath();
                for (var i = 0; i < 3; i++)
                {
                    var node = HudPaint.Polar(c, size * .22f, -90 + i * 120);
                    p.MoveTo(c); p.QuadraticCurveTo(HudPaint.Polar(c, size * .16f, -60 + i * 120), node);
                }
                p.Stroke();
                p.fillColor = stroke; p.BeginPath();
                for (var i = 0; i < 3; i++) HudPaint.Circle(p, HudPaint.Polar(c, size * .22f, -90 + i * 120), size * .06f);
                HudPaint.Circle(p, c, size * .08f);
                p.Fill();
            }
        }
    }

    // Schematic of a 1v1 map: plateaus, central basin contours, ramps and start markers.
    public sealed class MapPreview : HudVector
    {
        public MapPreview() { AddToClassList("map-preview"); }

        protected override void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 40 || r.height < 40) return;
            var p = context.painter2D;
            var size = Mathf.Min(r.width, r.height) - 2;
            var area = new Rect(r.center.x - size / 2, r.center.y - size / 2, size, size);
            var c = area.center;
            p.lineWidth = 1; p.strokeColor = HudPaint.Alpha(Line, .35f); p.BeginPath();
            for (var i = 1; i < 8; i++)
            {
                var x = area.x + area.width * i / 8; var y = area.y + area.height * i / 8;
                p.MoveTo(new Vector2(x, area.yMin)); p.LineTo(new Vector2(x, area.yMax));
                p.MoveTo(new Vector2(area.xMin, y)); p.LineTo(new Vector2(area.xMax, y));
            }
            p.Stroke();
            for (var ring = 0; ring < 3; ring++)
            {
                p.strokeColor = HudPaint.Alpha(Accent, .2f + ring * .15f); p.lineWidth = 1; p.BeginPath();
                var radius = size * (.3f - ring * .08f);
                for (var i = 0; i <= 64; i++)
                {
                    var a = i * 360f / 64;
                    var point = HudPaint.Polar(c, radius * (1 + .08f * Mathf.Sin((a * 2 + ring * 40) * Mathf.Deg2Rad) + .05f * Mathf.Sin((a * 5 - ring * 25) * Mathf.Deg2Rad)), a);
                    if (i == 0) p.MoveTo(point); else p.LineTo(point);
                }
                p.ClosePath(); p.Stroke();
            }
            var plateau = size * .26f;
            for (var corner = 0; corner < 4; corner++)
            {
                var right = corner == 1 || corner == 2; var bottom = corner >= 2;
                var rect = new Rect(right ? area.xMax - plateau : area.xMin, bottom ? area.yMax - plateau : area.yMin, plateau, plateau);
                // Cut the corner that faces the basin.
                HudPaint.Chamfer(p, rect, right && bottom ? 10 : 0, !right && bottom ? 10 : 0, !right && !bottom ? 10 : 0, right && !bottom ? 10 : 0);
                p.fillColor = HudPaint.Alpha(Line, .18f); p.Fill();
                p.strokeColor = HudPaint.Alpha(Line, .9f); p.Stroke();
                var inner = new Vector2(right ? rect.xMin : rect.xMax, bottom ? rect.yMin : rect.yMax);
                var dir = (c - inner).normalized;
                p.lineWidth = 2; p.strokeColor = HudPaint.Alpha(Muted, .8f); p.BeginPath();
                p.MoveTo(inner); p.LineTo(inner + dir * size * .07f); p.Stroke(); p.lineWidth = 1;
            }
            var player = new Vector2(area.xMin + plateau * .45f, area.yMin + plateau * .45f);
            var enemy = new Vector2(area.xMax - plateau * .45f, area.yMax - plateau * .45f);
            p.fillColor = Accent; p.BeginPath(); HudPaint.Polygon(p, player, 6, 4, 45); p.Fill();
            p.strokeColor = Accent; p.BeginPath(); HudPaint.Circle(p, player, 11); p.Stroke();
            p.fillColor = Warn; p.BeginPath(); HudPaint.Polygon(p, enemy, 7, 4, 0); p.Fill();
            p.strokeColor = Warn; p.BeginPath(); HudPaint.Circle(p, enemy, 11); p.Stroke();
            p.strokeColor = Line; p.lineWidth = 1; p.BeginPath();
            p.MoveTo(area.position); p.LineTo(new Vector2(area.xMax, area.yMin)); p.LineTo(area.max); p.LineTo(new Vector2(area.xMin, area.yMax)); p.ClosePath();
            p.Stroke();
        }
    }

    // Static backdrop: blueprint grid, edge rulers, corner brackets and vignette.
    public sealed class HudBackdrop : HudVector
    {
        public HudBackdrop()
        {
            AddToClassList("hud-backdrop");
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        protected override void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 16 || r.height < 16) return;
            var p = context.painter2D;
            const float minor = 40, major = 160;
            p.lineWidth = 1; p.strokeColor = HudPaint.Alpha(Line, .09f); p.BeginPath();
            for (var x = minor; x < r.width; x += minor) { p.MoveTo(new Vector2(x + .5f, 0)); p.LineTo(new Vector2(x + .5f, r.height)); }
            for (var y = minor; y < r.height; y += minor) { p.MoveTo(new Vector2(0, y + .5f)); p.LineTo(new Vector2(r.width, y + .5f)); }
            p.Stroke();
            p.strokeColor = HudPaint.Alpha(Line, .4f); p.BeginPath();
            for (var x = major; x < r.width; x += major)
                for (var y = major; y < r.height; y += major)
                {
                    p.MoveTo(new Vector2(x - 4, y + .5f)); p.LineTo(new Vector2(x + 5, y + .5f));
                    p.MoveTo(new Vector2(x + .5f, y - 4)); p.LineTo(new Vector2(x + .5f, y + 5));
                }
            p.Stroke();
            p.strokeColor = HudPaint.Alpha(Line, .5f); p.BeginPath();
            for (var y = 12f; y < r.height - 12; y += 8)
            {
                var length = Mathf.RoundToInt(y / 8) % 5 == 0 ? 8 : 4;
                p.MoveTo(new Vector2(4, y)); p.LineTo(new Vector2(4 + length, y));
                p.MoveTo(new Vector2(r.width - 4, y)); p.LineTo(new Vector2(r.width - 4 - length, y));
            }
            p.Stroke();
            p.lineWidth = 2; p.strokeColor = HudPaint.Alpha(Accent, .55f); p.BeginPath();
            const float arm = 28, inset = 10;
            p.MoveTo(new Vector2(inset, inset + arm)); p.LineTo(new Vector2(inset, inset)); p.LineTo(new Vector2(inset + arm, inset));
            p.MoveTo(new Vector2(r.width - inset - arm, inset)); p.LineTo(new Vector2(r.width - inset, inset)); p.LineTo(new Vector2(r.width - inset, inset + arm));
            p.MoveTo(new Vector2(r.width - inset, r.height - inset - arm)); p.LineTo(new Vector2(r.width - inset, r.height - inset)); p.LineTo(new Vector2(r.width - inset - arm, r.height - inset));
            p.MoveTo(new Vector2(inset + arm, r.height - inset)); p.LineTo(new Vector2(inset, r.height - inset)); p.LineTo(new Vector2(inset, r.height - inset - arm));
            p.Stroke();
            var dark = HudPaint.Alpha(Background, .92f); var clear = HudPaint.Alpha(Background, 0);
            var edgeX = r.width * .16f; var edgeY = r.height * .2f;
            HudPaint.Gradient(context, new Rect(0, 0, r.width, edgeY), dark, dark, clear, clear);
            HudPaint.Gradient(context, new Rect(0, r.height - edgeY, r.width, edgeY), clear, clear, dark, dark);
            HudPaint.Gradient(context, new Rect(0, 0, edgeX, r.height), dark, clear, clear, dark);
            HudPaint.Gradient(context, new Rect(r.width - edgeX, 0, edgeX, r.height), clear, dark, dark, clear);
        }
    }

    // Slow horizontal scan band moved with a translate, so the backdrop itself never re-tessellates.
    public sealed class HudScanline : HudVector
    {
        private const float Height = 140;
        public HudScanline()
        {
            AddToClassList("hud-scanline");
            style.position = Position.Absolute; style.left = 0; style.right = 0; style.top = 0; style.height = Height;
            schedule.Execute(Move).Every(16);
        }

        private void Move()
        {
            var range = (parent?.resolvedStyle.height ?? 800) + Height;
            var y = Time.realtimeSinceStartup * 46f % range - Height;
            style.translate = new Translate(0, y);
        }

        protected override void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            var clear = HudPaint.Alpha(Accent, 0); var tint = HudPaint.Alpha(Accent, .045f);
            HudPaint.Gradient(context, new Rect(r.x, r.y, r.width, r.height * .85f), clear, clear, tint, tint);
            HudPaint.Gradient(context, new Rect(r.x, r.y + r.height * .85f, r.width, r.height * .15f), tint, tint, clear, clear);
            context.painter2D.lineWidth = 1; context.painter2D.strokeColor = HudPaint.Alpha(Accent, .12f);
            context.painter2D.BeginPath(); context.painter2D.MoveTo(new Vector2(r.x, r.y + r.height * .85f)); context.painter2D.LineTo(new Vector2(r.xMax, r.y + r.height * .85f)); context.painter2D.Stroke();
        }
    }

    // Small animated bar readout, purely decorative telemetry.
    public sealed class HudTelemetry : HudVector
    {
        private readonly int bars;
        private readonly float seed;
        public HudTelemetry(int bars, float seed)
        {
            this.bars = bars; this.seed = seed;
            AddToClassList("hud-telemetry");
            schedule.Execute(MarkDirtyRepaint).Every(80);
        }

        protected override void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 8 || r.height < 4) return;
            var p = context.painter2D;
            var t = Time.realtimeSinceStartup;
            var step = r.width / bars;
            p.lineCap = LineCap.Butt; p.lineWidth = Mathf.Max(1, step * .55f);
            p.strokeColor = Stroke; p.BeginPath();
            for (var i = 0; i < bars; i++)
            {
                var level = .15f + .85f * Mathf.PerlinNoise(seed + i * .37f, t * .9f);
                var x = r.x + step * (i + .5f);
                p.MoveTo(new Vector2(x, r.yMax)); p.LineTo(new Vector2(x, r.yMax - r.height * level));
            }
            p.Stroke();
        }
    }
}
