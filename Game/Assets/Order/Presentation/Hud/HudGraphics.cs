using System;
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

    public enum HudIconKind
    {
        Gear, Power, Mic, ChevronLeft, ChevronRight, Close, Lock, Back, Check, Grid,
        Worker, Headquarters, Supply, Factory, Metal, Plasma, Move, Stop, Harvest, Pin, Base, Target, Armor, Menu, ClearPins, Train, Build,
        Assault, Rifleman, Gunship, Hold, Relay, Spider, Leaper, Upgrade, Repair, Disassemble, Extractor, Tank, Howitzer, Fighter, Deploy, Emitter
    }

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
                case HudIconKind.Worker: Worker(p, P, size, 1); break;
                case HudIconKind.Train:
                    Worker(p, P, size, .8f);
                    p.MoveTo(P(.78f, .62f)); p.LineTo(P(.78f, .9f)); p.MoveTo(P(.64f, .76f)); p.LineTo(P(.92f, .76f));
                    break;
                case HudIconKind.Headquarters:
                    Shape(p, P, .14f, .84f, .86f, .84f);
                    Shape(p, P, .2f, .84f, .2f, .5f, .36f, .4f, .36f, .24f, .64f, .24f, .64f, .4f, .8f, .5f, .8f, .84f);
                    Shape(p, P, .5f, .24f, .5f, .1f);
                    Shape(p, P, .44f, .84f, .44f, .66f, .56f, .66f, .56f, .84f);
                    break;
                case HudIconKind.Supply:
                    Shape(p, P, .22f, .3f, .78f, .3f, .78f, .84f, .22f, .84f, .22f, .3f);
                    Shape(p, P, .4f, .3f, .4f, .2f, .6f, .2f, .6f, .3f);
                    Shape(p, P, .34f, .5f, .66f, .5f); Shape(p, P, .34f, .66f, .66f, .66f);
                    break;
                case HudIconKind.Relay:
                    // A mast with signal arcs on both sides.
                    Shape(p, P, .5f, .36f, .5f, .86f); Shape(p, P, .36f, .86f, .64f, .86f);
                    HudPaint.Arc(p, P(.5f, .36f), size * .14f, 200, 340); HudPaint.Arc(p, P(.5f, .36f), size * .26f, 205, 335);
                    break;
                case HudIconKind.Tank:
                    // Side view: tracks, hull, turret and a level gun.
                    Shape(p, P, .1f, .74f, .9f, .74f, .84f, .86f, .16f, .86f, .1f, .74f);
                    Shape(p, P, .16f, .74f, .2f, .6f, .82f, .6f, .86f, .74f);
                    Shape(p, P, .32f, .6f, .36f, .46f, .62f, .46f, .66f, .6f);
                    Shape(p, P, .62f, .52f, .94f, .5f);
                    break;
                case HudIconKind.Howitzer:
                    // Tracks, a tall turret at the back and a long barrel raised to fire.
                    Shape(p, P, .1f, .74f, .9f, .74f, .84f, .86f, .16f, .86f, .1f, .74f);
                    Shape(p, P, .14f, .74f, .18f, .62f, .84f, .62f, .88f, .74f);
                    Shape(p, P, .18f, .62f, .22f, .4f, .56f, .4f, .6f, .62f);
                    Shape(p, P, .52f, .46f, .9f, .14f);
                    break;
                case HudIconKind.Fighter:
                    // From above: fuselage, swept wings with a lift fan in each tip, the tail.
                    Shape(p, P, .5f, .1f, .5f, .88f);
                    Shape(p, P, .12f, .58f, .5f, .38f, .88f, .58f);
                    Shape(p, P, .36f, .88f, .5f, .78f, .64f, .88f);
                    HudPaint.Circle(p, P(.2f, .56f), size * .08f); HudPaint.Circle(p, P(.8f, .56f), size * .08f);
                    break;
                case HudIconKind.Emitter:
                    // A mast on spread legs throwing a widening beam to the right.
                    Shape(p, P, .2f, .88f, .34f, .56f, .48f, .88f);
                    Shape(p, P, .34f, .56f, .34f, .3f);
                    HudPaint.Circle(p, P(.34f, .24f), size * .08f);
                    Shape(p, P, .44f, .22f, .9f, .12f); Shape(p, P, .44f, .27f, .9f, .38f);
                    break;
                case HudIconKind.Deploy:
                    // A barrel rising over two braced feet.
                    Shape(p, P, .2f, .86f, .36f, .62f, .64f, .62f, .8f, .86f);
                    Shape(p, P, .5f, .62f, .78f, .2f);
                    Shape(p, P, .62f, .3f, .74f, .14f);
                    break;
                case HudIconKind.Repair:
                    // A wrench: the open jaw up and to the right, the handle down to the left.
                    HudPaint.Arc(p, P(.64f, .36f), size * .17f, 15, 255);
                    Shape(p, P, .52f, .48f, .2f, .8f);
                    break;
                case HudIconKind.Disassemble:
                    // A unit coming apart into nanites that drain away below it.
                    Shape(p, P, .3f, .48f, .3f, .22f, .7f, .22f, .7f, .48f);
                    foreach (var (x, y) in new[] { (.3f, .64f), (.5f, .6f), (.7f, .64f), (.4f, .8f), (.6f, .8f), (.5f, .92f) }) HudPaint.Circle(p, P(x, y), size * .035f);
                    break;
                case HudIconKind.Extractor:
                    // A rig over the rift: a tapering housing with a band, a vent stack and the ground line.
                    Shape(p, P, .12f, .86f, .88f, .86f);
                    Shape(p, P, .26f, .86f, .36f, .42f, .64f, .42f, .74f, .86f);
                    Shape(p, P, .31f, .64f, .69f, .64f);
                    Shape(p, P, .44f, .42f, .44f, .16f, .56f, .16f, .56f, .42f);
                    break;
                case HudIconKind.Upgrade:
                    // Two chevrons pointing up over a bar.
                    Shape(p, P, .26f, .5f, .5f, .26f, .74f, .5f); Shape(p, P, .26f, .7f, .5f, .46f, .74f, .7f);
                    Shape(p, P, .28f, .86f, .72f, .86f);
                    break;
                case HudIconKind.Factory:
                    Shape(p, P, .12f, .84f, .88f, .84f);
                    Shape(p, P, .16f, .84f, .16f, .46f, .34f, .58f, .34f, .46f, .52f, .58f, .52f, .46f, .7f, .58f, .7f, .2f, .84f, .2f, .84f, .84f);
                    break;
                case HudIconKind.Metal:
                    // An ingot: the front face, the top face and a glint.
                    Shape(p, P, .12f, .8f, .88f, .8f, .76f, .52f, .24f, .52f, .12f, .8f);
                    Shape(p, P, .24f, .52f, .36f, .32f, .64f, .32f, .76f, .52f);
                    Shape(p, P, .3f, .68f, .52f, .68f);
                    break;
                case HudIconKind.Plasma:
                    // A tongue of plasma with a hot core.
                    p.MoveTo(P(.5f, .1f)); p.BezierCurveTo(P(.8f, .38f), P(.84f, .64f), P(.66f, .82f));
                    p.BezierCurveTo(P(.58f, .9f), P(.42f, .9f), P(.34f, .82f)); p.BezierCurveTo(P(.16f, .64f), P(.2f, .38f), P(.5f, .1f));
                    p.MoveTo(P(.5f, .48f)); p.BezierCurveTo(P(.62f, .6f), P(.6f, .76f), P(.5f, .78f)); p.BezierCurveTo(P(.4f, .76f), P(.38f, .6f), P(.5f, .48f));
                    break;
                case HudIconKind.Move: Shape(p, P, .18f, .5f, .8f, .5f); Shape(p, P, .58f, .28f, .8f, .5f, .58f, .72f); break;
                case HudIconKind.Stop:
                    Shape(p, P, .28f, .28f, .72f, .28f, .72f, .72f, .28f, .72f, .28f, .28f);
                    p.fillColor = HudPaint.Alpha(Stroke, .25f); p.Fill();
                    break;
                case HudIconKind.Harvest:
                    Shape(p, P, .5f, .1f, .64f, .3f, .5f, .5f, .36f, .3f, .5f, .1f);
                    Shape(p, P, .5f, .5f, .5f, .7f); Shape(p, P, .4f, .62f, .5f, .72f, .6f, .62f);
                    Shape(p, P, .18f, .66f, .18f, .86f, .82f, .86f, .82f, .66f);
                    break;
                case HudIconKind.Pin:
                case HudIconKind.ClearPins:
                    p.MoveTo(P(.5f, .88f)); p.LineTo(HudPaint.Polar(P(.5f, .38f), size * .24f, 150));
                    p.Arc(P(.5f, .38f), size * .24f, Angle.Degrees(150), Angle.Degrees(390)); p.ClosePath();
                    HudPaint.Circle(p, P(.5f, .38f), size * .07f);
                    if (kind == HudIconKind.ClearPins) { p.MoveTo(P(.16f, .16f)); p.LineTo(P(.84f, .84f)); }
                    break;
                case HudIconKind.Base:
                    Shape(p, P, .14f, .5f, .5f, .18f, .86f, .5f);
                    Shape(p, P, .26f, .42f, .26f, .84f, .74f, .84f, .74f, .42f);
                    Shape(p, P, .44f, .84f, .44f, .64f, .56f, .64f, .56f, .84f);
                    break;
                case HudIconKind.Target:
                    HudPaint.Circle(p, center, size * .24f);
                    Shape(p, P, .5f, .08f, .5f, .3f); Shape(p, P, .5f, .7f, .5f, .92f); Shape(p, P, .08f, .5f, .3f, .5f); Shape(p, P, .7f, .5f, .92f, .5f);
                    break;
                case HudIconKind.Armor: Shape(p, P, .5f, .12f, .82f, .26f, .78f, .6f, .5f, .88f, .22f, .6f, .18f, .26f, .5f, .12f); break;
                case HudIconKind.Build:
                    Shape(p, P, .42f, .38f, .62f, .16f, .86f, .38f, .66f, .6f, .42f, .38f);
                    Shape(p, P, .54f, .49f, .16f, .86f);
                    break;
                case HudIconKind.Menu: Shape(p, P, .22f, .3f, .78f, .3f); Shape(p, P, .22f, .5f, .78f, .5f); Shape(p, P, .22f, .7f, .78f, .7f); break;
                case HudIconKind.Hold:
                    // Pinned to the ground: an arrow down onto a bar.
                    Shape(p, P, .5f, .14f, .5f, .64f); Shape(p, P, .32f, .46f, .5f, .64f, .68f, .46f);
                    Shape(p, P, .18f, .8f, .82f, .8f); Shape(p, P, .3f, .8f, .24f, .9f); Shape(p, P, .5f, .8f, .44f, .9f); Shape(p, P, .7f, .8f, .64f, .9f);
                    break;
                case HudIconKind.Assault:
                    // Shield and blade.
                    Shape(p, P, .16f, .26f, .46f, .18f, .46f, .6f, .31f, .82f, .16f, .6f, .16f, .26f);
                    Shape(p, P, .58f, .84f, .86f, .16f);
                    Shape(p, P, .55f, .62f, .78f, .72f);
                    break;
                case HudIconKind.Rifleman:
                    // Rifle with stock and scope.
                    Shape(p, P, .12f, .66f, .2f, .56f, .88f, .3f);
                    Shape(p, P, .12f, .66f, .18f, .8f, .32f, .74f, .3f, .6f);
                    Shape(p, P, .44f, .44f, .5f, .58f);
                    Shape(p, P, .48f, .36f, .66f, .29f);
                    break;
                case HudIconKind.Spider:
                    // A round body with three legs on each side.
                    HudPaint.Circle(p, P(.5f, .52f), size * .13f);
                    for (var i = 0; i < 3; i++)
                    {
                        var y = .4f + i * .12f;
                        Shape(p, P, .38f, y, .22f, y - .1f + i * .06f, .14f, y + .08f + i * .04f);
                        Shape(p, P, .62f, y, .78f, y - .1f + i * .06f, .86f, y + .08f + i * .04f);
                    }
                    break;
                case HudIconKind.Leaper:
                    // A body mid-jump over an arc, the blade forward.
                    p.MoveTo(P(.12f, .84f)); p.BezierCurveTo(P(.3f, .2f), P(.7f, .2f), P(.88f, .84f));
                    Shape(p, P, .42f, .42f, .58f, .36f, .66f, .46f, .5f, .54f, .42f, .42f);
                    Shape(p, P, .64f, .4f, .8f, .3f);
                    break;
                case HudIconKind.Gunship:
                    // Top-down silhouette with two rotors.
                    Shape(p, P, .5f, .14f, .5f, .86f);
                    Shape(p, P, .2f, .44f, .8f, .44f);
                    Shape(p, P, .38f, .82f, .62f, .82f);
                    HudPaint.Circle(p, P(.2f, .44f), size * .12f);
                    HudPaint.Circle(p, P(.8f, .44f), size * .12f);
                    break;
            }
            p.Stroke();
        }

        // Polyline through (x, y) pairs in unit space.
        private static void Shape(Painter2D p, Func<float, float, Vector2> point, params float[] xy)
        {
            p.MoveTo(point(xy[0], xy[1]));
            for (var i = 2; i + 1 < xy.Length; i += 2) p.LineTo(point(xy[i], xy[i + 1]));
        }

        private static void Worker(Painter2D p, Func<float, float, Vector2> point, float size, float scale)
        {
            Vector2 S(float x, float y) => point(.5f + (x - .5f) * scale, .5f + (y - .5f) * scale);
            HudPaint.Circle(p, S(.5f, .3f), size * .15f * scale);
            Shape(p, S, .28f, .86f, .34f, .56f, .66f, .56f, .72f, .86f);
            Shape(p, S, .41f, .3f, .59f, .3f);
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
