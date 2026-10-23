using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // The machines' network (SETTING.md). Finished cores and relays are its nodes; each covers the ground within its
    // radius. Two nodes are linked when one stands inside the other's coverage, and a relay works while some path of
    // links leads from it to a core of its side: losing one relay switches off only what is left without any path.
    // Every core allows a few relays; relays beyond that (the newest first) stay dark. Machine buildings other than the
    // core are placed only inside the coverage and work only while covered: an uncovered building stops rising and
    // training, and a compute node outside it gives no supply. Units already built are never lost to a dark network.
    public sealed partial class PrototypeMatch
    {
        readonly List<PrototypeBuilding> nodes = new List<PrototypeBuilding>();
        readonly Queue<PrototypeBuilding> reach = new Queue<PrototypeBuilding>();

        void UpdateNetwork()
        {
            for (int side = 0; side < 2; side++)
            {
                if (!Roster(side).Network) { foreach (var b in Buildings) if (b.Owner == side) b.Powered = true; continue; }
                // Cores are the roots; the relays they allow, in the order they were placed, are the other nodes.
                int allowed = 0;
                foreach (var b in Buildings) if (b.Owner == side && b.Kind == FactionBalance.Main && b.Ready >= 1 && b.Health > 0) allowed += b.Stats.Relays;
                nodes.Clear(); reach.Clear();
                foreach (var b in Buildings)
                {
                    if (b.Owner != side) continue;
                    b.Powered = false;
                    // A building destroyed this step is no node, even before it is cleared away.
                    if (b.Health <= 0) continue;
                    if (b.Kind == FactionBalance.Main && b.Ready >= 1) { b.Powered = true; reach.Enqueue(b); }
                    else if (b.Kind == FactionBalance.Relay && b.Ready >= 1 && allowed > 0) { allowed--; nodes.Add(b); }
                }
                while (reach.Count > 0)
                {
                    var from = reach.Dequeue();
                    for (int i = nodes.Count - 1; i >= 0; i--)
                    {
                        var to = nodes[i];
                        if (Distance(from, to) > Math.Max(from.Stats.Coverage, to.Stats.Coverage)) continue;
                        to.Powered = true; reach.Enqueue(to); nodes.RemoveAt(i);
                    }
                }
                foreach (var b in Buildings)
                    if (b.Owner == side && b.Kind != FactionBalance.Main && (b.Kind != FactionBalance.Relay || b.Ready < 1))
                        b.Powered = Covered(side, b.X + b.Width * .5f, b.Z + b.Width * .5f);
            }
        }
        // Whether a point lies in the coverage of a working node of the side.
        public bool Covered(int side, float x, float z)
        {
            foreach (var b in Buildings)
            {
                if (b.Owner != side || b.Stats.Coverage <= 0 || b.Ready < 1 || !b.Powered) continue;
                float dx = b.X + b.Width * .5f - x, dz = b.Z + b.Width * .5f - z;
                if (dx * dx + dz * dz <= b.Stats.Coverage * b.Stats.Coverage) return true;
            }
            return false;
        }
        // Relays the side may still place: what its cores allow, less those standing, rising or on their way.
        public int RelaysLeft(int side)
        {
            int allowed = 0, used = 0;
            foreach (var b in Buildings) if (b.Owner == side) { if (b.Kind == FactionBalance.Main && b.Ready >= 1) allowed += b.Stats.Relays; else if (b.Kind == FactionBalance.Relay) used++; }
            foreach (var u in Units) if (u.Owner == side && u.SiteKind == FactionBalance.Relay) used++;
            return allowed - used;
        }
        static float Distance(PrototypeBuilding a, PrototypeBuilding b)
        {
            float dx = a.X + a.Width * .5f - b.X - b.Width * .5f, dz = a.Z + a.Width * .5f - b.Z - b.Width * .5f;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
