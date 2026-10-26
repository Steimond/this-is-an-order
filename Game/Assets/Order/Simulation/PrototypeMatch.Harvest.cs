using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // Mining trips of the humans, in the spirit of StarCraft (the machines wrap deposits instead, see Weave):
    // - a worker mines its deposit for the harvest interval, one worker per deposit at a time, then carries the load to the
    //   nearest finished command node and goes back for more;
    // - a busy deposit sends the worker to a free one of the same kind next to it, otherwise it waits its turn;
    // - a depleted deposit sends it to the nearest other one of the same kind within reach;
    // - workers in the mining cycle pass through other units (see Ghost in Movement);
    // - plasma is mined through an extractor standing on the rift, and only by its owner (see MiningBlocked).
    public sealed partial class PrototypeMatch
    {
        static readonly Fix DropReach = (Fix).7f, NeighbourReach = (Fix)3.2f, SwitchReach = 9;
        readonly Dictionary<ResourceDeposit, PrototypeUnit> miners = new Dictionary<ResourceDeposit, PrototypeUnit>();
        // Everything the player's workers delivered or mined in this match: the income, without refunds or cheats.
        public int MetalHarvested { get; private set; }
        public int PlasmaHarvested { get; private set; }

        void TickHarvest(Fix dt)
        {
            foreach (var u in Units)
            {
                if (u.Kind == UnitKind.Worker && Roster(u.Owner).NaniteMining) { Weave(u, dt); continue; }
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
                    if (u.CarriesPlasma) plasmaStock[u.Owner] += u.Carrying; else metalStock[u.Owner] += u.Carrying;
                    if (u.Owner == Player) { if (u.CarriesPlasma) PlasmaHarvested += u.Carrying; else MetalHarvested += u.Carrying; }
                    u.Carrying = 0; u.delivering = false;
                    GatherFrom(u, deposit.Remaining > 0 ? deposit : Nearby(deposit, u, SwitchReach, false));
                    continue;
                }
                if (u.route.Count > 0) { Release(u); u.Mining = false; continue; }
                if (deposit.Remaining <= 0) { Release(u); u.Mining = false; GatherFrom(u, Nearby(deposit, u, SwitchReach, false)); continue; }
                if (!NextTo(u, deposit)) { Release(u); u.Mining = false; GatherFrom(u, deposit); continue; }
                // The extractor it mined through is gone, or the rift was taken.
                var closed = MiningBlocked(u.Owner, deposit);
                if (closed != null) { StopHarvest(u); if (u.Owner == Player) Message = closed; continue; }
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
                u.Carrying = amount; u.CarriesPlasma = deposit.Plasma;
                if (!Haul(u)) StopHarvest(u);
            }
        }
        // Nanite mining of the machines (SETTING.md): no trips. The worker wraps the deposit (WrapSeconds), then stays on
        // it and adds a portion to its side's stock every tick. One worker per deposit, held for as long as it stays; a
        // busy deposit sends it to a free one nearby. While wrapped it wears a cocoon that takes blows first and grows back
        // once it has not been hit for a while; leaving the deposit dissolves it. The wrap outlives its worker: another
        // worker of the side attaches to a deposit wrapped before at once.
        static readonly Fix CocoonCalm = 3;
        void Weave(PrototypeUnit u, Fix dt)
        {
            var deposit = u.HarvestTarget; var stats = u.Stats;
            bool there = deposit != null && u.route.Count == 0 && deposit.Remaining > 0 && NextTo(u, deposit);
            if (!there) { Release(u); u.Mining = false; u.Wrap = 0; u.Cocoon = 0; u.cocoonGrowth = 0; }
            if (deposit == null || u.route.Count > 0) return;
            if (deposit.Remaining <= 0) { GatherFrom(u, Nearby(deposit, u, SwitchReach, true)); return; }
            if (!NextTo(u, deposit)) { GatherFrom(u, deposit); return; }
            var closed = MiningBlocked(u.Owner, deposit);
            if (closed != null) { StopHarvest(u); if (u.Owner == Player) Message = closed; return; }
            if (miners.TryGetValue(deposit, out var other) && other != u && Holds(other, deposit))
            {
                var free = Nearby(deposit, u, SwitchReach, true);
                if (free != null) { GatherFrom(u, free); return; }
                StopHarvest(u);
                if (u.Owner == Player) Message = "Свободных залежей рядом нет: на каждой уже работает рабочий.";
                return;
            }
            miners[deposit] = u;
            if (u.Wrap < 1)
            {
                Fix before = u.Wrap;
                u.Wrap = deposit.WrappedBy == u.Owner || stats.WrapSeconds <= 0 ? 1 : Math.Min(1, u.Wrap + dt / stats.WrapSeconds);
                // A fresh wrap grows the cocoon with it; on a deposit wrapped before the cocoon grows back as usual.
                if (deposit.WrappedBy != u.Owner) u.Cocoon = Math.Min(stats.Cocoon, u.Cocoon + (int)(stats.Cocoon * u.Wrap) - (int)(stats.Cocoon * before));
                if (u.Wrap < 1) return;
                deposit.WrappedBy = u.Owner; u.harvestTimer = 0;
            }
            u.Mining = true;
            if (u.sinceHit >= CocoonCalm && u.Cocoon < stats.Cocoon)
            {
                u.cocoonGrowth += stats.CocoonRegen * dt;
                int grown = (int)u.cocoonGrowth; u.cocoonGrowth -= grown;
                u.Cocoon = Math.Min(stats.Cocoon, u.Cocoon + grown);
            }
            if ((u.harvestTimer += dt) < stats.HarvestInterval) return;
            u.harvestTimer = 0;
            int amount = Math.Min(stats.HarvestAmount, deposit.Remaining);
            deposit.Remaining -= amount;
            if (deposit.Remaining == 0) NavigationChanged(deposit.X, deposit.Z, 1);
            if (deposit.Plasma) plasmaStock[u.Owner] += amount; else metalStock[u.Owner] += amount;
            if (u.Owner == Player) { if (deposit.Plasma) PlasmaHarvested += amount; else MetalHarvested += amount; }
        }
        // The extractor standing on the rift, if any.
        public PrototypeBuilding ExtractorOn(ResourceDeposit d)
        {
            foreach (var b in Buildings) if (b.Stats.Extracts && b.Health > 0 && d.X >= b.X && d.X < b.X + b.Width && d.Z >= b.Z && d.Z < b.Z + b.Width) return b;
            return null;
        }
        // Why the side cannot mine the deposit, or null. As with refineries in StarCraft, a rift under an extractor is the
        // extractor owner's alone, once it is finished; a side that has extractors (the humans) mines plasma only through one.
        public string MiningBlocked(int side, ResourceDeposit d)
        {
            var extractor = ExtractorOn(d);
            if (extractor != null) return extractor.Owner != side ? "Разлом занят экстрактором противника." : extractor.Ready < 1 ? "Экстрактор ещё строится." : null;
            return d.Plasma && Roster(side).Extractor >= 0 ? $"Сначала постройте на разломе: {Roster(side).Buildings[Roster(side).Extractor].Name.ToLowerInvariant()}." : null;
        }
        bool Holds(PrototypeUnit u, ResourceDeposit deposit) => u.Alive && u.HarvestTarget == deposit && u.Carrying == 0 && u.route.Count == 0 && Units.Contains(u);
        void Release(PrototypeUnit u)
        {
            ResourceDeposit held = null;
            foreach (var pair in miners) if (pair.Value == u) { held = pair.Key; break; }
            if (held != null) miners.Remove(held);
            u.harvestTimer = 0;
        }
        static bool NextTo(PrototypeUnit u, ResourceDeposit d) => Math.Abs(u.X - d.X - (Fix).5f) <= (Fix)1.35f && Math.Abs(u.Z - d.Z - (Fix).5f) <= (Fix)1.35f;
        // The nearest finished command node of the worker's side.
        PrototypeBuilding DropOff(PrototypeUnit u)
        {
            PrototypeBuilding best = null; Fix bestGap = Fix.MaxValue;
            foreach (var b in Buildings)
            {
                if (b.Owner != u.Owner || b.Ready < 1 || !Trains(b, UnitKind.Worker)) continue;
                Fix gap = Gap(u, b);
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
            PlanPath(u, deposit.X + (Fix).5f, deposit.Z + (Fix).5f);
            Message = message;
            if (u.HarvestTarget == null) StopHarvest(u);
        }
        void StopHarvest(PrototypeUnit u) { Release(u); u.HarvestTarget = null; u.Mining = false; u.delivering = false; }
        // Nearest explored deposit of the same kind within reach of this one; `free` also skips deposits being mined.
        ResourceDeposit Nearby(ResourceDeposit from, PrototypeUnit u, Fix reach, bool free)
        {
            ResourceDeposit best = null; Fix bestDistance = reach * reach;
            foreach (var r in Map.Resources)
            {
                if (r == from || r.Plasma != from.Plasma || r.Remaining <= 0 || (u.Owner == Player && !Explored[r.X, r.Z]) || MiningBlocked(u.Owner, r) != null) continue;
                if (free && miners.TryGetValue(r, out var other) && Holds(other, r)) continue;
                Fix dx = r.X - from.X, dz = r.Z - from.Z, d = dx * dx + dz * dz;
                if (d <= bestDistance) { bestDistance = d; best = r; }
            }
            return best;
        }
    }
}
