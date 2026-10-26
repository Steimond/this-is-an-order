using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // Units by 4×4-cell block, filled at the start of every step, for the searches of combat (whom to target, who comes to
    // help, whom a shell splashes): a search looks only at the blocks round its point instead of at every unit. It lists
    // the units in the order of Units, so it finds exactly what a pass over all units would, ties included. The blocks
    // hold the positions of the start of the step, which stay true while units pick targets and strike (Fight); later in
    // the step, when units move, a search goes over every unit as before.
    public sealed partial class PrototypeMatch
    {
        const int Block = 4;
        // Blocks across the map; the last ones may stick out past its edge.
        int Blocks => (Size + Block - 1) / Block;
        List<PrototypeUnit>[] blocks;
        readonly List<PrototypeUnit> near = new List<PrototypeUnit>();
        static readonly System.Comparison<PrototypeUnit> ByIndex = (a, b) => a.index.CompareTo(b.index);
        // The widest unit on the map this step: a search for edges within a reach looks this much farther for centres.
        Fix widest;
        bool blocksTrue;

        void FillBlocks()
        {
            if (blocks == null) blocks = new List<PrototypeUnit>[Blocks * Blocks];
            for (int i = 0; i < blocks.Length; i++) { if (blocks[i] == null) blocks[i] = new List<PrototypeUnit>(); else blocks[i].Clear(); }
            widest = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i]; u.index = i;
                blocks[BlockOf(u.Z) * Blocks + BlockOf(u.X)].Add(u);
                if (u.Stats.Radius > widest) widest = u.Stats.Radius;
            }
            blocksTrue = true;
        }
        int BlockOf(Fix v) => Math.Max(0, Math.Min(Blocks - 1, (int)v / Block));
        // Units whose centre may lie within `reach` of the point; in the order of Units when `ordered`, otherwise in any order
        // with fresh `index`es, for searches that break ties by index or do not depend on the order. The list is shared: use
        // it up before the next search.
        List<PrototypeUnit> Near(Fix x, Fix z, Fix reach, bool ordered = true)
        {
            near.Clear();
            if (!blocksTrue) { for (int i = 0; i < Units.Count; i++) { Units[i].index = i; near.Add(Units[i]); } return near; }
            int x0 = BlockOf(x - reach), x1 = BlockOf(x + reach), z0 = BlockOf(z - reach), z1 = BlockOf(z + reach);
            for (int bz = z0; bz <= z1; bz++) for (int bx = x0; bx <= x1; bx++) near.AddRange(blocks[bz * Blocks + bx]);
            if (ordered && (x0 != x1 || z0 != z1)) near.Sort(ByIndex);
            return near;
        }
    }
}
