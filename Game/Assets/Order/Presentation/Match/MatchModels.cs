using Order.Content;
using UnityEngine;

namespace Order.Presentation
{
    // Unit model split into animated parts: legs swing around their hips, rotors spin around their hubs.
    public sealed class UnitModel
    {
        public Mesh Body, Leg, Rotor;
        public Vector3[] Hips = new Vector3[0], Rotors = new Vector3[0];
        // Height is the visible top of a ground unit before Scale; it bounds clicks on the model.
        public float Hover, Scale = 1, Height = 1;
    }

    // Generated low-poly models of the prototype: units, buildings, deposits and markers.
    // Parts flagged Team take the player's colour in the shader; Glow parts are emissive.
    public static class MatchModels
    {
        static readonly Color Metal = new Color(.24f, .26f, .29f), Plate = new Color(.52f, .55f, .58f), Light = new Color(.78f, .8f, .82f),
            Dark = new Color(.1f, .11f, .12f), Concrete = new Color(.4f, .4f, .39f), Cyan = new Color(.45f, .9f, 1), Blade = new Color(1, .62f, .3f);

        // Units face +Z with their origin on the ground; Hover lifts the body of air units.
        public static UnitModel Unit(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Melee: return Assault();
                case UnitKind.Ranged: return Rifleman();
                case UnitKind.Air: return Gunship();
                default: return new UnitModel { Body = Worker(), Scale = 1.25f, Height = .75f };
            }
        }
        static void Box(MeshBuilder b, Color color, Vector3 center, Vector3 size, float team = 0, float glow = 0)
        { b.Paint = color; b.Team = team; b.Glow = glow; b.Transform = Matrix4x4.identity; b.Box(center, size); }

        // Heavy exosuit: shield on the left arm, energy blade on the right.
        static UnitModel Assault()
        {
            var b = new MeshBuilder();
            Box(b, Metal, new Vector3(0, .54f, 0), new Vector3(.34f, .12f, .22f));
            Box(b, Plate, new Vector3(0, .79f, 0), new Vector3(.5f, .36f, .34f));
            Box(b, Light, new Vector3(0, .83f, .16f), new Vector3(.36f, .24f, .06f), 1);
            Box(b, Metal, new Vector3(0, .83f, -.2f), new Vector3(.32f, .28f, .1f));
            for (int side = -1; side <= 1; side += 2) Box(b, Cyan, new Vector3(side * .08f, .86f, -.26f), new Vector3(.05f, .15f, .02f), 0, 1);
            Box(b, Metal, new Vector3(0, 1.03f, .04f), new Vector3(.2f, .14f, .2f));
            Box(b, Cyan, new Vector3(0, 1.04f, .145f), new Vector3(.14f, .035f, .02f), 0, 1);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(b, Light, new Vector3(side * .33f, .93f, 0), new Vector3(.2f, .14f, .27f), 1);
                Box(b, Metal, new Vector3(side * .33f, .71f, .04f), new Vector3(.12f, .3f, .14f));
            }
            Box(b, Metal, new Vector3(.34f, .56f, .16f), new Vector3(.1f, .1f, .16f));
            Box(b, Blade, new Vector3(.35f, .54f, .46f), new Vector3(.035f, .07f, .5f), 0, 1);
            Box(b, Plate, new Vector3(-.43f, .67f, .12f), new Vector3(.05f, .48f, .38f));
            Box(b, Light, new Vector3(-.46f, .67f, .12f), new Vector3(.02f, .4f, .3f), 1);
            var leg = new MeshBuilder();
            Box(leg, Metal, new Vector3(0, -.14f, 0), new Vector3(.15f, .3f, .17f));
            Box(leg, Plate, new Vector3(0, -.38f, .03f), new Vector3(.13f, .26f, .15f));
            Box(leg, Metal, new Vector3(0, -.48f, .06f), new Vector3(.17f, .06f, .27f));
            return new UnitModel { Body = b.ToMesh("Assault"), Leg = leg.ToMesh("Assault leg"), Hips = new[] { new Vector3(-.13f, .51f, 0), new Vector3(.13f, .51f, 0) }, Scale = 1.2f, Height = 1.1f };
        }

        // Light infantry with a rail rifle held forward.
        static UnitModel Rifleman()
        {
            var b = new MeshBuilder();
            Box(b, Metal, new Vector3(0, .5f, 0), new Vector3(.24f, .1f, .16f));
            Box(b, Plate, new Vector3(0, .7f, 0), new Vector3(.3f, .32f, .22f));
            Box(b, Light, new Vector3(0, .72f, .11f), new Vector3(.24f, .2f, .05f), 1);
            Box(b, Metal, new Vector3(0, .73f, -.15f), new Vector3(.22f, .26f, .1f));
            b.Paint = Plate; b.Team = 0; b.Glow = 0; b.Place(new Vector3(0, .95f, .01f), Quaternion.identity, new Vector3(.11f, .12f, .12f)); b.Blob(7, .05f);
            Box(b, Cyan, new Vector3(0, .96f, .1f), new Vector3(.13f, .035f, .03f), 0, 1);
            for (int side = -1; side <= 1; side += 2) Box(b, Light, new Vector3(side * .2f, .83f, 0), new Vector3(.12f, .08f, .17f), 1);
            Box(b, Metal, new Vector3(.17f, .7f, .08f), new Vector3(.08f, .22f, .09f));
            Box(b, Metal, new Vector3(-.06f, .68f, .2f), new Vector3(.07f, .07f, .2f));
            Box(b, Dark, new Vector3(.1f, .72f, .26f), new Vector3(.06f, .07f, .62f));
            Box(b, Dark, new Vector3(.1f, .65f, .2f), new Vector3(.04f, .1f, .06f));
            Box(b, Cyan, new Vector3(.1f, .72f, .58f), new Vector3(.04f, .04f, .03f), 0, 1);
            var leg = new MeshBuilder();
            Box(leg, Metal, new Vector3(0, -.12f, 0), new Vector3(.1f, .26f, .12f));
            Box(leg, Plate, new Vector3(0, -.34f, .02f), new Vector3(.09f, .24f, .11f));
            Box(leg, Metal, new Vector3(0, -.44f, .05f), new Vector3(.11f, .05f, .2f));
            return new UnitModel { Body = b.ToMesh("Rifleman"), Leg = leg.ToMesh("Rifleman leg"), Hips = new[] { new Vector3(-.09f, .465f, 0), new Vector3(.09f, .465f, 0) }, Scale = 1.3f };
        }

        // Twin-rotor gunship; the body origin is its centre at hover height.
        static UnitModel Gunship()
        {
            var b = new MeshBuilder();
            b.Paint = Plate; b.Place(Vector3.zero, Quaternion.identity, new Vector3(.27f, .22f, .62f)); b.Blob(11, .04f);
            b.Paint = Cyan; b.Glow = .6f; b.Place(new Vector3(0, .08f, .36f), Quaternion.identity, new Vector3(.16f, .12f, .22f)); b.Blob(13, .03f); b.Glow = 0;
            Box(b, Light, new Vector3(0, .2f, -.02f), new Vector3(.12f, .04f, .7f), 1);
            Box(b, Metal, new Vector3(0, .04f, -.72f), new Vector3(.09f, .09f, .62f));
            Box(b, Light, new Vector3(0, .2f, -.98f), new Vector3(.03f, .26f, .2f), 1);
            Box(b, Metal, new Vector3(0, .06f, -.98f), new Vector3(.38f, .03f, .13f));
            Box(b, Metal, new Vector3(0, 0, 0), new Vector3(1.2f, .05f, .2f));
            Box(b, Dark, new Vector3(0, -.17f, .3f), new Vector3(.08f, .06f, .26f));
            Box(b, Blade, new Vector3(0, -.17f, .44f), new Vector3(.04f, .04f, .02f), 0, 1);
            var rotors = new[] { new Vector3(-.62f, .14f, 0), new Vector3(.62f, .14f, 0) };
            foreach (var at in rotors)
            {
                b.Paint = Metal; b.Team = 0; b.Glow = 0; b.Place(at + Vector3.down * .14f, Quaternion.Euler(90, 0, 0), Vector3.one);
                b.Prism(8, .1f, .09f, -.22f, .2f, 0, 0, true);
                b.Paint = Cyan; b.Glow = 1; b.Disc(.07f, 8, -.222f, true); b.Glow = 0;
            }
            var rotor = new MeshBuilder();
            rotor.Paint = Metal; rotor.Prism(6, .05f, .04f, -.02f, .05f);
            for (int i = 0; i < 3; i++)
            {
                rotor.Paint = Dark; rotor.Place(Vector3.zero, i * 120, 1);
                rotor.Box(new Vector3(0, .03f, .23f), new Vector3(.07f, .012f, .42f));
            }
            return new UnitModel { Body = b.ToMesh("Gunship"), Rotor = rotor.ToMesh("Gunship rotor"), Rotors = rotors, Hover = 2.4f };
        }

        // Hovering drone; the mesh origin is on the ground, the body floats above it.
        public static Mesh Worker()
        {
            var b = new MeshBuilder();
            b.Paint = Plate; b.Place(new Vector3(0, .42f, 0), Quaternion.identity, new Vector3(.3f, .19f, .36f)); b.Blob(3, .04f);
            b.Paint = Light; b.Team = 1; b.Place(new Vector3(0, .49f, -.03f), Quaternion.identity, new Vector3(.28f, .13f, .33f)); b.Blob(5, .03f);
            b.Team = 0; b.Transform = Matrix4x4.identity;
            b.Paint = Cyan; b.Glow = 1; b.Box(new Vector3(0, .45f, .3f), new Vector3(.17f, .055f, .07f));
            b.Paint = new Color(.4f, .8f, 1); b.Disc(.11f, 8, .27f, true); b.Glow = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                b.Paint = Metal; b.Place(new Vector3(side * .31f, .38f, 0), Quaternion.Euler(90, 0, 0), Vector3.one);
                b.Prism(6, .075f, .06f, -.15f, .13f, 0, 0, true);
                b.Paint = Cyan; b.Glow = 1; b.Disc(.05f, 6, -.152f, true); b.Glow = 0;
                b.Paint = Plate; b.Transform = Matrix4x4.identity; b.Box(new Vector3(side * .2f, .4f, 0), new Vector3(.12f, .05f, .1f));
            }
            b.Paint = Plate; b.Transform = Matrix4x4.identity; b.Place(new Vector3(.08f, .55f, -.12f)); b.Prism(4, .012f, .008f, 0, .2f);
            b.Paint = new Color(1, .35f, .2f); b.Glow = 1; b.Place(new Vector3(.08f, .76f, -.12f), Quaternion.identity, Vector3.one * .025f); b.Blob(1, 0, 0);
            return b.ToMesh("Worker drone");
        }

        // Kinds follow MatchBalance: 0 supply node, 1 factory, 2 headquarters. Origin is the footprint centre on the ground.
        public static Mesh Building(int kind, float w, float h)
        {
            var b = new MeshBuilder();
            void Box(Color color, Vector3 center, Vector3 size, float team = 0, float glow = 0)
            { b.Paint = color; b.Team = team; b.Glow = glow; b.Transform = Matrix4x4.identity; b.Box(center, size); }
            void Prism(Color color, Vector3 at, int sides, float r0, float r1, float y0, float y1, float team = 0, float glow = 0, float tip = 0, float turn = 0)
            { b.Paint = color; b.Team = team; b.Glow = glow; b.Place(at); b.Prism(sides, r0, r1, y0, y1, tip, turn); }
            if (kind == 0)
            {
                Box(Concrete, new Vector3(0, .08f, 0), new Vector3(w * .96f, .16f, w * .96f));
                Prism(Plate, Vector3.zero, 4, w * .52f, w * .4f, .16f, h * .6f, 0, 0, 0, .5f);
                Prism(Light, Vector3.zero, 4, w * .5f, w * .48f, h * .34f, h * .42f, 1, 0, 0, .5f);
                Box(Metal, new Vector3(0, h * .66f, 0), new Vector3(w * .44f, h * .12f, w * .44f));
                Box(Light, new Vector3(0, h * .73f, 0), new Vector3(w * .3f, .04f, w * .08f), 1, 1);
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                {
                    var corner = new Vector3(x * w * .37f, 0, z * w * .37f);
                    Prism(Light, corner, 8, .13f, .13f, .16f, h * .72f);
                    Prism(Light, corner, 8, .14f, .14f, h * .72f, h * .78f, 1);
                }
                Prism(Plate, new Vector3(w * .37f, 0, -w * .37f), 4, .02f, .012f, h * .78f, h * .97f);
                Box(new Color(1, .35f, .2f), new Vector3(w * .37f, h * .975f, -w * .37f), Vector3.one * .05f, 0, 1);
            }
            else if (kind == 1)
            {
                Box(Concrete, new Vector3(0, .09f, 0), new Vector3(w * .96f, .18f, w * .96f));
                float wallTop = .18f + h * .5f, length = w * .41f, radius = w * .34f, centerZ = w * .06f;
                Box(Plate, new Vector3(0, (.18f + wallTop) * .5f, centerZ), new Vector3(w * .82f, wallTop - .18f, radius * 2));
                // Arched roof: half an octagon extruded along X.
                b.Paint = Metal; b.Team = 0; b.Glow = 0; b.Transform = Matrix4x4.identity;
                for (int i = 0; i < 4; i++)
                {
                    float a0 = i / 4f * Mathf.PI, a1 = (i + 1) / 4f * Mathf.PI;
                    Vector3 p0 = new Vector3(0, wallTop + Mathf.Sin(a0) * radius * .6f, centerZ + Mathf.Cos(a0) * radius), p1 = new Vector3(0, wallTop + Mathf.Sin(a1) * radius * .6f, centerZ + Mathf.Cos(a1) * radius);
                    b.Quad(p0 + Vector3.left * length, p0 + Vector3.right * length, p1 + Vector3.right * length, p1 + Vector3.left * length);
                    b.Triangle(new Vector3(-length, wallTop, centerZ), p0 + Vector3.left * length, p1 + Vector3.left * length);
                    b.Triangle(new Vector3(length, wallTop, centerZ), p1 + Vector3.right * length, p0 + Vector3.right * length);
                }
                Box(Light, new Vector3(0, wallTop + radius * .6f + .02f, centerZ), new Vector3(w * .84f, .05f, .12f), 1);
                Box(Dark, new Vector3(0, .18f + h * .22f, centerZ - radius - .01f), new Vector3(w * .46f, h * .44f, .04f));
                Box(Light, new Vector3(0, .18f + h * .46f, centerZ - radius - .03f), new Vector3(w * .54f, .09f, .06f), 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(Light, new Vector3(side * w * .25f, .18f + h * .22f, centerZ - radius - .03f), new Vector3(.08f, h * .44f, .06f), 1);
                    Box(Metal, new Vector3(side * w * .43f, .18f + h * .18f, centerZ), new Vector3(.12f, h * .36f, radius * 1.6f));
                    var stack = new Vector3(side * w * .3f, 0, w * .32f);
                    Prism(Metal, stack, 8, .15f, .11f, .18f, h * .92f);
                    Prism(new Color(1, .55f, .2f), stack, 8, .115f, .115f, h * .92f, h * .97f, 0, 1);
                }
                Box(Cyan, new Vector3(0, .18f + h * .03f, centerZ - radius - .04f), new Vector3(w * .42f, .03f, .03f), 0, 1);
            }
            else
            {
                Prism(Concrete, Vector3.zero, 8, w * .5f, w * .48f, 0, .2f, 0, 0, 0, .5f);
                Prism(Plate, Vector3.zero, 8, w * .38f, w * .3f, .2f, h * .62f, 0, 0, 0, .5f);
                Prism(Light, Vector3.zero, 8, w * .385f, w * .37f, h * .3f, h * .4f, 1, 0, 0, .5f);
                Prism(Cyan, Vector3.zero, 8, w * .318f, w * .305f, h * .52f, h * .57f, 0, 1, 0, .5f);
                Prism(Metal, Vector3.zero, 8, w * .3f, w * .14f, h * .62f, h * .76f, 0, 0, 0, .5f);
                Prism(Plate, new Vector3(.12f, 0, .1f), 4, .03f, .02f, h * .76f, h * .97f);
                b.Paint = Light; b.Team = 1; b.Glow = 0; b.Place(new Vector3(-.15f, h * .8f, -.05f), Quaternion.Euler(-35, 20, 0), Vector3.one);
                b.Prism(8, .06f, .24f, 0, .07f);
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + .5f) / 4 * Mathf.PI * 2;
                    var pylon = new Vector3(Mathf.Cos(a) * w * .42f, 0, Mathf.Sin(a) * w * .42f);
                    Prism(Metal, pylon, 4, .12f, .07f, .2f, h * .42f, 0, 0, 0, .5f);
                    Prism(Light, pylon, 4, .07f, .02f, h * .42f, h * .47f, 1, 1, 0, .5f);
                }
            }
            return b.ToMesh("Building " + kind);
        }

        // Crystal cluster on one cell: tilted hexagonal prisms with glowing tips around a rocky base.
        public static Mesh Minerals(int seed)
        {
            var random = new System.Random(seed);
            float R(float a, float c) => a + (float)random.NextDouble() * (c - a);
            var b = new MeshBuilder();
            b.Paint = new Color(.2f, .21f, .24f);
            for (int i = 0; i < 3; i++)
            {
                float a = R(0, 6.28f);
                b.Place(new Vector3(Mathf.Cos(a) * .22f, 0, Mathf.Sin(a) * .22f), Quaternion.Euler(R(0, 40), R(0, 360), 0), new Vector3(R(.12f, .2f), R(.08f, .12f), R(.12f, .2f)));
                b.Blob(seed + i, .25f, 0);
            }
            int count = 4 + random.Next(3);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count + R(-.4f, .4f), distance = i == 0 ? 0 : R(.12f, .28f), radius = R(.08f, .13f), height = i == 0 ? R(.85f, 1.15f) : R(.4f, .85f);
                var color = Color.Lerp(new Color(.22f, .55f, 1), new Color(.38f, .86f, 1), R(0, 1));
                var position = new Vector3(Mathf.Cos(a) * distance, -.05f, Mathf.Sin(a) * distance);
                var tilt = Quaternion.AngleAxis(R(6, 24) * (i == 0 ? .3f : 1), Vector3.Cross(Vector3.up, position.sqrMagnitude > 0 ? position.normalized : Vector3.right));
                b.Transform = Matrix4x4.TRS(position, tilt * Quaternion.Euler(0, R(0, 60), 0), Vector3.one);
                b.Paint = color; b.Glow = .18f; b.Prism(6, radius, radius * .9f, 0, height, 0, 0);
                b.Paint = Color.Lerp(color, Color.white, .3f); b.Glow = .9f; b.Prism(6, radius * .9f, radius * .9f, height, height, radius * 1.9f);
            }
            return b.ToMesh("Mineral crystals");
        }

        // Gas vent: a rocky mound around a glowing pool; steam is a separate particle effect.
        public static Mesh Vent(int seed)
        {
            var random = new System.Random(seed);
            float R(float a, float c) => a + (float)random.NextDouble() * (c - a);
            var b = new MeshBuilder();
            b.Paint = new Color(.26f, .25f, .23f); b.Prism(10, .5f, .32f, -.05f, .17f, 0, 0);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2 + R(-.2f, .2f);
                b.Place(new Vector3(Mathf.Cos(a) * .4f, .12f, Mathf.Sin(a) * .4f), Quaternion.Euler(R(0, 30), R(0, 360), 0), new Vector3(R(.1f, .16f), R(.08f, .14f), R(.1f, .16f)));
                b.Blob(seed + i, .3f, 0);
            }
            b.Transform = Matrix4x4.identity;
            b.Paint = new Color(.2f, .32f, .22f); b.Glow = .25f; b.Ring(.2f, .3f, 10, .172f);
            b.Paint = new Color(.45f, 1, .55f); b.Glow = 1; b.Disc(.2f, 10, .175f);
            return b.ToMesh("Gas vent");
        }

        // Selection circle of unit radius: a bright rim over a faint fill; the material colour tints both.
        public static Mesh SelectionRing()
        {
            var b = new MeshBuilder();
            b.Paint = new Color(1, 1, 1, .13f); b.Disc(.86f, 48, 0);
            b.Paint = Color.white; b.Ring(.86f, 1, 48, .002f);
            return b.ToMesh("Selection circle");
        }

        // Move order marker: a small ring and four arrowheads pointing at it; the view shrinks and fades it.
        public static Mesh MoveMarker()
        {
            var b = new MeshBuilder();
            b.Paint = Color.white; b.Ring(.4f, .48f, 32, 0);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90 + 45) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), side = new Vector3(-direction.z, 0, direction.x);
                b.Triangle(direction * .6f, direction * .95f - side * .17f, direction * .95f + side * .17f);
            }
            return b.ToMesh("Move marker");
        }

        // Flat ring of unit radius for selection and construction markers.
        public static Mesh Ring(float inner)
        {
            var b = new MeshBuilder();
            b.Ring(inner, 1, 64, .03f);
            return b.ToMesh("Ring");
        }

        // Map pin: a thin pillar, a ground ring and a tall fading beam (shader Order/Glow with _FadeHeight).
        public static Mesh Beacon()
        {
            var b = new MeshBuilder();
            b.Prism(6, .07f, .04f, 0, 1.6f);
            b.Ring(.5f, .62f, 32, .04f);
            b.Prism(6, .18f, .18f, 0, 7);
            return b.ToMesh("Beacon");
        }
    }
}
