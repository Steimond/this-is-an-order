using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Object = UnityEngine.Object;

namespace Order.Presentation
{
    // Hardware cursor drawn at runtime, so it costs no input latency and can follow the theme colour.
    public sealed class HudCursor : IDisposable
    {
        private enum Shape { Arrow, Pointer, Text, Blocked }
        private readonly VisualElement scope;
        private readonly Texture2D[] textures = new Texture2D[4];
        private readonly Vector2[] hotspots = new Vector2[4];
        private Shape shape = Shape.Arrow;
        private bool applied, enabled = true;

        public bool Enabled { get => enabled; set { if (enabled == value) return; enabled = value; Apply(); } }

        public HudCursor(VisualElement scope, Color accent, Color hot, Color dark, Color warn)
        {
            this.scope = scope;
            var size = Screen.height > 1600 ? 48 : 32;
            var scale = size / 32f;
            Vector2[] arrow = { new Vector2(2, 2), new Vector2(2, 23), new Vector2(8, 17.2f), new Vector2(18.5f, 17.2f) };
            textures[0] = Draw(size, canvas =>
            {
                canvas.Polygon(arrow, Alpha(dark, .88f), accent, 1.4f, Alpha(Color.black, .6f));
                canvas.Line(new Vector2(4.5f, 9), new Vector2(4.5f, 17), 1, Alpha(accent, .55f));
                canvas.Disc(new Vector2(2.6f, 2.6f), 1.3f, hot);
            });
            textures[1] = Draw(size, canvas =>
            {
                canvas.Polygon(arrow, Alpha(accent, .45f), hot, 1.5f, Alpha(Color.black, .65f));
                canvas.Ring(new Vector2(23.5f, 23.5f), 4.2f, 1.4f, accent, Alpha(Color.black, .6f));
                canvas.Disc(new Vector2(23.5f, 23.5f), 1.2f, hot);
            });
            textures[2] = Draw(size, canvas =>
            {
                foreach (var pass in new[] { (3.4f, Alpha(Color.black, .6f)), (1.5f, hot) })
                {
                    canvas.Line(new Vector2(16, 7), new Vector2(16, 25), pass.Item1, pass.Item2);
                    canvas.Line(new Vector2(12.5f, 6.5f), new Vector2(19.5f, 6.5f), pass.Item1, pass.Item2);
                    canvas.Line(new Vector2(12.5f, 25.5f), new Vector2(19.5f, 25.5f), pass.Item1, pass.Item2);
                }
            });
            textures[3] = Draw(size, canvas =>
            {
                canvas.Polygon(arrow, Alpha(dark, .88f), Alpha(accent, .45f), 1.4f, Alpha(Color.black, .6f));
                canvas.Ring(new Vector2(23.5f, 23.5f), 4.2f, 1.4f, warn, Alpha(Color.black, .6f));
                canvas.Line(new Vector2(20.8f, 26.2f), new Vector2(26.2f, 20.8f), 1.4f, warn);
            });
            hotspots[0] = hotspots[1] = hotspots[3] = new Vector2(2, 2) * scale;
            hotspots[2] = new Vector2(16, 16) * scale;
            scope.RegisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            Apply();
        }

        private void OnOver(PointerOverEvent evt)
        {
            var target = evt.target as VisualElement;
            var interactive = Hud.FindInteractive(target, scope);
            var next = Hud.IsTextInput(target, scope) ? Shape.Text
                : interactive == null ? Shape.Arrow : interactive.enabledInHierarchy ? Shape.Pointer : Shape.Blocked;
            if (next == shape) return;
            shape = next;
            Apply();
        }

        private void Apply()
        {
            if (enabled) { Cursor.SetCursor(textures[(int)shape], hotspots[(int)shape], CursorMode.Auto); applied = true; }
            else if (applied) { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); applied = false; }
        }

        public void Dispose()
        {
            scope.UnregisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            foreach (var texture in textures) if (texture != null) Object.Destroy(texture);
        }

        private static Color Alpha(Color color, float alpha) { color.a *= alpha; return color; }

        private static Texture2D Draw(int size, Action<CursorCanvas> paint)
        {
            var canvas = new CursorCanvas(size);
            paint(canvas);
            return canvas.ToTexture();
        }

        // Tiny anti-aliased rasterizer in a 32-unit design space.
        private sealed class CursorCanvas
        {
            private readonly int size;
            private readonly float scale;
            private readonly Color[] pixels;

            public CursorCanvas(int size) { this.size = size; scale = size / 32f; pixels = new Color[size * size]; }

            public void Polygon(Vector2[] points, Color fill, Color stroke, float width, Color halo)
            {
                Shade((p, unit) =>
                {
                    var coverage = 0f;
                    for (var sy = 0; sy < 4; sy++)
                        for (var sx = 0; sx < 4; sx++)
                            if (Inside(points, p + new Vector2((sx - 1.5f) * .25f, (sy - 1.5f) * .25f) * unit)) coverage += 1 / 16f;
                    var distance = EdgeDistance(points, p);
                    var color = Over(Alpha(halo, Band(distance, width + 2.2f, unit)), Alpha(fill, coverage));
                    return Over(color, Alpha(stroke, Band(distance, width, unit)));
                });
            }

            public void Ring(Vector2 center, float radius, float width, Color stroke, Color halo) =>
                Shade((p, unit) =>
                {
                    var distance = Mathf.Abs(Vector2.Distance(p, center) - radius);
                    return Over(Alpha(halo, Band(distance, width + 2.2f, unit)), Alpha(stroke, Band(distance, width, unit)));
                });

            public void Disc(Vector2 center, float radius, Color fill) =>
                Shade((p, unit) => Alpha(fill, Mathf.Clamp01((radius - Vector2.Distance(p, center)) / unit + .5f)));

            public void Line(Vector2 a, Vector2 b, float width, Color color) =>
                Shade((p, unit) => Alpha(color, Band(SegmentDistance(p, a, b), width, unit)));

            private void Shade(Func<Vector2, float, Color> sample)
            {
                var unit = 1 / scale;
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var color = sample(new Vector2(x + .5f, y + .5f) * unit, unit);
                        if (color.a <= 0) continue;
                        var index = (size - 1 - y) * size + x;
                        pixels[index] = Over(pixels[index], color);
                    }
            }

            public Texture2D ToTexture()
            {
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Order HUD Cursor" };
                texture.SetPixels(pixels);
                texture.Apply(false, false);
                return texture;
            }

            private static float Band(float distance, float width, float unit) => Mathf.Clamp01((width * .5f - distance) / unit + .5f);

            private static Color Over(Color below, Color above)
            {
                var alpha = above.a + below.a * (1 - above.a);
                if (alpha <= 0) return Color.clear;
                var rgb = ((Vector4)above * above.a + (Vector4)below * below.a * (1 - above.a)) / alpha;
                return new Color(rgb.x, rgb.y, rgb.z, alpha);
            }

            private static bool Inside(Vector2[] points, Vector2 p)
            {
                var inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    if ((points[i].y > p.y) != (points[j].y > p.y) &&
                        p.x < (points[j].x - points[i].x) * (p.y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                        inside = !inside;
                return inside;
            }

            private static float EdgeDistance(Vector2[] points, Vector2 p)
            {
                var best = float.MaxValue;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++) best = Mathf.Min(best, SegmentDistance(p, points[j], points[i]));
                return best;
            }

            private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
            {
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                return Vector2.Distance(p, a + ab * t);
            }
        }
    }

    public enum HudCue { Hover, Click, Reply, Deny }

    // Synthesized interface sounds; no audio assets needed for the prototype.
    public sealed class HudSound : IDisposable
    {
        private readonly VisualElement scope;
        private readonly GameObject host;
        private readonly AudioSource source;
        private readonly AudioClip[] clips;
        private VisualElement hovered;
        private float lastHover;
        public float Volume = .6f;

        public HudSound(VisualElement scope)
        {
            this.scope = scope;
            host = new GameObject("Order Interface Sound") { hideFlags = HideFlags.HideAndDontSave };
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0;
            clips = new[]
            {
                Tone("hud-hover", .035f, 2300, 2700, .09f),
                Tone("hud-click", .075f, 900, 1500, .2f, 2),
                Tone("hud-reply", .16f, 660, 990, .12f, 3),
                Tone("hud-deny", .12f, 320, 240, .2f, 2, true),
            };
            scope.RegisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            scope.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
        }

        public void Play(HudCue cue)
        {
            if (Volume <= 0 || source == null) return;
            source.PlayOneShot(clips[(int)cue], Volume);
        }

        private void OnOver(PointerOverEvent evt)
        {
            var interactive = Hud.FindInteractive(evt.target as VisualElement, scope);
            if (interactive == hovered) return;
            hovered = interactive;
            if (interactive == null || !interactive.enabledInHierarchy || Time.unscaledTime - lastHover < .04f) return;
            lastHover = Time.unscaledTime;
            Play(HudCue.Hover);
        }

        private void OnClick(ClickEvent evt)
        {
            var interactive = Hud.FindInteractive(evt.target as VisualElement, scope);
            if (interactive != null && interactive.enabledInHierarchy) Play(HudCue.Click);
        }

        public void Dispose()
        {
            scope.UnregisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            scope.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            foreach (var clip in clips) Object.Destroy(clip);
            Object.Destroy(host);
        }

        private static AudioClip Tone(string name, float duration, float from, float to, float volume, float harmonics = 1, bool stutter = false)
        {
            const int rate = 44100;
            var count = (int)(duration * rate);
            var data = new float[count];
            var phase = 0.0;
            for (var i = 0; i < count; i++)
            {
                var k = i / (float)count;
                phase += 2 * Math.PI * Mathf.Lerp(from, to, k) / rate;
                var wave = Math.Sin(phase) + (harmonics > 1 ? .25 * Math.Sin(phase * 2) : 0) + (harmonics > 2 ? .12 * Math.Sin(phase * 3) : 0);
                var envelope = Mathf.Min(1, i / (rate * .003f)) * Mathf.Exp(-k * 4.5f);
                if (stutter && k > .4f && k < .55f) envelope = 0;
                data[i] = (float)wave * envelope * volume;
            }
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    // Uses the Windows system font Bahnschrift at runtime (not redistributed); falls back to the theme font.
    public static class HudFonts
    {
        private static FontAsset font;

        public static void Apply(VisualElement element)
        {
            if (font == null)
            {
                try { font = FontAsset.CreateFontAsset("Bahnschrift", "Regular", 90); }
                catch (Exception) { font = null; }
            }
            if (font != null) element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(font));
        }
    }
}
