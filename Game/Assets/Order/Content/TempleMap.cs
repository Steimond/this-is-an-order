using System;
using System.Collections.Generic;

namespace Order.Content
{
    // Versioned, engine-independent map source; rendering and simulation share these cells. Everything sized by the map
    // (the simulation's grids, the terrain, fog and minimap) takes Size from here, so another map may have another.
    // Temple Basin is 64×64: four plateaus in the corners, mirrored, each with a ramp down into the basin.
    public sealed class TempleMap
    {
        const int Side = 64, Last = Side - 1, Middle = Side / 2;
        public const string Id = "temple-basin-v1";
        public int Size => Side;
        public readonly List<ResourceDeposit> Resources = new List<ResourceDeposit>();
        // Where a main building (3×3) stands at each base, corner of the footprint: the four starts on the plateaus, then
        // the four expansions in the basin below their ramps.
        public readonly List<(int x, int z)> Bases = new List<(int, int)>();
        public TempleMap()
        {
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i < 7; i++) Add(c, 5 + i * 2, 5, false);
                Add(c, 5, 10, true);
                for (int i = 0; i < 5; i++) Add(c, 26 + i, 9, false);
                Add(c, 29, 13, true);
            }
            for (int c = 0; c < 4; c++) Bases.Add(Base(c, 10, 10));
            for (int c = 0; c < 4; c++) Bases.Add(Base(c, 24, 14));
        }
        static (int, int) Base(int c, int x, int z) => ((c & 1) == 0 ? x : Last - 2 - x, (c & 2) == 0 ? z : Last - 2 - z);
        void Add(int c, int x, int z, bool plasma) => Resources.Add(new ResourceDeposit
        { X = (c & 1) == 0 ? x : Last-x, Z = (c & 2) == 0 ? z : Last-z, Plasma = plasma, Remaining = plasma ? 2500 : 1500 });
        public bool Inside(int x, int z) => x >= 0 && z >= 0 && x < Size && z < Size;
        public bool Ramp(int x, int z)
        {
            int u = Math.Min(x, Last-x), v = Math.Min(z, Last-z);
            return u >= 20 && u < 26 && v >= 9 && v < 13;
        }
        // The height of a cell, on a ramp at dx across the cell along its slope (by default its middle). Fix, for the
        // simulation; the view converts.
        public Fix Height(int x, int z) => Height(x, z, Fix.Half);
        public Fix Height(int x, int z, Fix dx)
        {
            int u = Math.Min(x, Last-x), v = Math.Min(z, Last-z);
            if (u < 20 && v < 20) return 3;
            if (!Ramp(x,z)) return 0;
            Fix along = x < Middle ? x+dx : Side-x-dx;
            return Fix.Max(0, Fix.Min(3, (26-along)*Fix.Half));
        }
        public ResourceDeposit ResourceAt(int x, int z) => Resources.Find(r => r.X == x && r.Z == z && r.Remaining > 0);
        // The middle of the map, where the armies of all bases meet.
        public (Fix x, Fix z) Centre => (Middle, Middle);
        // Where the army of a base near this point gathers: on its plateau at the top of the ramp, in the quarter of the
        // map the point lies in.
        public (Fix x, Fix z) Staging(Fix x, Fix z)
        {
            Fix sx = (Fix)17.5f, sz = (Fix)11.5f;
            return (x < Middle ? sx : Side - sx, z < Middle ? sz : Side - sz);
        }
    }
    // WrappedBy: the side whose machine nanites have wrapped the deposit (-1: none); the wrap outlives its worker.
    public sealed class ResourceDeposit { public int X, Z, Remaining, WrappedBy = -1; public bool Plasma; }
}
