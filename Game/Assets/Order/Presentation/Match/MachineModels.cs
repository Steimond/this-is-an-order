using Order.Content;
using UnityEngine;

namespace Order.Presentation
{
    // Generated low-poly models of the machine faction, "mercury and ice": polished grey metal, dark steel and cold
    // glowing seams; parts flagged Team take the player's colour. The glider keeps the humans' gunship model until the
    // machines get their own air units.
    public static class MachineModels
    {
        static readonly Color Mercury = new Color(.42f, .45f, .51f), Steel = new Color(.14f, .16f, .19f), Pale = new Color(.62f, .67f, .74f),
            Ice = new Color(.62f, .85f, 1);

        public static UnitModel Unit(UnitKind kind) =>
            kind == UnitKind.Worker ? new UnitModel { Body = Weaver(), Scale = 1.25f, Height = .75f }
            : kind == UnitKind.Melee ? Spider() : kind == UnitKind.Leaper ? Leaper() : kind == UnitKind.Ranged ? Emitter() : MatchModels.Unit(kind);

        // Emitter: a tall faceted mast on two pairs of thin legs, a glowing collar, and a lens at the front of its head that
        // throws the beam.
        static UnitModel Emitter()
        {
            var b = new MeshBuilder();
            b.Paint = Mercury; b.Place(new Vector3(0, .42f, 0)); b.Prism(6, .13f, .09f, 0, .34f, 0, .5f, true);
            b.Paint = Pale; b.Team = 1; b.Place(new Vector3(0, .6f, 0)); b.Prism(6, .12f, .12f, 0, .06f, 0, .5f); b.Team = 0;
            b.Paint = Steel; b.Place(new Vector3(0, .8f, .02f), Quaternion.identity, new Vector3(.18f, .14f, .2f)); b.Blob(31, .04f, 0);
            b.Paint = Steel; b.Place(new Vector3(0, .8f, .1f), Quaternion.Euler(90, 0, 0), Vector3.one); b.Prism(8, .065f, .075f, 0, .1f);
            b.Paint = Ice; b.Glow = 1; b.Place(new Vector3(0, .8f, .2f), Quaternion.Euler(90, 0, 0), Vector3.one); b.Disc(.06f, 8, 0); b.Glow = 0;
            b.Paint = Ice; b.Glow = .7f; b.Transform = Matrix4x4.identity; b.Box(new Vector3(0, .63f, .125f), new Vector3(.06f, .02f, .02f)); b.Glow = 0;
            var leg = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                Limb(leg, Steel, new Vector3(side * .06f, 0, 0), new Vector3(side * .22f, .05f, 0), .035f);
                Limb(leg, Mercury, new Vector3(side * .22f, .05f, 0), new Vector3(side * .27f, -.42f, 0), .028f);
            }
            return new UnitModel { Body = b.ToMesh("Emitter"), Leg = leg.ToMesh("Emitter legs"),
                Hips = new[] { new Vector3(0, .44f, .08f), new Vector3(0, .44f, -.08f) }, Scale = 1.05f, Height = .92f };
        }

        // A straight limb between two points, a thin four-sided bar.
        static void Limb(MeshBuilder b, Color color, Vector3 from, Vector3 to, float thickness, float glow = 0)
        {
            var along = to - from;
            b.Paint = color; b.Team = 0; b.Glow = glow;
            b.Place((from + to) * .5f, Quaternion.FromToRotation(Vector3.up, along), new Vector3(thickness, along.magnitude, thickness));
            b.Prism(4, .5f, .5f, -.5f, .5f, 0, .5f, true);
        }

        // Spider: a small low body on three pairs of legs; the pairs swing in turn as it runs.
        static UnitModel Spider()
        {
            var b = new MeshBuilder();
            b.Paint = Mercury; b.Place(new Vector3(0, .2f, -.05f), Quaternion.identity, new Vector3(.24f, .12f, .3f)); b.Blob(11, .06f, 0);
            b.Paint = Pale; b.Team = 1; b.Place(new Vector3(0, .26f, -.08f), Quaternion.identity, new Vector3(.16f, .06f, .2f)); b.Blob(12, .04f, 0); b.Team = 0;
            b.Paint = Steel; b.Place(new Vector3(0, .2f, .14f), Quaternion.identity, new Vector3(.12f, .09f, .12f)); b.Blob(13, .05f, 0);
            b.Paint = Ice; b.Glow = 1; b.Transform = Matrix4x4.identity; b.Box(new Vector3(0, .22f, .2f), new Vector3(.08f, .025f, .03f)); b.Glow = 0;
            var leg = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                Limb(leg, Steel, new Vector3(side * .05f, 0, 0), new Vector3(side * .22f, .1f, 0), .03f);
                Limb(leg, Mercury, new Vector3(side * .22f, .1f, 0), new Vector3(side * .32f, -.18f, 0), .025f);
            }
            return new UnitModel { Body = b.ToMesh("Spider"), Leg = leg.ToMesh("Spider legs"),
                Hips = new[] { new Vector3(0, .18f, .1f), new Vector3(0, .18f, -.01f), new Vector3(0, .18f, -.12f) }, Scale = 1, Height = .42f };
        }

        // Leaper: a hunched body on two powerful hind legs, with two blades forward to strike as it jumps through.
        static UnitModel Leaper()
        {
            var b = new MeshBuilder();
            b.Paint = Mercury; b.Place(new Vector3(0, .5f, 0), Quaternion.Euler(-20, 0, 0), new Vector3(.3f, .22f, .42f)); b.Blob(21, .05f, 0);
            b.Paint = Pale; b.Team = 1; b.Place(new Vector3(0, .58f, -.04f), Quaternion.Euler(-20, 0, 0), new Vector3(.2f, .08f, .3f)); b.Blob(22, .03f, 0); b.Team = 0;
            b.Paint = Steel; b.Place(new Vector3(0, .6f, .2f), Quaternion.identity, new Vector3(.15f, .11f, .14f)); b.Blob(23, .04f, 0);
            b.Paint = Ice; b.Glow = 1; b.Transform = Matrix4x4.identity; b.Box(new Vector3(0, .62f, .27f), new Vector3(.1f, .03f, .03f)); b.Glow = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                Limb(b, Steel, new Vector3(side * .12f, .5f, .12f), new Vector3(side * .16f, .36f, .34f), .04f);
                Limb(b, Ice, new Vector3(side * .16f, .36f, .34f), new Vector3(side * .12f, .44f, .56f), .025f, 1);
            }
            var leg = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                Limb(leg, Steel, new Vector3(side * .13f, 0, 0), new Vector3(side * .19f, -.14f, -.18f), .07f);
                Limb(leg, Mercury, new Vector3(side * .19f, -.14f, -.18f), new Vector3(side * .16f, -.36f, .02f), .05f);
                Limb(leg, Steel, new Vector3(side * .16f, -.36f, .02f), new Vector3(side * .16f, -.38f, .1f), .05f);
            }
            return new UnitModel { Body = b.ToMesh("Leaper"), Leg = leg.ToMesh("Leaper legs"), Hips = new[] { new Vector3(0, .4f, -.1f) }, Scale = 1.1f, Height = .8f };
        }

        // The weaver: a hovering faceted core with a glowing eye and three hooked arms that wrap deposits with nanites.
        public static Mesh Weaver()
        {
            var b = new MeshBuilder();
            b.Paint = Mercury; b.Place(new Vector3(0, .34f, 0)); b.Prism(6, .1f, .2f, 0, .12f, 0, .5f);
            b.Paint = Pale; b.Team = 1; b.Place(new Vector3(0, .46f, 0)); b.Prism(6, .2f, .13f, 0, .1f, .12f, .5f); b.Team = 0;
            b.Paint = Ice; b.Glow = 1; b.Transform = Matrix4x4.identity; b.Box(new Vector3(0, .44f, .17f), new Vector3(.12f, .05f, .05f));
            b.Paint = Ice; b.Disc(.07f, 6, .335f, true); b.Glow = 0;
            for (int i = 0; i < 3; i++)
            {
                float a = (i / 3f + .25f) * 360;
                var arm = Quaternion.Euler(0, a, 0);
                b.Paint = Steel; b.Place(arm * new Vector3(0, .38f, .16f), arm * Quaternion.Euler(35, 0, 0), Vector3.one);
                b.Prism(4, .03f, .02f, 0, .2f, 0, .5f);
                b.Paint = Mercury; b.Place(arm * new Vector3(0, .25f, .3f), arm * Quaternion.Euler(150, 0, 0), Vector3.one);
                b.Prism(4, .022f, .006f, 0, .14f, 0, .5f);
                b.Paint = Ice; b.Glow = 1; b.Place(arm * new Vector3(0, .4f, .2f), Quaternion.identity, Vector3.one * .02f); b.Blob(i, 0, 0); b.Glow = 0;
            }
            return b.ToMesh("Weaver");
        }

        // Kinds follow FactionBalance: 0 compute node, 1 assembler, 2 core, 3 relay, 4 firmware node. Origin is the footprint centre on the ground.
        public static Mesh Building(int kind, float w, float h)
        {
            var b = new MeshBuilder();
            void Prism(Color color, Vector3 at, int sides, float r0, float r1, float y0, float y1, float team = 0, float glow = 0, float tip = 0, float turn = 0)
            { b.Paint = color; b.Team = team; b.Glow = glow; b.Place(at); b.Prism(sides, r0, r1, y0, y1, tip, turn); }
            void Slab(Color color, float y, float r, float thick, float turn, float team = 0, float glow = 0)
            { b.Paint = color; b.Team = team; b.Glow = glow; b.Place(new Vector3(0, y, 0), turn * 60, 1); b.Prism(6, r, r * .96f, 0, thick); }
            // Every building stands on a low hexagonal plinth of dark steel with a glowing rim.
            Prism(Steel, Vector3.zero, 6, w * .5f, w * .47f, 0, .14f, 0, 0, 0, .5f);
            Prism(Ice, Vector3.zero, 6, w * .47f, w * .47f, .14f, .155f, 0, 1, 0, .5f);
            if (kind == 0)
            {
                // Compute node: a stack of turned slabs with glowing seams between them.
                for (int i = 0; i < 4; i++)
                {
                    float y = .16f + i * h * .19f, r = w * (.38f - i * .04f);
                    Slab(i == 3 ? Pale : Mercury, y, r, h * .15f, i * .5f, i == 3 ? 1 : 0);
                    Slab(Ice, y + h * .15f, r * .9f, h * .04f, i * .5f, 0, 1);
                }
                Prism(Pale, new Vector3(0, .16f + h * .76f, 0), 6, w * .12f, 0, 0, h * .16f, 1, 0, h * .08f);
            }
            else if (kind == 1)
            {
                // Assembler: three leaning pylons over a glowing assembly pad, a ring floating where they meet.
                Prism(Mercury, Vector3.zero, 6, w * .36f, w * .3f, .15f, .3f, 0, 0, 0, .5f);
                Prism(Ice, Vector3.zero, 6, w * .26f, w * .26f, .3f, .31f, 0, 1, 0, .5f);
                for (int i = 0; i < 3; i++)
                {
                    float a = i / 3f * 360 + 30;
                    var turn = Quaternion.Euler(0, a, 0);
                    b.Paint = Steel; b.Team = 0; b.Glow = 0; b.Place(turn * new Vector3(0, .15f, w * .4f), turn * Quaternion.Euler(-24, 0, 0), Vector3.one);
                    b.Prism(4, .16f, .08f, 0, h * .85f, 0, .5f);
                    b.Paint = Pale; b.Team = 1; b.Place(turn * new Vector3(0, .15f + h * .78f, w * .4f - h * .33f), turn * Quaternion.Euler(-24, 0, 0), Vector3.one);
                    b.Prism(4, .085f, .03f, 0, h * .14f, 0, .5f);
                }
                b.Team = 0;
                b.Paint = Ice; b.Glow = 1; b.Transform = Matrix4x4.identity; b.Ring(w * .16f, w * .21f, 12, h * .7f); b.Glow = 0;
            }
            else if (kind == 3)
            {
                // Relay: a slim mast with three rings round it and a glowing emitter on top.
                Prism(Steel, Vector3.zero, 4, w * .22f, w * .1f, .15f, h * .82f, 0, 0, 0, .5f);
                for (int i = 0; i < 3; i++)
                {
                    float y = h * (.3f + i * .2f), r = w * (.34f - i * .06f);
                    b.Paint = i == 1 ? Pale : Mercury; b.Team = i == 1 ? 1 : 0; b.Glow = 0; b.Transform = Matrix4x4.identity; b.Ring(r * .55f, r, 8, y);
                    b.Paint = Ice; b.Team = 0; b.Glow = 1; b.Ring(r * .5f, r * .55f, 8, y + .005f);
                }
                Prism(Ice, new Vector3(0, h * .82f, 0), 6, w * .1f, w * .1f, 0, 0, 0, 1, h * .16f, .5f);
            }
            else if (kind == 4)
            {
                // Firmware node: a dark monolith split by a glowing seam, with three data panes turning round it.
                Prism(Steel, Vector3.zero, 4, w * .24f, w * .2f, .15f, h * .9f, 0, 0, 0, .5f);
                Prism(Ice, Vector3.zero, 4, w * .245f, w * .245f, h * .45f, h * .5f, 0, 1, 0, .5f);
                Prism(Pale, Vector3.zero, 4, w * .2f, w * .2f, h * .9f, h * .9f, 1, 0, h * .1f, .5f);
                for (int i = 0; i < 3; i++)
                {
                    float a = i / 3f * Mathf.PI * 2;
                    var at = new Vector3(Mathf.Cos(a) * w * .36f, h * (.35f + i * .12f), Mathf.Sin(a) * w * .36f);
                    b.Paint = Ice; b.Team = 0; b.Glow = .8f; b.Place(at, Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, 0), new Vector3(.22f, .3f, .02f));
                    b.Box(Vector3.zero, Vector3.one);
                }
            }
            else
            {
                // Core: a faceted spire of mercury split by a band of team colour, with glowing seams and a floating crown.
                Prism(Mercury, Vector3.zero, 6, w * .34f, w * .26f, .15f, h * .45f, 0, 0, 0, .5f);
                Prism(Pale, Vector3.zero, 6, w * .265f, w * .25f, h * .45f, h * .53f, 1, 0, 0, .5f);
                Prism(Mercury, Vector3.zero, 6, w * .25f, w * .12f, h * .53f, h * .8f, 0, 0, 0, .5f);
                Prism(Pale, Vector3.zero, 6, w * .12f, w * .12f, h * .8f, h * .8f, 0, 0, h * .16f, .5f);
                for (int i = 0; i < 6; i++)
                {
                    float a = (i + .5f) / 6 * Mathf.PI * 2;
                    var at = new Vector3(Mathf.Cos(a) * w * .3f, 0, Mathf.Sin(a) * w * .3f);
                    Prism(Ice, at, 4, .025f, .015f, .2f, h * .44f, 0, 1, 0, .5f);
                }
                b.Paint = Ice; b.Glow = 1; b.Transform = Matrix4x4.identity; b.Ring(w * .2f, w * .25f, 12, h * .9f);
                b.Paint = Pale; b.Team = 1; b.Glow = 0; b.Ring(w * .25f, w * .29f, 12, h * .9f); b.Team = 0;
            }
            return b.ToMesh("Machine building " + kind);
        }

        // A piece of the construction grenade: a small double pyramid of mercury with a glowing core band.
        public static Mesh GrenadePiece()
        {
            var b = new MeshBuilder();
            b.Paint = Mercury; b.Place(Vector3.zero); b.Prism(4, .1f, .1f, 0, 0, .16f, .5f);
            b.Paint = Pale; b.Team = 1; b.Place(Vector3.zero, Quaternion.Euler(180, 0, 0), Vector3.one); b.Prism(4, .1f, .1f, 0, 0, .12f, .5f); b.Team = 0;
            b.Paint = Ice; b.Glow = 1; b.Place(Vector3.zero); b.Prism(4, .105f, .105f, -.015f, .015f, 0, .5f);
            return b.ToMesh("Grenade piece");
        }

        // The nanite cocoon round a wrapped worker: a faceted shell, drawn translucent and glowing.
        public static Mesh Cocoon()
        {
            var b = new MeshBuilder();
            b.Paint = Color.white; b.Glow = 1;
            b.Place(new Vector3(0, .42f, 0), Quaternion.identity, new Vector3(.36f, .3f, .36f)); b.Blob(7, .08f, 1);
            return b.ToMesh("Nanite cocoon");
        }
    }
}
