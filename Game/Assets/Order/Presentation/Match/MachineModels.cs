using Order.Content;
using UnityEngine;

namespace Order.Presentation
{
    // Generated low-poly models of the machine faction, "mercury and ice": polished grey metal, dark steel and cold
    // glowing seams; parts flagged Team take the player's colour. The combat roles keep the humans' models until the
    // machines get their own units.
    public static class MachineModels
    {
        static readonly Color Mercury = new Color(.42f, .45f, .51f), Steel = new Color(.14f, .16f, .19f), Pale = new Color(.62f, .67f, .74f),
            Ice = new Color(.62f, .85f, 1);

        public static UnitModel Unit(UnitKind kind) => kind == UnitKind.Worker ? new UnitModel { Body = Weaver(), Scale = 1.25f, Height = .75f } : MatchModels.Unit(kind);

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

        // Kinds follow FactionBalance: 0 compute node, 1 assembler, 2 core. Origin is the footprint centre on the ground.
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
