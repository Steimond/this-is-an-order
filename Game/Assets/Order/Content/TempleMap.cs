using System;
using System.Collections.Generic;

namespace Order.Content
{
    // Versioned, engine-independent map source; rendering and simulation share these cells.
    public sealed class TempleMap
    {
        public const int Size = 64;
        public const string Id = "temple-basin-v1";
        public readonly List<ResourceDeposit> Resources = new List<ResourceDeposit>();
        public TempleMap()
        {
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i < 7; i++) Add(c, 5 + i * 2, 5, false);
                Add(c, 5, 10, true);
                for (int i = 0; i < 5; i++) Add(c, 26 + i, 9, false);
                Add(c, 29, 13, true);
            }
        }
        void Add(int c, int x, int z, bool gas) => Resources.Add(new ResourceDeposit
        { X = (c & 1) == 0 ? x : 63-x, Z = (c & 2) == 0 ? z : 63-z, Gas = gas, Remaining = gas ? 2500 : 1500 });
        public bool Inside(int x, int z) => x >= 0 && z >= 0 && x < Size && z < Size;
        public bool Ramp(int x, int z)
        {
            int u = Math.Min(x, 63-x), v = Math.Min(z, 63-z);
            return u >= 20 && u < 26 && v >= 9 && v < 13;
        }
        public float Height(int x, int z, float dx = .5f)
        {
            int u = Math.Min(x, 63-x), v = Math.Min(z, 63-z);
            if (u < 20 && v < 20) return 3;
            if (!Ramp(x,z)) return 0;
            float along = x < 32 ? x+dx : 64-x-dx;
            return Math.Max(0, Math.Min(3, (26-along)*.5f));
        }
        public ResourceDeposit ResourceAt(int x, int z) => Resources.Find(r => r.X == x && r.Z == z && r.Remaining > 0);
    }
    public sealed class ResourceDeposit { public int X, Z, Remaining; public bool Gas; }
}
