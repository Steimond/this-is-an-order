using Order.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    public enum AssistantMood { Idle, Attentive, Thinking, Speaking, Alert }

    // Animated presence of the voice assistant. The faction changes the core's shape language;
    // the palette stays with the surrounding theme.
    public sealed class AssistantCore : HudVector
    {
        private const int Bars = 96;
        public AssistantMood Mood = AssistantMood.Idle;
        public FactionId Faction;
        public float SpeechLevel;
        private float energy, alert, thinking, speech;
        private float tickAngle, ringAngle, dashAngle, scanAngle, coreAngle, phase, lastTime = -1;

        public AssistantCore()
        {
            AddToClassList("assistant-core");
            schedule.Execute(Animate).Every(16);
        }

        private void Animate()
        {
            var now = Time.realtimeSinceStartup;
            var dt = lastTime < 0 ? 0 : Mathf.Clamp(now - lastTime, 0, .1f);
            lastTime = now;
            var blend = 1 - Mathf.Exp(-dt * 7);
            var targetEnergy = Mood == AssistantMood.Speaking ? .7f : Mood == AssistantMood.Thinking ? .55f
                : Mood == AssistantMood.Alert ? .65f : Mood == AssistantMood.Attentive ? .35f : .12f;
            energy = Mathf.Lerp(energy, targetEnergy, blend);
            alert = Mathf.Lerp(alert, Mood == AssistantMood.Alert ? 1 : 0, blend);
            thinking = Mathf.Lerp(thinking, Mood == AssistantMood.Thinking ? 1 : 0, blend);
            speech = Mathf.Lerp(speech, Mood == AssistantMood.Speaking ? SpeechLevel : 0, 1 - Mathf.Exp(-dt * 18));
            tickAngle += dt * 3;
            ringAngle -= dt * (10 + thinking * 70);
            dashAngle += dt * (16 + energy * 30);
            scanAngle += dt * 210;
            coreAngle += dt * (6 + thinking * 30);
            phase += dt * (.8f + energy * 2.6f);
            MarkDirtyRepaint();
        }

        protected override void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var radius = Mathf.Min(rect.width, rect.height) * .5f - 14;
            if (radius < 30) return;
            var c = rect.center;
            var t = Time.realtimeSinceStartup;
            var p = context.painter2D;
            var main = Color.Lerp(Accent, Warn, alert);
            var hot = Color.Lerp(Hot, Warn, alert * .6f);
            var pulse = .5f + .5f * Mathf.Sin(t * (1.6f + energy * 3));

            HudPaint.Glow(context, c, radius * 1.3f, HudPaint.Alpha(main, .07f + .08f * energy));
            HudPaint.Glow(context, c, radius * .55f, HudPaint.Alpha(main, .12f + .22f * energy + .12f * speech));
            p.lineCap = LineCap.Butt; p.lineJoin = LineJoin.Miter;

            p.lineWidth = 1; p.strokeColor = HudPaint.Alpha(Line, .95f); p.BeginPath();
            for (var i = 0; i < 120; i++)
            {
                var a = i * 3 + tickAngle;
                var length = i % 10 == 0 ? 10 : i % 5 == 0 ? 6 : 3;
                p.MoveTo(HudPaint.Polar(c, radius, a)); p.LineTo(HudPaint.Polar(c, radius - length, a));
            }
            p.Stroke();

            p.lineWidth = 2; p.strokeColor = HudPaint.Alpha(main, .85f); p.BeginPath();
            for (var k = 0; k < 4; k++) { var a = k * 90 - 45; p.MoveTo(HudPaint.Polar(c, radius + 5, a)); p.LineTo(HudPaint.Polar(c, radius + 13, a)); }
            p.Stroke();

            p.lineWidth = 2.5f; p.strokeColor = HudPaint.Alpha(main, .9f); p.BeginPath();
            for (var k = 0; k < 3; k++) HudPaint.Arc(p, c, radius * .87f, ringAngle + k * 120, ringAngle + k * 120 + 76);
            p.Stroke();
            p.lineWidth = 4; p.strokeColor = Warn; p.BeginPath();
            HudPaint.Arc(p, c, radius * .87f, ringAngle + 84, ringAngle + 92);
            HudPaint.Arc(p, c, radius * .87f, ringAngle + 264, ringAngle + 268);
            p.Stroke();

            p.lineWidth = 1; p.strokeColor = HudPaint.Alpha(Line, .75f); p.BeginPath();
            HudPaint.Circle(p, c, radius * .8f); HudPaint.Circle(p, c, radius * .52f);
            p.Stroke();

            p.lineWidth = 2; p.strokeColor = HudPaint.Alpha(main, .4f); p.BeginPath();
            for (var i = 0; i < 60; i++) HudPaint.Arc(p, c, radius * .74f, dashAngle + i * 6, dashAngle + i * 6 + 2.6f);
            p.Stroke();

            DrawBars(p, c, radius, main, hot, t);

            if (thinking > .02f)
            {
                p.lineWidth = 3; p.strokeColor = HudPaint.Alpha(hot, thinking); p.BeginPath();
                HudPaint.Arc(p, c, radius * .62f, scanAngle, scanAngle + 46);
                HudPaint.Arc(p, c, radius * .62f, scanAngle + 180, scanAngle + 226);
                p.Stroke();
            }

            if (Faction == FactionId.Expedition) DrawEngineeredCore(p, c, radius, main, hot, pulse);
            else DrawOrganicCore(p, c, radius, main, hot, pulse, t);
        }

        private void DrawBars(Painter2D p, Vector2 c, float radius, Color main, Color hot, float t)
        {
            var inner = radius * .55f;
            p.lineWidth = Mathf.Max(1.5f, radius * .018f);
            for (var pass = 0; pass < 2; pass++)
            {
                p.strokeColor = pass == 0 ? HudPaint.Alpha(main, .75f) : HudPaint.Alpha(hot, .9f);
                p.BeginPath();
                for (var i = 0; i < Bars; i++)
                {
                    var mirrored = Mathf.Min(i, Bars - i);
                    var noise = Mathf.PerlinNoise(mirrored * .23f, phase);
                    var level = .02f + .025f * energy + noise * (.03f * energy + .17f * speech);
                    var angle = -90 + i * 360f / Bars;
                    if (thinking > .02f)
                    {
                        var distance = Mathf.Abs(Mathf.DeltaAngle(angle, scanAngle + 23));
                        level += thinking * .07f * Mathf.Clamp01(1 - distance / 40);
                    }
                    // Second pass highlights only the loudest bars.
                    if (pass == 1 && level < .09f) continue;
                    var from = pass == 0 ? inner : inner + radius * .09f;
                    var to = inner + radius * level;
                    if (to <= from) continue;
                    p.MoveTo(HudPaint.Polar(c, from, angle)); p.LineTo(HudPaint.Polar(c, to, angle));
                }
                p.Stroke();
            }
        }

        private void DrawEngineeredCore(Painter2D p, Vector2 c, float radius, Color main, Color hot, float pulse)
        {
            p.lineWidth = 2; p.strokeColor = main; p.fillColor = HudPaint.Alpha(main, .07f + .05f * energy);
            p.BeginPath(); HudPaint.Polygon(p, c, radius * .36f, 6, coreAngle - 90); p.Fill(); p.Stroke();
            p.lineWidth = 1; p.strokeColor = HudPaint.Alpha(Line, .9f);
            p.BeginPath(); HudPaint.Polygon(p, c, radius * .24f, 6, -coreAngle * 1.6f - 90); p.Stroke();
            p.strokeColor = HudPaint.Alpha(main, .45f); p.BeginPath();
            for (var i = 0; i < 6; i++) { var a = coreAngle - 90 + i * 60; p.MoveTo(HudPaint.Polar(c, radius * .25f, a)); p.LineTo(HudPaint.Polar(c, radius * .35f, a)); }
            p.Stroke();
            p.fillColor = hot; p.BeginPath(); HudPaint.Polygon(p, c, radius * (.06f + .025f * pulse * (.4f + energy)), 4, -90); p.Fill();
        }

        private void DrawOrganicCore(Painter2D p, Vector2 c, float radius, Color main, Color hot, float pulse, float t)
        {
            void Blob(float baseRadius, float shift)
            {
                p.BeginPath();
                for (var i = 0; i <= 72; i++)
                {
                    var a = i * 5f;
                    var r = a * Mathf.Deg2Rad;
                    var wobble = 1 + .07f * Mathf.Sin(3 * r + 1.3f * t + shift) + .05f * Mathf.Sin(5 * r - .9f * t + shift)
                        + .03f * Mathf.Sin(7 * r + 2.1f * t) + .06f * energy * Mathf.Sin(t * 5 + r * 2 + shift);
                    var point = HudPaint.Polar(c, baseRadius * wobble, a);
                    if (i == 0) p.MoveTo(point); else p.LineTo(point);
                }
                p.ClosePath();
            }
            p.lineWidth = 2; p.strokeColor = main; p.fillColor = HudPaint.Alpha(main, .08f + .05f * energy);
            Blob(radius * .32f, 0); p.Fill(); p.Stroke();
            p.lineWidth = 1; p.strokeColor = HudPaint.Alpha(Line, .9f);
            Blob(radius * .2f, 2); p.Stroke();
            p.strokeColor = HudPaint.Alpha(main, .5f); p.BeginPath();
            for (var i = 0; i < 5; i++)
            {
                var a = coreAngle * .6f + i * 72 + 8 * Mathf.Sin(t * .7f + i);
                var node = HudPaint.Polar(c, radius * .44f, a);
                p.MoveTo(HudPaint.Polar(c, radius * .33f, a + 10)); p.QuadraticCurveTo(HudPaint.Polar(c, radius * .4f, a + 18), node);
            }
            p.Stroke();
            p.fillColor = main; p.BeginPath();
            for (var i = 0; i < 5; i++) HudPaint.Circle(p, HudPaint.Polar(c, radius * .44f, coreAngle * .6f + i * 72 + 8 * Mathf.Sin(t * .7f + i)), 2.5f + 1.5f * energy);
            p.Fill();
            p.fillColor = hot; p.BeginPath(); HudPaint.Circle(p, c, radius * (.05f + .02f * pulse * (.4f + energy))); p.Fill();
        }
    }
}
