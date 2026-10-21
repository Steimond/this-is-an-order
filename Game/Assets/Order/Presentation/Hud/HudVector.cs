using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Base for procedurally drawn HUD graphics. Single-tone shapes use the resolved color (stroke) and
    // background tint (fill), so USS states and transitions drive them; multi-tone shapes read theme tokens.
    public abstract class HudVector : VisualElement
    {
        private static readonly CustomStyleProperty<Color> AccentToken = new CustomStyleProperty<Color>("--hud-accent");
        private static readonly CustomStyleProperty<Color> HotToken = new CustomStyleProperty<Color>("--hud-hot");
        private static readonly CustomStyleProperty<Color> LineToken = new CustomStyleProperty<Color>("--hud-line-bright");
        private static readonly CustomStyleProperty<Color> WarnToken = new CustomStyleProperty<Color>("--hud-warn");
        private static readonly CustomStyleProperty<Color> MutedToken = new CustomStyleProperty<Color>("--hud-muted");
        private static readonly CustomStyleProperty<Color> BackgroundToken = new CustomStyleProperty<Color>("--hud-bg");
        protected Color Accent = new Color(.25f, .83f, .78f), Hot = new Color(.73f, 1f, .97f), Line = new Color(.17f, .45f, .46f),
            Warn = new Color(1f, .54f, .24f), Muted = new Color(.42f, .58f, .59f), Background = new Color(.016f, .035f, .047f);
        private Color drawnStroke, drawnFill;

        protected HudVector()
        {
            AddToClassList("hud-graphic");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnTokens);
            // Generated meshes do not follow color transitions on their own.
            schedule.Execute(TrackColors).Every(16);
        }

        protected Color Stroke => resolvedStyle.color;
        protected Color Fill => resolvedStyle.unityBackgroundImageTintColor;
        protected abstract void Draw(MeshGenerationContext context);

        private void OnTokens(CustomStyleResolvedEvent evt)
        {
            var tokens = evt.customStyle;
            if (tokens.TryGetValue(AccentToken, out var accent)) Accent = accent;
            if (tokens.TryGetValue(HotToken, out var hot)) Hot = hot;
            if (tokens.TryGetValue(LineToken, out var line)) Line = line;
            if (tokens.TryGetValue(WarnToken, out var warn)) Warn = warn;
            if (tokens.TryGetValue(MutedToken, out var muted)) Muted = muted;
            if (tokens.TryGetValue(BackgroundToken, out var background)) Background = background;
            MarkDirtyRepaint();
        }

        private void TrackColors()
        {
            var stroke = resolvedStyle.color;
            var fill = resolvedStyle.unityBackgroundImageTintColor;
            if (stroke == drawnStroke && fill == drawnFill) return;
            drawnStroke = stroke; drawnFill = fill;
            MarkDirtyRepaint();
        }
    }

    public static class HudPaint
    {
        public static Color Alpha(Color color, float alpha) { color.a *= alpha; return color; }

        public static Vector2 Polar(Vector2 center, float radius, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }

        // Arc without a connecting line from the previous point.
        public static void Arc(Painter2D painter, Vector2 center, float radius, float from, float to)
        {
            painter.MoveTo(Polar(center, radius, from));
            painter.Arc(center, radius, Angle.Degrees(from), Angle.Degrees(to));
        }

        public static void Circle(Painter2D painter, Vector2 center, float radius) => Arc(painter, center, radius, 0, 360);

        public static void Polygon(Painter2D painter, Vector2 center, float radius, int sides, float rotation)
        {
            painter.MoveTo(Polar(center, radius, rotation));
            for (var i = 1; i < sides; i++) painter.LineTo(Polar(center, radius, rotation + i * 360f / sides));
            painter.ClosePath();
        }

        public static void Chamfer(Painter2D painter, Rect r, float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(r.xMin + topLeft, r.yMin));
            painter.LineTo(new Vector2(r.xMax - topRight, r.yMin));
            painter.LineTo(new Vector2(r.xMax, r.yMin + topRight));
            painter.LineTo(new Vector2(r.xMax, r.yMax - bottomRight));
            painter.LineTo(new Vector2(r.xMax - bottomRight, r.yMax));
            painter.LineTo(new Vector2(r.xMin + bottomLeft, r.yMax));
            painter.LineTo(new Vector2(r.xMin, r.yMax - bottomLeft));
            painter.LineTo(new Vector2(r.xMin, r.yMin + topLeft));
            painter.ClosePath();
        }

        // Radial falloff from center color to transparent.
        public static void Glow(MeshGenerationContext context, Vector2 center, float radius, Color color, int segments = 48)
        {
            var mesh = context.Allocate(segments + 1, segments * 3);
            var edge = color; edge.a = 0;
            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x, center.y, Vertex.nearZ), tint = color });
            for (var i = 0; i < segments; i++)
            {
                var point = Polar(center, radius, i * 360f / segments);
                mesh.SetNextVertex(new Vertex { position = new Vector3(point.x, point.y, Vertex.nearZ), tint = edge });
            }
            for (var i = 0; i < segments; i++)
            {
                mesh.SetNextIndex(0);
                mesh.SetNextIndex((ushort)(1 + i));
                mesh.SetNextIndex((ushort)(1 + (i + 1) % segments));
            }
        }

        public static void Gradient(MeshGenerationContext context, Rect r, Color topLeft, Color topRight, Color bottomRight, Color bottomLeft)
        {
            var mesh = context.Allocate(4, 6);
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMin, Vertex.nearZ), tint = topLeft });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMin, Vertex.nearZ), tint = topRight });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMax, Vertex.nearZ), tint = bottomRight });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMax, Vertex.nearZ), tint = bottomLeft });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
        }
    }
}
