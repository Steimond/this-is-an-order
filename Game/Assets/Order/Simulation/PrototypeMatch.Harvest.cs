using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // Mining trips in the spirit of StarCraft:
    // - a worker mines its deposit for the harvest interval, one worker per deposit at a time, then carries the load to the
    //   nearest finished command node and goes back for more;
    // - a busy deposit sends the worker to a free one of the same kind next to it, otherwise it waits its turn;
    // - a depleted deposit sends it to the nearest other one of the same kind within reach;
    // - workers in the mining cycle pass through other units (see Ghost in Movement).
    public sealed partial class PrototypeMatch
    {
        const float DropReach = .7f, NeighbourReach = 3.2f, SwitchReach = 9;
        readonly Dictionary<ResourceDeposit, PrototypeUnit> miners = new Dictionary<ResourceDeposit, PrototypeUnit>();
        // Everything delivered by workers in this match: the income, without refunds or cheats.
        public int MineralsHarvested { get; private set; }
        public int GasHarvested { get; private set; }

        void TickHarvest(float dt)
        {
            foreach (var u in Units)
            {
                var deposit = u.HarvestTarget;
                if (deposit == null) { Release(u); u.Mining = false; u.delivering = false; continue; }
                if (u.Carrying > 0)
                {
                    Release(u); u.Mining = false;
                    if (!u.delivering) { if (!Haul(u)) StopHarvest(u); continue; }
                    if (u.route.Count > 0) continue;
                    var drop = DropOff(u);
                    if (drop == null) { StopHarvest(u); continue; }
                    if (Gap(u, drop) > DropReach) { Haul(u); continue; }
                    if (u.CarriesGas) gasStock[u.Owner] += u.Carrying; else mineralStock[u.Owner] += u.Carrying;
                    if (u.Owner == Player) { if (u.CarriesGas) GasHarvested += u.Carrying; else MineralsHarvested += u.Carrying; }
                    u.Carrying = 0; u.delivering = false;
                    GatherFrom(u, deposit.Remaining > 0 ? deposit : Nearby(deposit, u, SwitchReach, false));
                    continue;
                }
                if (u.route.Count > 0) { Release(u); u.Mining = false; continue; }
                if (deposit.Remaining <= 0) { Release(u); u.Mining = false; GatherFrom(u, Nearby(deposit, u, SwitchReach, false)); continue; }
                if (!NextTo(u, deposit)) { Release(u); u.Mining = false; GatherFrom(u, deposit); continue; }
                if (miners.TryGetValue(deposit, out var other) && other != u && Holds(other, deposit))
                {
                    // Busy: try a free neighbour, otherwise wait here.
                    u.Mining = false;
                    var free = Nearby(deposit, u, NeighbourReach, true);
                    if (free != null) GatherFrom(u, free);
                    continue;
                }
                miners[deposit] = u; u.Mining = true;
                if ((u.harvestTimer += dt) < u.Stats.HarvestInterval) continue;
                u.harvestTimer = 0; u.Mining = false; Release(u);
                int amount = Math.Min(u.Stats.HarvestAmount, deposit.Remaining);
                deposit.Remaining -= amount;
                // A depleted deposit no longer blocks movement.
                if (deposit.Remaining == 0) NavigationChanged(deposit.X, deposit.Z, 1);
                u.Carrying = amount; u.CarriesGas = deposit.Gas;
                if (!Haul(u)) StopHarvest(u);
            }
        }
        bool Holds(PrototypeUnit u, ResourceDeposit deposit) => u.Alive && u.HarvestTarget == deposit && u.Carrying == 0 && u.route.Count == 0 && Units.Contains(u);
        void Release(PrototypeUnit u)
        {
            ResourceDeposit held = null;
            foreach (var pair in miners) if (pair.Value == u) { held = pair.Key; break; }
            if (held != null) miners.Remove(held);
            u.harvestTimer = 0;
        }
        static bool NextTo(PrototypeUnit u, ResourceDeposit d) => Math.Abs(u.X - d.X - .5f) <= 1.35f && Math.Abs(u.Z - d.Z - .5f) <= 1.35f;
        // The nearest finished command node of the worker's side.
        PrototypeBuilding DropOff(PrototypeUnit u)
        {
            PrototypeBuilding best = null; float bestGap = float.MaxValue;
            foreach (var b in Buildings)
            {
                if (b.Owner != u.Owner || b.Ready < 1 || !Trains(b, UnitKind.Worker)) continue;
                float gap = Gap(u, b);
                if (gap < bestGap) { bestGap = gap; best = b; }
            }
            return best;
        }
        // Heads for the drop-off, keeping the deposit to come back to.
        bool Haul(PrototypeUnit u)
        {
            var drop = DropOff(u);
            if (drop == null) return false;
            var (x, z) = Approach(u, drop);
            var deposit = u.HarvestTarget; var message = Message;
            PlanPath(u, x, z);
            u.HarvestTarget = deposit; u.delivering = true; Message = message;
            return true;
        }
        void GatherFrom(PrototypeUnit u, ResourceDeposit deposit)
        {
            if (deposit == null) { StopHarvest(u); return; }
            var message = Message;
            PlanPath(u, deposit.X + .5f, deposit.Z + .5f);
            Message = message;
            if (u.HarvestTarget == null) StopHarvest(u);
        }
        void StopHarvest(PrototypeUnit u) { Release(u); u.HarvestTarget = null; u.Mining = false; u.delivering = false; }
        // Nearest explored deposit of the same kind within reach of this one; `free` also skips deposits being mined.
        ResourceDeposit Nearby(ResourceDeposit from, PrototypeUnit u, float reach, bool free)
        {
            ResourceDeposit best = null; float bestDistance = reach * reach;
            foreach (var r in Map.Resources)
            {
                if (r == from || r.Gas != from.Gas || r.Remaining <= 0 || (u.Owner == Player && !Explored[r.X, r.Z])) continue;
                if (free && miners.TryGetValue(r, out var other) && Holds(other, r)) continue;
                float dx = r.X - from.X, dz = r.Z - from.Z, d = dx * dx + dz * dz;
                if (d <= bestDistance) { bestDistance = d; best = r; }
            }
            return best;
        }
    }
}
