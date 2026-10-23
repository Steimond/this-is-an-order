using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // Enemy bot of the test ground, an easy StarCraft-like opponent. Once a second it:
    // - keeps its workers mining (two on gas once it has eight) and trains more up to a dozen;
    // - adds a supply node before supply runs out and, after its first wave, a second factory once it has minerals to spare;
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
            if (hq != null && !Units.Exists(u => u.Owner == Enemy && u.Kind == UnitKind.Worker))
            {
                var message = Message;
                SpawnSquad(UnitKind.Worker, 4, hq.X + hq.Width * .5f, hq.Z - 1.5f, Enemy);
                Message = message;
            }
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
            int onGas = workers.FindAll(w => w.HarvestTarget != null && w.HarvestTarget.Gas).Count, wantGas = workers.Count >= 8 ? 2 : 0;
            foreach (var w in workers)
            {
                if (w.HarvestTarget != null || w.Moving) continue;
                bool gas = onGas < wantGas;
                var deposit = BotDeposit(hq, gas, workers) ?? BotDeposit(hq, !gas, workers);
                if (deposit == null) continue;
                Move(w, deposit.X + .5f, deposit.Z + .5f);
                if (w.HarvestTarget != null && w.HarvestTarget.Gas) onGas++;
            }
            // Busy miners are moved to gas too, between trips.
            while (onGas < wantGas)
            {
                var gas = BotDeposit(hq, true, workers);
                var miner = workers.Find(w => w.HarvestTarget != null && !w.HarvestTarget.Gas && w.Carrying == 0);
                if (gas == null || miner == null) break;
                Move(miner, gas.X + .5f, gas.Z + .5f);
                if (miner.HarvestTarget != gas) break;
                onGas++;
            }
            int queued = 0; foreach (var k in hq.queue) if (k == UnitKind.Worker) queued++;
            if (workers.Count + queued < BotWorkers && hq.queue.Count == 0) Train(hq, UnitKind.Worker);
            var rally = BotDeposit(hq, false, workers);
            if (rally != null && (!hq.HasRally || Map.ResourceAt((int)hq.RallyX, (int)hq.RallyZ) == null)) SetRally(hq, rally.X + .5f, rally.Z + .5f);
        }
        // The deposit of the kind nearest the command node among those with the fewest of the bot's workers on them.
        ResourceDeposit BotDeposit(PrototypeBuilding hq, bool gas, List<PrototypeUnit> workers)
        {
            float cx = hq.X + hq.Width * .5f, cz = hq.Z + hq.Width * .5f;
            ResourceDeposit best = null; float bestScore = float.MaxValue;
            foreach (var r in Map.Resources)
            {
                if (r.Gas != gas || r.Remaining <= 0) continue;
                float d = (float)Math.Sqrt((r.X + .5f - cx) * (r.X + .5f - cx) + (r.Z + .5f - cz) * (r.Z + .5f - cz));
                if (d > 10) continue;
                int on = 0; foreach (var w in workers) if (w.HarvestTarget == r) on++;
                float score = on * 100 + d;
                if (score < bestScore) { bestScore = score; best = r; }
            }
            return best;
        }

        void BotBuild()
        {
            var hq = BotHeadquarters();
            if (hq == null) return;
            bool raising = Buildings.Exists(b => b.Owner == Enemy && b.Ready < 1);
            if (raising) return;
            if (Supply(Enemy) + 6 >= Limit(Enemy) && Limit(Enemy) < 200) { if (BotPlace(hq, 0)) return; }
            int factories = Buildings.FindAll(b => b.Owner == Enemy && Trains(b, UnitKind.Ranged)).Count;
            if (factories < 2 && Waves >= 1 && mineralStock[Enemy] >= 400) BotPlace(hq, 1);
        }
        // A free spot near the command node that leaves a cell of room all round and keeps off the mineral line.
        bool BotPlace(PrototypeBuilding hq, int kind)
        {
            var stats = Balance.Buildings[kind];
            if (mineralStock[Enemy] < stats.Minerals || gasStock[Enemy] < stats.Gas) return false;
            int w = stats.Width; float cx = hq.X + hq.Width * .5f, cz = hq.Z + hq.Width * .5f, level = Map.Height(hq.X, hq.Z);
            int bestX = 0, bestZ = 0; float best = float.MaxValue;
            for (int z = hq.Z - 10; z <= hq.Z + 10; z++)
            for (int x = hq.X - 10; x <= hq.X + 10; x++)
            {
                float d = (x + w * .5f - cx) * (x + w * .5f - cx) + (z + w * .5f - cz) * (z + w * .5f - cz);
                if (d >= best || d < 9 || !Room(x, z, w, level) || !CanBuild(x, z, kind, out _, Enemy)) continue;
                best = d; bestX = x; bestZ = z;
            }
            return best < float.MaxValue && Build(bestX, bestZ, kind, Enemy);
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
                    var stats = Balance.Unit(kind);
                    if (!Trains(b, kind) || mineralStock[Enemy] < stats.Minerals || gasStock[Enemy] < stats.Gas) continue;
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
