using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // The bot, an easy StarCraft-like opponent; any side may have one (on the player's it plays for the player). Once a second it:
    // - keeps its workers mining and trains more: humans up to a dozen (ten more per extra base), two on each of its
    //   extractors once it has eight; machines one on each deposit round its bases, one on plasma once it has five;
    // - humans put an extractor on the rift by each base from eight workers, and mend damaged buildings once the
    //   enemy has left; machines link their two bases with a relay, and add one when the coverage has no room left;
    // - from the sixth minute takes a second base: the free base spot nearest its first one;
    // - adds a supply node before supply runs out and, after its first wave, a second factory once it has metal to spare
    //   and a research building, where it upgrades weapons and armour in turn;
    //   a worker builds them, by its faction's rules, and a human building left without its worker gets another one;
    // - keeps its factories training a mixed army, gathered at the top of its ramp;
    // - meets enemy units that come near its buildings with the army at home;
    // - from the fifth minute sends the army at the nearest enemy buildings each time it has grown to the next wave's
    //   size; a wave keeps going from building to building until it dies.
    public sealed partial class PrototypeMatch
    {
        // One bot's mind: its side, its clock and waves, and the army it has sent.
        sealed class BotMind
        {
            public readonly int Side;
            public Fix Timer, StageX, StageZ;
            public int Waves, WaveSupply = FirstWaveSupply;
            public bool WaveNow, Cramped;
            public readonly List<PrototypeUnit> Raiders = new List<PrototypeUnit>();
            public BotMind(int side) { Side = side; }
        }
        // The bots in side order; `bot` is the one thinking right now, for the Bot… methods below.
        readonly List<BotMind> bots = new List<BotMind>();
        BotMind bot;
        BotMind BotOf(int side) => bots.Find(b => b.Side == side);
        public bool HasBot(int side) => BotOf(side) != null;
        // The test ground's opponent.
        public bool BotEnabled => HasBot(Enemy);
        public int Waves => WavesOf(Enemy);
        public int WavesOf(int side) => BotOf(side)?.Waves ?? 0;
        static readonly Fix BotThink = 1, FirstWave = 300, DefenseRadius = 12, ExpandAt = 360;
        const int BotWorkers = 12, FirstWaveSupply = 10, WaveGrowth = 4, MaxWaveSupply = 40;

        public void StartBot(int side = Enemy)
        {
            if (side < 0 || side >= Sides || HasBot(side)) return;
            var mind = new BotMind(side); bots.Add(mind); bots.Sort((a, b) => a.Side.CompareTo(b.Side));
            var previous = bot; bot = mind;
            var hq = BotHeadquarters();
            // Its army waits on its plateau at the top of its ramp; the map's corners mirror each other.
            Fix cx = hq != null ? hq.X + hq.Width * (Fix).5f : 48, cz = hq != null ? hq.Z + hq.Width * (Fix).5f : 48;
            mind.StageX = cx < 32 ? 64 - (Fix)46.5f : (Fix)46.5f; mind.StageZ = cz < 32 ? 64 - (Fix)52.5f : (Fix)52.5f;
            // Starting workers are part of the bot's base, not a cheat.
            if (hq != null && !Units.Exists(u => u.Owner == side && u.Kind == UnitKind.Worker))
                SpawnAround(UnitKind.Worker, 4, hq.X + hq.Width * (Fix).5f, hq.Z < 32 ? hq.Z + hq.Width + (Fix)1.5f : hq.Z - (Fix)1.5f, side);
            bot = previous;
        }
        public bool ToggleBot() { if (BotEnabled) bots.RemoveAll(b => b.Side == Enemy); else StartBot(); return Cheat(BotEnabled, "бот противника"); }
        // Cheat: the next think sends the army at the player whatever its size and the time.
        public void SendWaveNow() { Cheated = true; if (!BotEnabled) StartBot(); BotOf(Enemy).WaveNow = true; Message = "Чит: противник атакует."; }

        void TickBot(Fix dt)
        {
            if (Winner >= 0) return;
            // The bots' orders must not take over the player's event line.
            var message = Message;
            foreach (var mind in bots)
            {
                if (defeated[mind.Side] || (mind.Timer += dt) < BotThink) continue;
                mind.Timer = 0; bot = mind;
                BotEconomy();
                BotBuild();
                BotProduce();
                BotArmy();
            }
            bot = null; Message = message;
        }
        PrototypeBuilding BotHeadquarters() => Buildings.Find(b => b.Owner == bot.Side && Trains(b, UnitKind.Worker));
        static readonly (UnitKind, Fix)[] HumanMix = { (UnitKind.Ranged, (Fix)3f), (UnitKind.Melee, (Fix)2f), (UnitKind.Tank, (Fix)1.5f), (UnitKind.Artillery, (Fix)1f), (UnitKind.Air, (Fix)1f), (UnitKind.Fighter, (Fix)1f) },
            MachineMix = { (UnitKind.Melee, (Fix)3f), (UnitKind.Leaper, (Fix)2f), (UnitKind.Ranged, (Fix)2f), (UnitKind.Air, (Fix)1f) };

        void BotEconomy()
        {
            var bases = Buildings.FindAll(b => b.Owner == bot.Side && b.Kind == FactionBalance.Main && b.Ready >= 1);
            if (bases.Count == 0) return;
            var workers = Units.FindAll(u => u.Owner == bot.Side && u.Kind == UnitKind.Worker);
            // Hauling workers share deposits: a dozen at the first base and ten more at each other, two on plasma from
            // eight. Nanite workers hold one deposit each: as many as there are deposits round the bases, one on plasma
            // from five.
            bool nanites = Roster(bot.Side).NaniteMining;
            int wanted = nanites ? DepositsNear(bases) : BotWorkers + 10 * (bases.Count - 1);
            // Humans put two on each finished extractor by their bases.
            int extractors = Buildings.FindAll(b => b.Owner == bot.Side && b.Stats.Extracts && b.Ready >= 1).Count;
            int onPlasma = workers.FindAll(w => w.HarvestTarget != null && w.HarvestTarget.Plasma).Count, wantPlasma = nanites ? (workers.Count >= 5 ? bases.Count : 0) : workers.Count >= 8 ? 2 * Math.Min(extractors, bases.Count) : 0;
            foreach (var w in workers)
            {
                if (w.HarvestTarget != null || w.Moving || Busy(w)) continue;
                bool plasma = onPlasma < wantPlasma;
                var deposit = BotDeposit(bases, plasma, workers) ?? BotDeposit(bases, !plasma, workers);
                if (deposit == null) continue;
                Move(w, deposit.X + (Fix).5f, deposit.Z + (Fix).5f);
                if (w.HarvestTarget != null && w.HarvestTarget.Plasma) onPlasma++;
            }
            // Busy miners are moved to plasma too, between trips.
            while (onPlasma < wantPlasma)
            {
                var plasma = BotDeposit(bases, true, workers);
                var miner = workers.Find(w => w.HarvestTarget != null && !w.HarvestTarget.Plasma && w.Carrying == 0);
                if (plasma == null || miner == null) break;
                Move(miner, plasma.X + (Fix).5f, plasma.Z + (Fix).5f);
                if (miner.HarvestTarget != plasma) break;
                onPlasma++;
            }
            int queued = 0; foreach (var b in bases) foreach (var k in b.queue) if (k == UnitKind.Worker) queued++;
            // New workers from the least busy base, rallied to a deposit next to it.
            var idle = bases.Find(b => b.queue.Count == 0);
            if (idle != null && workers.Count + queued < wanted) Train(idle, UnitKind.Worker);
            foreach (var b in bases)
            {
                var rally = BotDeposit(new List<PrototypeBuilding> { b }, false, workers);
                if (rally != null && (!b.HasRally || Map.ResourceAt((int)b.RallyX, (int)b.RallyZ) == null)) SetRally(b, rally.X + (Fix).5f, rally.Z + (Fix).5f);
            }
        }
        int DepositsNear(List<PrototypeBuilding> bases)
        {
            int count = 0;
            foreach (var r in Map.Resources) if (r.Remaining > 0 && Reach(bases, r) <= 10) count++;
            return count;
        }
        // From the deposit to the nearest of the bases.
        static Fix Reach(List<PrototypeBuilding> bases, ResourceDeposit r)
        {
            Fix best = Fix.MaxValue;
            foreach (var hq in bases)
            {
                Fix cx = hq.X + hq.Width * (Fix).5f, cz = hq.Z + hq.Width * (Fix).5f;
                best = Math.Min(best, Math.Sqrt((r.X + (Fix).5f - cx) * (r.X + (Fix).5f - cx) + (r.Z + (Fix).5f - cz) * (r.Z + (Fix).5f - cz)));
            }
            return best;
        }
        // The deposit of the kind nearest a base among those with the fewest of the bot's workers on them; a nanite
        // worker only takes a deposit nobody holds.
        ResourceDeposit BotDeposit(List<PrototypeBuilding> bases, bool plasma, List<PrototypeUnit> workers)
        {
            bool nanites = Roster(bot.Side).NaniteMining;
            ResourceDeposit best = null; Fix bestScore = Fix.MaxValue;
            foreach (var r in Map.Resources)
            {
                if (r.Plasma != plasma || r.Remaining <= 0 || MiningBlocked(bot.Side, r) != null) continue;
                Fix d = Reach(bases, r);
                if (d > 10) continue;
                int on = 0; foreach (var w in workers) if (w.HarvestTarget == r) on++;
                if (nanites && (on > 0 || (miners.TryGetValue(r, out var holder) && Holds(holder, r)))) continue;
                Fix score = on * 100 + d;
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
                if (b.Owner != bot.Side || b.Ready >= 1 || b.SelfBuilt || b.Builder != null) continue;
                var worker = BotBuilder(b.X + b.Width * (Fix).5f, b.Z + b.Width * (Fix).5f);
                if (worker != null) Resume(worker, b);
                return;
            }
            // Humans mend a damaged building once no enemy is near it, one worker to each.
            if (Roster(bot.Side).Repairs)
                foreach (var b in Buildings)
                {
                    if (b.Owner != bot.Side || b.Ready < 1 || b.Health >= b.Stats.Health || Units.Exists(u => u.Repairing == b)) continue;
                    if (Units.Exists(u => u.Owner != bot.Side && u.Alive && Gap(u, b) < DefenseRadius)) continue;
                    var worker = BotBuilder(b.X + b.Width * (Fix).5f, b.Z + b.Width * (Fix).5f);
                    if (worker != null) Repair(worker, b);
                    break;
                }
            // One building at a time, apart from a new base, which takes long: supply goes on meanwhile.
            bool raising = Buildings.Exists(b => b.Owner == bot.Side && b.Ready < 1 && b.Kind != FactionBalance.Main)
                || Units.Exists(u => u.Owner == bot.Side && u.SiteKind >= 0 && u.SiteKind != FactionBalance.Main);
            if (raising) return;
            if (Supply(bot.Side) + 6 >= Limit(bot.Side) && Limit(bot.Side) < supplyCap[bot.Side]) { if (BotPlace(hq, 0)) return; }
            // A side that starts with only its main building raises its first factory before anything else.
            int factories = Buildings.FindAll(b => b.Owner == bot.Side && Trains(b, UnitKind.Ranged)).Count;
            if (factories == 0) { BotPlace(hq, 1); return; }
            if (BotExtractor()) return;
            if (BotRelay(hq)) return;
            if (BotExpand(hq)) return;
            if (factories < 2 && bot.Waves >= 1 && metalStock[bot.Side] >= 400) { if (BotPlace(hq, 1)) return; }
            // After its first wave it builds a research building and upgrades weapons and armour in turn.
            int research = Roster(bot.Side).Research;
            if (research >= 0 && bot.Waves >= 1 && !Buildings.Exists(b => b.Owner == bot.Side && b.Kind == research) && metalStock[bot.Side] >= 300) BotPlace(hq, research);
            foreach (var b in Buildings)
                if (b.Owner == bot.Side && b.Kind == research && !b.research.HasValue)
                {
                    var next = Level(bot.Side, Upgrade.Weapons) <= Level(bot.Side, Upgrade.Armor) ? Upgrade.Weapons : Upgrade.Armor;
                    if (ResearchBlocked(b, next) != null) next = next == Upgrade.Weapons ? Upgrade.Armor : Upgrade.Weapons;
                    if (ResearchBlocked(b, next) == null) Research(b, next);
                }
        }
        // From the sixth minute the bot takes a second base: the free base spot nearest its first one. Until it has the
        // metal, it saves for it and trains no army.
        bool ExpansionDue() => Clock >= ExpandAt && Buildings.FindAll(b => b.Owner == bot.Side && b.Kind == FactionBalance.Main).Count == 1
            && !Units.Exists(u => u.Owner == bot.Side && u.SiteKind == FactionBalance.Main);
        bool BotExpand(PrototypeBuilding hq)
        {
            if (!ExpansionDue() || metalStock[bot.Side] < Roster(bot.Side).Buildings[FactionBalance.Main].Metal) return false;
            Fix cx = hq.X + hq.Width * (Fix).5f, cz = hq.Z + hq.Width * (Fix).5f, best = Fix.MaxValue; int bx = 0, bz = 0;
            foreach (var (x, z) in Map.Bases)
            {
                Fix d = (x + (Fix)1.5f - cx) * (x + (Fix)1.5f - cx) + (z + (Fix)1.5f - cz) * (z + (Fix)1.5f - cz);
                if (d < 1 || d >= best || !CanBuild(x, z, FactionBalance.Main, out _, bot.Side)) continue;
                best = d; bx = x; bz = z;
            }
            if (best == Fix.MaxValue) return false;
            var builder = BotBuilder(bx + (Fix)1.5f, bz + (Fix)1.5f);
            return builder != null && Build(builder, bx, bz, FactionBalance.Main);
        }
        // A free spot near the command node that leaves a cell of room all round and keeps off the metal line.
        bool BotPlace(PrototypeBuilding hq, int kind)
        {
            var stats = Roster(bot.Side).Buildings[kind];
            if (metalStock[bot.Side] < stats.Metal || plasmaStock[bot.Side] < stats.Plasma) return false;
            int w = stats.Width; Fix cx = hq.X + hq.Width * (Fix).5f, cz = hq.Z + hq.Width * (Fix).5f, level = Map.Height(hq.X, hq.Z);
            int bestX = 0, bestZ = 0; Fix best = Fix.MaxValue;
            for (int z = hq.Z - 10; z <= hq.Z + 10; z++)
            for (int x = hq.X - 10; x <= hq.X + 10; x++)
            {
                Fix d = (x + w * (Fix).5f - cx) * (x + w * (Fix).5f - cx) + (z + w * (Fix).5f - cz) * (z + w * (Fix).5f - cz);
                if (d >= best || d < 9 || !Room(x, z, w, level) || !CanBuild(x, z, kind, out _, bot.Side)) continue;
                best = d; bestX = x; bestZ = z;
            }
            // No room inside the coverage: a machine bot extends it with a relay next.
            if (best == Fix.MaxValue) { bot.Cramped = Roster(bot.Side).Network; return false; }
            var builder = BotBuilder(bestX + w * (Fix).5f, bestZ + w * (Fix).5f);
            return builder != null && Build(builder, bestX, bestZ, kind);
        }
        // Machines tie their bases into one network: a relay between two cores that do not reach each other, so each base
        // has a second path to a core (SETTING.md). With no room left for a building inside the coverage, a relay at its edge
        // toward the middle of the map makes more.
        bool BotRelay(PrototypeBuilding hq)
        {
            if (!Roster(bot.Side).Network || RelaysLeft(bot.Side) <= 0 || metalStock[bot.Side] < Roster(bot.Side).Buildings[FactionBalance.Relay].Metal) return false;
            var cores = Buildings.FindAll(b => b.Owner == bot.Side && b.Kind == FactionBalance.Main && b.Ready >= 1 && b.Linked);
            for (int i = 0; i < cores.Count; i++)
            for (int j = i + 1; j < cores.Count; j++)
            {
                PrototypeBuilding a = cores[i], c = cores[j];
                if (Distance(a, c) <= a.Stats.Coverage || Distance(a, c) > a.Stats.Coverage + c.Stats.Coverage) continue;
                if (Buildings.Exists(r => r.Owner == bot.Side && r.Kind == FactionBalance.Relay && Distance(r, a) <= a.Stats.Coverage && Distance(r, c) <= c.Stats.Coverage)) continue;
                Fix mx = (a.X + c.X) * (Fix).5f + (Fix)1.5f, mz = (a.Z + c.Z) * (Fix).5f + (Fix)1.5f;
                if (BotRelayAt(mx, mz, r => Near(r, a) && Near(r, c))) return true;
            }
            if (!bot.Cramped) return false;
            bot.Cramped = false;
            Fix cx = hq.X + hq.Width * (Fix).5f, cz = hq.Z + hq.Width * (Fix).5f, dx = 32 - cx, dz = 32 - cz, d = Math.Sqrt(dx * dx + dz * dz), reach = hq.Stats.Coverage - (Fix)1.5f;
            return d > 0 && BotRelayAt(cx + dx / d * reach, cz + dz / d * reach, r => Near(r, hq));
            bool Near(PrototypeBuilding relay, PrototypeBuilding core) => Distance(relay, core) <= core.Stats.Coverage;
        }
        // A relay on a free spot near the point that passes the check: a stand-in building tells where it would stand.
        bool BotRelayAt(Fix x, Fix z, Func<PrototypeBuilding, bool> fits)
        {
            var stats = Roster(bot.Side).Buildings[FactionBalance.Relay];
            int bestX = 0, bestZ = 0; Fix best = Fix.MaxValue;
            for (int zz = (int)z - 3; zz <= (int)z + 3; zz++)
            for (int xx = (int)x - 3; xx <= (int)x + 3; xx++)
            {
                Fix d = (xx + (Fix).5f - x) * (xx + (Fix).5f - x) + (zz + (Fix).5f - z) * (zz + (Fix).5f - z);
                if (d >= best || !Map.Inside(xx, zz) || !CanBuild(xx, zz, FactionBalance.Relay, out _, bot.Side)) continue;
                if (!fits(new PrototypeBuilding { X = xx, Z = zz, Width = 1, Stats = stats })) continue;
                best = d; bestX = xx; bestZ = zz;
            }
            if (best == Fix.MaxValue) return false;
            var builder = BotBuilder(bestX + (Fix).5f, bestZ + (Fix).5f);
            return builder != null && Build(builder, bestX, bestZ, FactionBalance.Relay);
        }
        // A side that mines plasma through extractors puts one on the rift by each finished base once it has eight workers.
        bool BotExtractor()
        {
            int kind = Roster(bot.Side).Extractor;
            if (kind < 0 || Units.FindAll(u => u.Owner == bot.Side && u.Kind == UnitKind.Worker).Count < 8) return false;
            var bases = Buildings.FindAll(b => b.Owner == bot.Side && b.Kind == FactionBalance.Main && b.Ready >= 1);
            foreach (var r in Map.Resources)
            {
                if (!r.Plasma || r.Remaining <= 0 || Reach(bases, r) > 10 || ExtractorOn(r) != null || !CanBuild(r.X, r.Z, kind, out _, bot.Side)) continue;
                var builder = BotBuilder(r.X + (Fix).5f, r.Z + (Fix).5f);
                return builder != null && Build(builder, r.X, r.Z, kind);
            }
            return false;
        }
        // The worker nearest the spot that is not building: a metal miner with empty hands if there is one.
        PrototypeUnit BotBuilder(Fix x, Fix z)
        {
            PrototypeUnit best = null; Fix bestScore = Fix.MaxValue;
            foreach (var u in Units)
            {
                if (u.Owner != bot.Side || u.Kind != UnitKind.Worker || Busy(u)) continue;
                Fix score = (u.X - x) * (u.X - x) + (u.Z - z) * (u.Z - z);
                if (u.Carrying > 0 || (u.HarvestTarget != null && u.HarvestTarget.Plasma)) score += 10000;
                if (score < bestScore) { bestScore = score; best = u; }
            }
            return best;
        }
        bool Room(int x, int z, int w, Fix level)
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
            if (ExpansionDue() && metalStock[bot.Side] < Roster(bot.Side).Buildings[FactionBalance.Main].Metal) return;
            var army = Units.FindAll(u => u.Owner == bot.Side && u.Stats.Damage > 0);
            foreach (var b in Buildings)
            {
                if (b.Owner != bot.Side || b.Ready < 1 || !Trains(b, UnitKind.Ranged)) continue;
                if (!b.HasRally) SetRally(b, bot.StageX, bot.StageZ);
                if (b.queue.Count >= 1) continue;
                // Humans: riflemen, assault units, tanks, howitzers and gunships about three to two to one and a half to one to
                // one, interceptors only against a player who has air units; machines: spiders, leapers, emitters and gliders
                // about three to two to two to one; as far as resources allow.
                bool air = Units.Exists(u => u.Owner != bot.Side && u.Stats.Flying);
                UnitKind? pick = null; Fix least = Fix.MaxValue;
                foreach (var (kind, weight) in Roster(bot.Side).Has(UnitKind.Leaper) ? MachineMix : HumanMix)
                {
                    var stats = Roster(bot.Side).Unit(kind);
                    if (stats == null || (!stats.HitsGround && !air)) continue;
                    if (!Trains(b, kind) || metalStock[bot.Side] < stats.Metal || plasmaStock[bot.Side] < stats.Plasma) continue;
                    int have = army.FindAll(u => u.Kind == kind).Count; foreach (var q in b.queue) if (q == kind) have++;
                    if (have / weight < least) { least = have / weight; pick = kind; }
                }
                if (pick.HasValue) Train(b, pick.Value);
            }
        }

        void BotArmy()
        {
            bot.Raiders.RemoveAll(u => !u.Alive);
            var home = Units.FindAll(u => u.Owner == bot.Side && u.Stats.Damage > 0 && !bot.Raiders.Contains(u));
            // Defence: the army at home goes for enemy units near the bot's buildings.
            PrototypeUnit threat = null; Fix threatDistance = Fix.MaxValue;
            foreach (var u in Units)
            {
                if (u.Owner == bot.Side || !SeenBy(bot.Side, u)) continue;
                foreach (var b in Buildings)
                {
                    if (b.Owner != bot.Side) continue;
                    Fix dx = u.X - (b.X + b.Width * (Fix).5f), dz = u.Z - (b.Z + b.Width * (Fix).5f), d = dx * dx + dz * dz;
                    if (d < DefenseRadius * DefenseRadius && d < threatDistance) { threatDistance = d; threat = u; }
                }
            }
            var idle = home.FindAll(u => u.Order == UnitOrder.Idle && u.Target == null && !u.Moving);
            if (threat != null && idle.Count > 0) AttackMoveGroup(idle, threat.X, threat.Z);
            else if (threat == null)
            {
                // Back to the gathering point after a fight away from it.
                var strays = idle.FindAll(u => (u.X - bot.StageX) * (u.X - bot.StageX) + (u.Z - bot.StageZ) * (u.Z - bot.StageZ) > 64);
                if (strays.Count > 0) AttackMoveGroup(strays, bot.StageX, bot.StageZ);
            }
            // Attack waves.
            int supply = 0; foreach (var u in home) supply += u.Stats.Supply;
            if (home.Count > 0 && threat == null && (bot.WaveNow || (Clock >= FirstWave && supply >= bot.WaveSupply)))
            {
                var target = EnemyTarget(bot.StageX, bot.StageZ);
                if (target != null)
                {
                    AttackMoveGroup(home, target.X + target.Width * (Fix).5f, target.Z + target.Width * (Fix).5f);
                    foreach (var u in home) if (!bot.Raiders.Contains(u)) bot.Raiders.Add(u);
                    bot.Waves++; bot.WaveSupply = Math.Min(MaxWaveSupply, bot.WaveSupply + WaveGrowth); bot.WaveNow = false;
                }
            }
            // A wave that has done its point goes on to the next enemy building.
            foreach (var u in bot.Raiders)
            {
                if (u.Order != UnitOrder.Idle || u.Target != null || u.TargetBuilding != null || u.Moving) continue;
                var next = EnemyTarget(u.X, u.Z);
                if (next != null) AttackMove(u, next.X + next.Width * (Fix).5f, next.Z + next.Width * (Fix).5f);
            }
        }
        // The enemy building nearest a point.
        PrototypeBuilding EnemyTarget(Fix x, Fix z)
        {
            PrototypeBuilding best = null; Fix bestScore = Fix.MaxValue;
            foreach (var b in Buildings)
            {
                if (b.Owner == bot.Side) continue;
                Fix dx = b.X + b.Width * (Fix).5f - x, dz = b.Z + b.Width * (Fix).5f - z, score = dx * dx + dz * dz;
                if (score < bestScore) { bestScore = score; best = b; }
            }
            return best;
        }
    }
}
