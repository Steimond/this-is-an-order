using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // Enemy bot of the test ground, an easy StarCraft-like opponent. Once a second it:
    // - keeps its workers mining and trains more: humans up to a dozen, two on plasma once it has eight; machines one on
    //   each deposit round its base, one on plasma once it has five;
    // - adds a supply node before supply runs out and, after its first wave, a second factory once it has metal to spare;
    //   a worker builds them, by its faction's rules, and a human building left without its worker gets another one;
    // - keeps its factories training a mixed army, gathered at the top of its ramp;
    // - meets player units that come near its buildings with the army at home;
    // - from the fifth minute sends the army at the player's buildings each time it has grown to the next wave's size;
    //   a wave keeps going from building to building until it dies.
    public sealed partial class PrototypeMatch
    {
        public bool BotEnabled { get; private set; }
        public int Waves { get; private set; }
        const float BotThink = 1, FirstWave = 300, DefenseRadius = 12;
        const int BotWorkers = 12, FirstWaveSupply = 10, WaveGrowth = 4, MaxWaveSupply = 40;
        float botTimer;
        int waveSupply = FirstWaveSupply;
        bool waveNow;
        readonly HashSet<PrototypeUnit> raiders = new HashSet<PrototypeUnit>();
        // Where the bot's army waits: on its plateau at the top of its ramp.
        const float StageX = 46.5f, StageZ = 52.5f;

        public void StartBot()
        {
            BotEnabled = true;
            var hq = BotHeadquarters();
            // Starting workers are part of the bot's base, not a cheat.
            if (hq != null && !Units.Exists(u => u.Owner == Enemy && u.Kind == UnitKind.Worker)) SpawnAround(UnitKind.Worker, 4, hq.X + hq.Width * .5f, hq.Z - 1.5f, Enemy);
        }
        public bool ToggleBot() { if (BotEnabled) BotEnabled = false; else StartBot(); return Cheat(BotEnabled, "бот противника"); }
        // Cheat: the next think sends the army at the player whatever its size and the time.
        public void SendWaveNow() { Cheated = true; waveNow = true; if (!BotEnabled) StartBot(); Message = "Чит: противник атакует."; }

        void TickBot(float dt)
        {
            if (!BotEnabled || Winner >= 0 || (botTimer += dt) < BotThink) return;
            botTimer = 0;
            // The bot's orders must not take over the player's event line.
            var message = Message;
            BotEconomy();
            BotBuild();
            BotProduce();
            BotArmy();
            Message = message;
        }
        PrototypeBuilding BotHeadquarters() => Buildings.Find(b => b.Owner == Enemy && Trains(b, UnitKind.Worker));

        void BotEconomy()
        {
            var hq = BotHeadquarters();
            if (hq == null || hq.Ready < 1) return;
            var workers = Units.FindAll(u => u.Owner == Enemy && u.Kind == UnitKind.Worker);
            // Hauling workers share deposits: a dozen, two on plasma from eight. Nanite workers hold one deposit each: as
            // many as there are deposits round the base, one on plasma from five.
            bool nanites = Roster(Enemy).NaniteMining;
            int wanted = nanites ? DepositsNear(hq) : BotWorkers;
            int onPlasma = workers.FindAll(w => w.HarvestTarget != null && w.HarvestTarget.Plasma).Count, wantPlasma = nanites ? (workers.Count >= 5 ? 1 : 0) : workers.Count >= 8 ? 2 : 0;
            foreach (var w in workers)
            {
                if (w.HarvestTarget != null || w.Moving || Busy(w)) continue;
                bool plasma = onPlasma < wantPlasma;
                var deposit = BotDeposit(hq, plasma, workers) ?? BotDeposit(hq, !plasma, workers);
                if (deposit == null) continue;
                Move(w, deposit.X + .5f, deposit.Z + .5f);
                if (w.HarvestTarget != null && w.HarvestTarget.Plasma) onPlasma++;
            }
            // Busy miners are moved to plasma too, between trips.
            while (onPlasma < wantPlasma)
            {
                var plasma = BotDeposit(hq, true, workers);
                var miner = workers.Find(w => w.HarvestTarget != null && !w.HarvestTarget.Plasma && w.Carrying == 0);
                if (plasma == null || miner == null) break;
                Move(miner, plasma.X + .5f, plasma.Z + .5f);
                if (miner.HarvestTarget != plasma) break;
                onPlasma++;
            }
            int queued = 0; foreach (var k in hq.queue) if (k == UnitKind.Worker) queued++;
            if (workers.Count + queued < wanted && hq.queue.Count == 0) Train(hq, UnitKind.Worker);
            var rally = BotDeposit(hq, false, workers);
            if (rally != null && (!hq.HasRally || Map.ResourceAt((int)hq.RallyX, (int)hq.RallyZ) == null)) SetRally(hq, rally.X + .5f, rally.Z + .5f);
        }
        int DepositsNear(PrototypeBuilding hq)
        {
            int count = 0;
            foreach (var r in Map.Resources) if (r.Remaining > 0 && Reach(hq, r) <= 10) count++;
            return count;
        }
        static float Reach(PrototypeBuilding hq, ResourceDeposit r)
        {
            float cx = hq.X + hq.Width * .5f, cz = hq.Z + hq.Width * .5f;
            return (float)Math.Sqrt((r.X + .5f - cx) * (r.X + .5f - cx) + (r.Z + .5f - cz) * (r.Z + .5f - cz));
        }
        // The deposit of the kind nearest the command node among those with the fewest of the bot's workers on them; a
        // nanite worker only takes a deposit nobody holds.
        ResourceDeposit BotDeposit(PrototypeBuilding hq, bool plasma, List<PrototypeUnit> workers)
        {
            bool nanites = Roster(Enemy).NaniteMining;
            ResourceDeposit best = null; float bestScore = float.MaxValue;
            foreach (var r in Map.Resources)
            {
                if (r.Plasma != plasma || r.Remaining <= 0) continue;
                float d = Reach(hq, r);
                if (d > 10) continue;
                int on = 0; foreach (var w in workers) if (w.HarvestTarget == r) on++;
                if (nanites && (on > 0 || (miners.TryGetValue(r, out var holder) && Holds(holder, r)))) continue;
                float score = on * 100 + d;
                if (score < bestScore) { bestScore = score; best = r; }
            }
            return best;
        }

        void BotBuild()
        {
            var hq = BotHeadquarters();
            if (hq == null) return;
            // A building left without its worker gets another one.
            foreach (var b in Buildings)
            {
                if (b.Owner != Enemy || b.Ready >= 1 || b.SelfBuilt || b.Builder != null) continue;
                var worker = BotBuilder(b.X + b.Width * .5f, b.Z + b.Width * .5f);
                if (worker != null) Resume(worker, b);
                return;
            }
            bool raising = Buildings.Exists(b => b.Owner == Enemy && b.Ready < 1) || Units.Exists(u => u.Owner == Enemy && u.SiteKind >= 0);
            if (raising) return;
            if (Supply(Enemy) + 6 >= Limit(Enemy) && Limit(Enemy) < 200) { if (BotPlace(hq, 0)) return; }
            int factories = Buildings.FindAll(b => b.Owner == Enemy && Trains(b, UnitKind.Ranged)).Count;
            if (factories < 2 && Waves >= 1 && metalStock[Enemy] >= 400) BotPlace(hq, 1);
        }
        // A free spot near the command node that leaves a cell of room all round and keeps off the metal line.
        bool BotPlace(PrototypeBuilding hq, int kind)
        {
            var stats = Roster(Enemy).Buildings[kind];
            if (metalStock[Enemy] < stats.Metal || plasmaStock[Enemy] < stats.Plasma) return false;
            int w = stats.Width; float cx = hq.X + hq.Width * .5f, cz = hq.Z + hq.Width * .5f, level = Map.Height(hq.X, hq.Z);
            int bestX = 0, bestZ = 0; float best = float.MaxValue;
            for (int z = hq.Z - 10; z <= hq.Z + 10; z++)
            for (int x = hq.X - 10; x <= hq.X + 10; x++)
            {
                float d = (x + w * .5f - cx) * (x + w * .5f - cx) + (z + w * .5f - cz) * (z + w * .5f - cz);
                if (d >= best || d < 9 || !Room(x, z, w, level) || !CanBuild(x, z, kind, out _, Enemy)) continue;
                best = d; bestX = x; bestZ = z;
            }
            if (best == float.MaxValue) return false;
            var builder = BotBuilder(bestX + w * .5f, bestZ + w * .5f);
            return builder != null && Build(builder, bestX, bestZ, kind);
        }
        // The worker nearest the spot that is not building: a metal miner with empty hands if there is one.
        PrototypeUnit BotBuilder(float x, float z)
        {
            PrototypeUnit best = null; float bestScore = float.MaxValue;
            foreach (var u in Units)
            {
                if (u.Owner != Enemy || u.Kind != UnitKind.Worker || Busy(u)) continue;
                float score = (u.X - x) * (u.X - x) + (u.Z - z) * (u.Z - z);
                if (u.Carrying > 0 || (u.HarvestTarget != null && u.HarvestTarget.Plasma)) score += 10000;
                if (score < bestScore) { bestScore = score; best = u; }
            }
            return best;
        }
        bool Room(int x, int z, int w, float level)
        {
            for (int zz = z - 1; zz <= z + w; zz++)
            for (int xx = x - 1; xx <= x + w; xx++)
                if (!Map.Inside(xx, zz) || Occupied(xx, zz) || Map.Ramp(xx, zz) || Map.Height(xx, zz) != level) return false;
            foreach (var r in Map.Resources)
                if (r.Remaining > 0 && r.X >= x - 3 && r.X <= x + w + 2 && r.Z >= z - 3 && r.Z <= z + w + 2) return false;
            return true;
        }

        void BotProduce()
        {
            var army = Units.FindAll(u => u.Owner == Enemy && u.Stats.Damage > 0);
            foreach (var b in Buildings)
            {
                if (b.Owner != Enemy || b.Ready < 1 || !Trains(b, UnitKind.Ranged)) continue;
                if (!b.HasRally) SetRally(b, StageX, StageZ);
                if (b.queue.Count >= 1) continue;
                // Riflemen, assault units and gunships about three to two to one, as far as resources allow.
                UnitKind? pick = null; float least = float.MaxValue;
                foreach (var (kind, weight) in new[] { (UnitKind.Ranged, 3f), (UnitKind.Melee, 2f), (UnitKind.Air, 1f) })
                {
                    var stats = Roster(Enemy).Unit(kind);
                    if (!Trains(b, kind) || metalStock[Enemy] < stats.Metal || plasmaStock[Enemy] < stats.Plasma) continue;
                    int have = army.FindAll(u => u.Kind == kind).Count; foreach (var q in b.queue) if (q == kind) have++;
                    if (have / weight < least) { least = have / weight; pick = kind; }
                }
                if (pick.HasValue) Train(b, pick.Value);
            }
        }

        void BotArmy()
        {
            raiders.RemoveWhere(u => !u.Alive);
            var home = Units.FindAll(u => u.Owner == Enemy && u.Stats.Damage > 0 && !raiders.Contains(u));
            // Defence: the army at home goes for the player's units near the bot's buildings.
            PrototypeUnit threat = null; float threatDistance = float.MaxValue;
            foreach (var u in Units)
            {
                if (u.Owner != Player || !SeenBy(Enemy, u)) continue;
                foreach (var b in Buildings)
                {
                    if (b.Owner != Enemy) continue;
                    float dx = u.X - (b.X + b.Width * .5f), dz = u.Z - (b.Z + b.Width * .5f), d = dx * dx + dz * dz;
                    if (d < DefenseRadius * DefenseRadius && d < threatDistance) { threatDistance = d; threat = u; }
                }
            }
            var idle = home.FindAll(u => u.Order == UnitOrder.Idle && u.Target == null && !u.Moving);
            if (threat != null && idle.Count > 0) AttackMoveGroup(idle, threat.X, threat.Z);
            else if (threat == null)
            {
                // Back to the gathering point after a fight away from it.
                var strays = idle.FindAll(u => (u.X - StageX) * (u.X - StageX) + (u.Z - StageZ) * (u.Z - StageZ) > 64);
                if (strays.Count > 0) AttackMoveGroup(strays, StageX, StageZ);
            }
            // Attack waves.
            int supply = 0; foreach (var u in home) supply += u.Stats.Supply;
            if (home.Count > 0 && threat == null && (waveNow || (Clock >= FirstWave && supply >= waveSupply)))
            {
                var target = PlayerTarget(StageX, StageZ);
                if (target != null)
                {
                    AttackMoveGroup(home, target.X + target.Width * .5f, target.Z + target.Width * .5f);
                    foreach (var u in home) raiders.Add(u);
                    Waves++; waveSupply = Math.Min(MaxWaveSupply, waveSupply + WaveGrowth); waveNow = false;
                }
            }
            // A wave that has done its point goes on to the next of the player's buildings.
            foreach (var u in raiders)
            {
                if (u.Order != UnitOrder.Idle || u.Target != null || u.TargetBuilding != null || u.Moving) continue;
                var next = PlayerTarget(u.X, u.Z);
                if (next != null) AttackMove(u, next.X + next.Width * .5f, next.Z + next.Width * .5f);
            }
        }
        // The player's building nearest a point.
        PrototypeBuilding PlayerTarget(float x, float z)
        {
            PrototypeBuilding best = null; float bestScore = float.MaxValue;
            foreach (var b in Buildings)
            {
                if (b.Owner != Player) continue;
                float dx = b.X + b.Width * .5f - x, dz = b.Z + b.Width * .5f - z, score = dx * dx + dz * dz;
                if (score < bestScore) { bestScore = score; best = b; }
            }
            return best;
        }
    }
}
