using System.Collections.Generic;
using UnityEngine;

namespace Order.Presentation
{
    // Combat feedback in the world, short-lived and unlit: tracers with muzzle flashes, melee slashes, impact sparks,
    // explosions with a shock ring, smoke and a scorch mark that slowly fades, and health bars above units.
    sealed class CombatEffects
    {
        sealed class Effect
        {
            public GameObject View; public Renderer Renderer; public LineRenderer Line;
            public float Start, Life, From, To, FadeFrom; public Color Color; public Vector3 Drift;
            // A bullet streak flies from A to B; Start may lie ahead, so an impact waits for the bullet.
            public Vector3 A, B; public bool Streak;
            public bool Billboard, Falls;
        }
        readonly TempleScenery scenery;
        readonly Transform parent;
        readonly Camera camera;
        readonly Mesh disc, ring, quad;
        readonly Material lineMaterial, sparkMaterial, smokeMaterial, scorchMaterial, ringMaterial, barBack, barFill;
        readonly List<Effect> effects = new List<Effect>();
        readonly List<(Transform back, Transform fill, Renderer renderer)> bars = new List<(Transform, Transform, Renderer)>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        int barsUsed;
        const float BulletSpeed = 45, StreakLength = 1.1f;

        public CombatEffects(TempleScenery scenery, Transform parent, Camera camera, Mesh disc, Mesh ring)
        {
            this.scenery = scenery; this.parent = parent; this.camera = camera; this.disc = disc; this.ring = ring;
            quad = scenery.Own(Quad());
            // Lines take their hue and alpha from vertex colours; the material lifts them above 1 for the bloom.
            lineMaterial = scenery.Glow("Tracer", new Color(2.6f, 2.6f, 2.6f, 1));
            sparkMaterial = scenery.Glow("Spark", Color.white, true, m => m.SetFloat("_Soft", 1));
            smokeMaterial = scenery.Glow("Smoke", Color.white, false, m => m.SetFloat("_Soft", 1));
            scorchMaterial = scenery.Glow("Scorch", Color.white, false, m => { m.SetFloat("_Soft", 1); m.renderQueue = 2990; });
            ringMaterial = scenery.Glow("Shock ring", Color.white);
            barBack = scenery.Glow("Health bar", new Color(0, 0, 0, .7f), false, m => m.renderQueue = 3100);
            barFill = scenery.Glow("Health fill", Color.white, false, m => m.renderQueue = 3101);
        }

        // ---------- Strikes ----------
        // A short glowing streak flies from the muzzle at bullet speed; the hit flashes when it arrives.
        public void Tracer(Vector3 from, Vector3 to, Color hue, float width = .075f)
        {
            var line = Line("Tracer", 2, width);
            line.SetPosition(0, from); line.SetPosition(1, from);
            float flight = Vector3.Distance(from, to) / BulletSpeed;
            Add(new Effect { View = line.gameObject, Line = line, Life = flight + StreakLength / BulletSpeed, Color = hue, A = from, B = to, Streak = true });
            Flash(from, hue * 3f, .42f, .07f);
            Flash(to, new Color(3f, 1.7f, .7f), .5f, .13f, 1.3f, flight);
        }
        // An arc swept across the target, from the attacker's side.
        public void Slash(Vector3 at, Vector3 direction, Color hue)
        {
            var line = Line("Slash", 7, .1f);
            direction.y = 0; if (direction.sqrMagnitude < 1e-4f) direction = Vector3.forward; direction.Normalize();
            var side = Vector3.Cross(Vector3.up, direction);
            float sweep = Random.value < .5f ? 1 : -1;
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-1.1f, 1.1f, i / 6f) * sweep;
                line.SetPosition(i, at - direction * .32f + (side * Mathf.Sin(a) + direction * (Mathf.Cos(a) - 1) * .4f) * .42f + Vector3.up * a * .14f);
            }
            line.widthCurve = new AnimationCurve(new Keyframe(0, .2f), new Keyframe(.5f, 1), new Keyframe(1, .2f));
            Add(new Effect { View = line.gameObject, Line = line, Life = .2f, Color = hue });
            Flash(at, new Color(2.4f, 2.7f, 3f), .55f, .12f);
        }
        public void Flash(Vector3 at, Color color, float size, float life, float grow = 1.3f, float delay = 0)
        {
            var view = scenery.Model("Flash", disc, sparkMaterial, parent, false);
            view.transform.position = at;
            if (delay > 0) view.transform.localScale = Vector3.zero;
            Add(new Effect { View = view, Renderer = view.GetComponent<MeshRenderer>(), Life = life, From = size, To = size * grow, Color = color, Billboard = true }, delay);
        }

        // ---------- Deaths ----------
        // A fireball with debris and smoke where the unit was, a shock ring and a scorch mark on the ground below.
        public void Explosion(Vector3 at, float ground, float size)
        {
            Flash(at, new Color(3.2f, 1.6f, .55f), size * .5f, .45f, 4.5f);
            Flash(at, new Color(3, 2.6f, 2), size * .35f, .12f, 2);
            for (int i = 0; i < 8; i++)
            {
                var view = scenery.Model("Debris", disc, sparkMaterial, parent, false);
                view.transform.position = at;
                var drift = Quaternion.Euler(0, i * 45 + Random.Range(-15f, 15f), 0) * new Vector3(Random.Range(1.6f, 3.2f), Random.Range(2f, 4.5f), 0) * size;
                Add(new Effect { View = view, Renderer = view.GetComponent<MeshRenderer>(), Life = .55f, From = .14f, To = .05f, Color = new Color(3, 1.5f, .45f), Billboard = true, Drift = drift, Falls = true });
            }
            for (int i = 0; i < 3; i++)
            {
                var view = scenery.Model("Smoke", disc, smokeMaterial, parent, false);
                view.transform.position = at + Random.insideUnitSphere * .25f * size;
                Add(new Effect { View = view, Renderer = view.GetComponent<MeshRenderer>(), Life = 1.4f + i * .3f, From = size * .7f, To = size * 2.4f,
                    Color = new Color(.06f, .055f, .05f, .55f), Billboard = true, Drift = new Vector3(Random.Range(-.3f, .3f), .7f, Random.Range(-.3f, .3f)) });
            }
            var shock = scenery.Model("Shock ring", ring, ringMaterial, parent, false);
            shock.transform.position = new Vector3(at.x, ground + .06f, at.z);
            Add(new Effect { View = shock, Renderer = shock.GetComponent<MeshRenderer>(), Life = .45f, From = .3f * size, To = 2.1f * size, Color = new Color(2.6f, 1.2f, .4f) });
            var scorch = scenery.Model("Scorch", disc, scorchMaterial, parent, false);
            scorch.transform.SetPositionAndRotation(new Vector3(at.x, ground + .025f, at.z), Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            Add(new Effect { View = scorch, Renderer = scorch.GetComponent<MeshRenderer>(), Life = 9, From = 1.5f * size, To = 1.6f * size, Color = new Color(.02f, .018f, .015f, .75f), FadeFrom = .6f });
        }

        public void Tick(float dt)
        {
            float now = Time.time;
            var facing = Quaternion.FromToRotation(Vector3.up, -camera.transform.forward);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var e = effects[i]; float t = (now - e.Start) / e.Life;
                if (t < 0) continue;
                if (t >= 1) { Object.Destroy(e.View); effects.RemoveAt(i); continue; }
                var color = e.Color; color.a *= 1 - Mathf.Clamp01((t - e.FadeFrom) / (1 - e.FadeFrom));
                if (e.Streak)
                {
                    // The head runs at bullet speed and stops in the target; the tail follows a streak length behind.
                    float length = Vector3.Distance(e.A, e.B), travelled = (now - e.Start) * BulletSpeed;
                    float head = Mathf.Min(length, travelled), tail = Mathf.Clamp(travelled - StreakLength, 0, length);
                    var direction = length > 1e-3f ? (e.B - e.A) / length : Vector3.zero;
                    e.Line.SetPosition(0, e.A + direction * tail); e.Line.SetPosition(1, e.A + direction * head);
                    e.Line.startColor = new Color(color.r, color.g, color.b, 0); e.Line.endColor = new Color(color.r, color.g, color.b, 1);
                    continue;
                }
                if (e.Line != null) { color.a = Mathf.Clamp01(color.a); e.Line.startColor = e.Line.endColor = color; continue; }
                if (e.Falls) e.Drift.y -= 9 * dt;
                var transform = e.View.transform;
                transform.position += e.Drift * dt;
                if (e.Billboard) transform.rotation = facing;
                transform.localScale = Vector3.one * Mathf.Lerp(e.From, e.To, 1 - (1 - t) * (1 - t));
                block.SetColor("_Color", color); e.Renderer.SetPropertyBlock(block);
            }
        }

        // ---------- Health bars ----------
        // Bars are drawn anew each frame: call Bar for every unit that shows one, then EndBars.
        public void Bar(Vector3 top, float width, float share)
        {
            if (barsUsed == bars.Count)
            {
                var back = scenery.Model("Health bar", quad, barBack, parent, false).transform;
                var fill = scenery.Model("Health fill", quad, barFill, parent, false).transform;
                bars.Add((back, fill, fill.GetComponent<MeshRenderer>()));
            }
            var (b, f, renderer) = bars[barsUsed++];
            b.gameObject.SetActive(true); f.gameObject.SetActive(true);
            var rotation = camera.transform.rotation; var right = camera.transform.right;
            b.SetPositionAndRotation(top, rotation); b.localScale = new Vector3(width + .05f, .13f, 1);
            share = Mathf.Clamp01(share);
            f.SetPositionAndRotation(top + right * (share - 1) * width * .5f - camera.transform.forward * .01f, rotation);
            f.localScale = new Vector3(width * share, .075f, 1);
            var color = share > .6f ? new Color(.35f, 1.25f, .45f) : share > .3f ? new Color(1.3f, 1.05f, .25f) : new Color(1.4f, .3f, .22f);
            block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
        }
        public void EndBars()
        {
            for (int i = barsUsed; i < bars.Count; i++) { bars[i].back.gameObject.SetActive(false); bars[i].fill.gameObject.SetActive(false); }
            barsUsed = 0;
        }

        LineRenderer Line(string name, int points, float width)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = lineMaterial; line.positionCount = points; line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            line.numCapVertices = 2;
            return line;
        }
        void Add(Effect effect, float delay = 0) { effect.Start = Time.time + delay; effects.Add(effect); }
        static Mesh Quad()
        {
            var mesh = new Mesh { name = "Bar quad" };
            mesh.SetVertices(new[] { new Vector3(-.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0), new Vector3(.5f, -.5f, 0) });
            mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); mesh.RecalculateBounds();
            return mesh;
        }
        public void Dispose()
        {
            foreach (var e in effects) if (e.View != null) Object.Destroy(e.View);
            effects.Clear(); bars.Clear();
        }
    }
}
