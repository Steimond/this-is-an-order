using System;
using System.Collections.Generic;

namespace Order.Simulation
{
    // Combat in the spirit of StarCraft:
    // - a unit strikes once per cooldown while the gap between its edge and the target's edge is within its range;
    // - idle and attack-moving units pick a visible enemy within acquire range, enemies that can strike back first, then
    //   the nearest, and keep their target while it stays close; enemy buildings come only when no enemy unit is near;
    // - a unit that is hit answers its attacker, idle allies nearby join in, also when one of their buildings is hit;
    // - a plain move ignores enemies; holding position never leaves the spot and shoots only what is in range;
    // - a destroyed building frees its cells; a side that loses all its buildings loses the match.
    public sealed partial class PrototypeMatch
    {
        // Every strike (attacker, target), every death and destroyed building, for effects.
        public event Action<PrototypeUnit, PrototypeUnit> Attacked;
        public event Action<PrototypeUnit, PrototypeBuilding> AttackedBuilding;
        public event Action<PrototypeUnit> Died;
        public event Action<PrototypeBuilding> Destroyed;
        // The player's units or buildings came under enemy fire at (x, z); `building` tells which. At most once in
        // AlertInterval seconds for each.
        public event Action<float, float, bool> UnderAttack;
        const float AlertInterval = 10;
        float lastBaseAlert = -AlertInterval, lastArmyAlert = -AlertInterval;
        // -1 while the match goes on, then the side that won.
        public int Winner { get; private set; } = -1;
        // Acquire range is the attack range plus this much; a current target is kept this much farther still.
        const float AcquireExtra = 2.5f, ChaseInterval = .4f, HelpRadius = 6, AggressorReach = 6;

        public bool CanAttack(PrototypeUnit attacker, PrototypeUnit target) =>
            target != null && attacker != target && attacker.Alive && target.Alive && attacker.Owner != target.Owner && attacker.Stats.Damage > 0 &&
            (target.Stats.Flying ? attacker.Stats.HitsAir : attacker.Stats.HitsGround);
        public bool CanAttack(PrototypeUnit attacker, PrototypeBuilding target) =>
            target != null && attacker.Alive && target.Health > 0 && attacker.Owner != target.Owner && attacker.Stats.Damage > 0 && attacker.Stats.HitsGround;
        public static float Gap(PrototypeUnit a, PrototypeUnit b) =>
            (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z)) - a.Stats.Radius - b.Stats.Radius;
        // From the unit's edge to the nearest point of the footprint.
        public static float Gap(PrototypeUnit a, PrototypeBuilding b)
        {
            float x = Math.Max(b.X, Math.Min(a.X, b.X + b.Width)), z = Math.Max(b.Z, Math.Min(a.Z, b.Z + b.Width));
            return (float)Math.Sqrt((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z)) - a.Stats.Radius;
        }
        public static bool InRange(PrototypeUnit attacker, PrototypeUnit target) => Gap(attacker, target) <= attacker.Stats.AttackRange;
        static float AcquireRange(PrototypeUnit u) => Math.Max(u.Stats.AttackRange + AcquireExtra, 4);
        static float Reaction(PrototypeUnit u) => u.Id * .618034f % 1 * .25f;
        bool Valid(PrototypeUnit u, PrototypeUnit target) => CanAttack(u, target) && SeenBy(u.Owner, target);
        bool Valid(PrototypeUnit u, PrototypeBuilding target) => CanAttack(u, target) && SeenBy(u.Owner, target);
        // A building is seen when any of its cells is.
        public bool SeenBy(int side, PrototypeBuilding b)
        {
            if (b.Owner == side) return true;
            var grid = side == Player ? Visible : enemyVisible;
            for (int z = b.Z; z < b.Z + b.Width; z++) for (int x = b.X; x < b.X + b.Width; x++) if (Map.Inside(x, z) && grid[x, z]) return true;
            return false;
        }

        // ---------- Orders ----------
        // Pursue one visible enemy until it dies; a unit that cannot hit it walks up to it instead.
        public bool Attack(PrototypeUnit unit, PrototypeUnit target)
        {
            if (target == null || !target.Alive || target.Owner == unit.Owner || !SeenBy(unit.Owner, target)) return false;
            if (!CanAttack(unit, target)) return Move(unit, target.X, target.Z);
            GiveOrder(unit, UnitOrder.Attack); unit.ordered = target; unit.Target = target;
            CloseIn(unit, target);
            Message = "Приказ: атаковать цель.";
            return true;
        }
        public bool Attack(PrototypeUnit unit, PrototypeBuilding target)
        {
            if (target == null || target.Health <= 0 || target.Owner == unit.Owner || !SeenBy(unit.Owner, target)) return false;
            if (!CanAttack(unit, target)) { var (x, z) = Approach(unit, target); return Move(unit, x, z); }
            GiveOrder(unit, UnitOrder.Attack); unit.orderedBuilding = target; unit.TargetBuilding = target;
            CloseIn(unit, target);
            Message = "Приказ: атаковать здание.";
            return true;
        }
        public bool AttackGroup(IReadOnlyList<PrototypeUnit> units, PrototypeUnit target)
        {
            bool any = false, attacking = false;
            foreach (var u in units) { bool can = CanAttack(u, target); if (Attack(u, target)) { any = true; attacking |= can; } }
            if (any) Message = attacking ? "Приказ: атаковать цель." : "Эти юниты не бьют такую цель: подходят к ней.";
            return any;
        }
        public bool AttackGroup(IReadOnlyList<PrototypeUnit> units, PrototypeBuilding target)
        {
            bool any = false, attacking = false;
            foreach (var u in units) { bool can = CanAttack(u, target); if (Attack(u, target)) { any = true; attacking |= can; } }
            if (any) Message = attacking ? "Приказ: атаковать здание." : "Эти юниты не бьют здания: подходят к нему.";
            return any;
        }
        // Go to the point and fight every enemy met on the way; a unit without a weapon simply moves.
        public bool AttackMove(PrototypeUnit unit, float x, float z)
        {
            if (!InMap(x, z)) return false;
            if (unit.Stats.Damage <= 0) return Move(unit, x, z);
            GiveOrder(unit, UnitOrder.AttackMove); unit.attackX = x; unit.attackZ = z;
            if (!PlanPath(unit, x, z)) return false;
            if (Message != Unreachable) Message = "Приказ: атака с движением.";
            return true;
        }
        public bool AttackMoveGroup(IReadOnlyList<PrototypeUnit> units, float x, float z) => GroupOrder(units, x, z, true, false);
        public void Hold(PrototypeUnit unit)
        {
            GiveOrder(unit, UnitOrder.Hold);
            unit.route.Clear(); unit.HarvestTarget = null; unit.hasGoal = false; unit.arrived = false;
            Message = "Приказ: держать позицию.";
        }
        void GiveOrder(PrototypeUnit unit, UnitOrder order)
        {
            unit.Order = order; unit.ordered = null; unit.orderedBuilding = null; unit.Target = null; unit.TargetBuilding = null;
            unit.aggressor = null; unit.engaged = false; unit.returning = false; unit.returns = 0; unit.delivering = false; unit.queued.Clear();
            // Any new order takes a worker off its construction: the building waits, a planned site is dropped.
            unit.Constructing = null; unit.SiteKind = -1;
        }

        // ---------- Steps ----------
        // Runs before movement every step: pick a target, then shoot it from where the unit stands or close in on it.
        void Fight(PrototypeUnit u, float dt)
        {
            if (u.cooldown > 0) u.cooldown -= dt;
            if (u.leaping) return;
            if (!u.Alive || u.Stats.Damage <= 0 || u.Order == UnitOrder.Move || u.HarvestTarget != null) { u.Target = null; u.TargetBuilding = null; return; }
            var target = PickTarget(u);
            var building = target == null ? PickBuilding(u) : null;
            // A new target takes a moment to aim at, different for every unit, so a squad does not fire in one volley.
            bool fresh = (target != null && target != u.Target) || (building != null && building != u.TargetBuilding);
            if (fresh && u.cooldown <= 0) u.cooldown = Reaction(u);
            u.Target = target; u.TargetBuilding = building;
            if (target == null && building == null) { if (u.engaged) Disengage(u); return; }
            if (u.Stats.Leap && target != null) { FightByLeaps(u, target, dt); return; }
            if (target != null ? InRange(u, target) : Gap(u, building) <= (u.Stats.Leap ? BuildingReach : u.Stats.AttackRange))
            {
                // Stand and shoot; an attack-move picks its route up again once the fight is over.
                if (u.route.Count > 0) { u.route.Clear(); u.hasGoal = false; u.arrived = false; }
                u.engaged = true;
                if (u.cooldown <= 0) { u.cooldown += u.Stats.AttackCooldown; if (target != null) Hit(u, target); else Hit(u, building); }
                return;
            }
            if (u.Order == UnitOrder.Hold) return;
            if (!u.engaged || u.route.Count == 0 || (u.chaseTimer -= dt) <= 0)
            {
                if (target != null) CloseIn(u, target);
                else CloseIn(u, building);
            }
        }
        void Chase(PrototypeUnit u, float x, float z)
        {
            var message = Message;
            PlanPath(u, x, z);
            Message = message; u.engaged = true; u.chaseTimer = ChaseInterval;
        }
        // Where to stand to hit a target, as in StarCraft, where melee units spread round it instead of queueing behind the
        // ones already striking: a free spot on an outline just within reach around the target. Spots are compared by the
        // way round the outline from where the unit meets it, not by a straight line through the target and the units at it,
        // and a spot farther round is reached along a wider outline, outside the units already there.
        // A spot is taken by any unit standing there and by an ally already on its way to it for the same target.
        void CloseIn(PrototypeUnit u, PrototypeBuilding b)
        {
            float r = u.Stats.Radius, clearance = r + Math.Min(.15f, u.Stats.AttackRange * .5f), outer = clearance + 2 * r + .05f;
            float perimeter = b.Width * 4 + 2 * (float)Math.PI * clearance, outerPerimeter = b.Width * 4 + 2 * (float)Math.PI * outer;
            float level = Map.Height(b.X, b.Z);
            int count = Math.Max(8, (int)(perimeter / Math.Max(.4f, r * 2.05f)));
            spots.Clear();
            for (int i = 0; i < count; i++)
            {
                float along = (i + .5f) / count;
                var (px, pz) = AroundFootprint(b, clearance, perimeter * along);
                var (ox, oz) = AroundFootprint(b, outer, outerPerimeter * along);
                bool usable = u.Stats.Flying || (InMap(px, pz) && Math.Abs(HeightAt(px, pz) - level) <= .51f && CanTravel(px, pz, px, pz, r));
                spots.Add((px, pz, usable, ox, oz));
            }
            int spot = BestSpot(u, b, perimeter / count, out int entry);
            if (spot < 0) { var (x, z) = Approach(u, b); Chase(u, x, z); }
            else GoRound(u, spot, entry);
        }
        // A melee unit next to its target picks a free spot round it; farther away it simply runs at the target.
        void CloseIn(PrototypeUnit u, PrototypeUnit t)
        {
            if (u.Stats.AttackRange >= 1 || u.Stats.Flying != t.Stats.Flying || Gap(u, t) > 3) { Chase(u, t.X, t.Z); return; }
            float r = u.Stats.Radius, ring = t.Stats.Radius + r + Math.Min(.22f, u.Stats.AttackRange * .75f), outer = ring + 2 * r + .05f;
            int count = Math.Max(6, (int)(2 * Math.PI * ring / (r * 2.05f)));
            spots.Clear();
            for (int i = 0; i < count; i++)
            {
                double a = 2 * Math.PI * i / count;
                float c = (float)Math.Cos(a), s = (float)Math.Sin(a), px = t.X + c * ring, pz = t.Z + s * ring;
                spots.Add((px, pz, u.Stats.Flying || (InMap(px, pz) && CanTravel(px, pz, px, pz, r)), t.X + c * outer, t.Z + s * outer));
            }
            int spot = BestSpot(u, t, 2 * (float)Math.PI * ring / count, out int entry);
            if (spot < 0) Chase(u, t.X, t.Z); else GoRound(u, spot, entry);
        }
        readonly List<(float x, float z, bool usable, float outerX, float outerZ)> spots = new List<(float, float, bool, float, float)>();
        // The unit meets the outline at the spot nearest to it; from there it walks round, `step` apart from spot to spot.
        int BestSpot(PrototypeUnit u, object target, float step, out int entry)
        {
            entry = 0; float entryDistance = float.MaxValue;
            for (int i = 0; i < spots.Count; i++)
            {
                float d = (spots[i].x - u.X) * (spots[i].x - u.X) + (spots[i].z - u.Z) * (spots[i].z - u.Z);
                if (d < entryDistance) { entryDistance = d; entry = i; }
            }
            entryDistance = (float)Math.Sqrt(entryDistance);
            int best = -1; float bestScore = float.MaxValue;
            for (int i = 0; i < spots.Count; i++)
            {
                if (!spots[i].usable) continue;
                int around = Math.Abs(i - entry); around = Math.Min(around, spots.Count - around);
                float score = entryDistance + around * step + (Taken(u, spots[i].x, spots[i].z, target) ? 1000 : 0);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
        // Next to the meeting point the unit steps straight into its spot; farther round it follows the wider outline the
        // shorter way and steps in at the end. Where that outline meets a wall or a cliff, the path search takes over.
        void GoRound(PrototypeUnit u, int spot, int entry)
        {
            int n = spots.Count, forward = (spot - entry + n) % n, way = forward <= n - forward ? 1 : -1, steps = Math.Min(forward, n - forward);
            if (steps <= 1) { Chase(u, spots[spot].x, spots[spot].z); return; }
            var route = new List<Waypoint>();
            float x = u.X, z = u.Z;
            for (int k = 0; k < steps; k++)
            {
                var p = spots[(entry + way * k + n) % n];
                if (!u.Stats.Flying && !CanTravel(x, z, p.outerX, p.outerZ, u.Stats.Radius)) { Chase(u, spots[spot].x, spots[spot].z); return; }
                route.Add(new Waypoint(p.outerX, p.outerZ)); x = p.outerX; z = p.outerZ;
            }
            var end = spots[spot];
            if (!u.Stats.Flying && !CanTravel(x, z, end.x, end.z, u.Stats.Radius)) { Chase(u, end.x, end.z); return; }
            route.Add(new Waypoint(end.x, end.z));
            u.HarvestTarget = null; u.arrived = false; u.stuckTime = 0; u.bestDistance = float.MaxValue; u.replans = 0;
            SetRoute(u, route);
            u.engaged = true; u.chaseTimer = ChaseInterval;
        }
        // A point at `distance` along the outline that runs `clearance` outside the footprint: sides and rounded corners.
        static (float, float) AroundFootprint(PrototypeBuilding b, float clearance, float distance)
        {
            float w = b.Width, arc = (float)Math.PI * .5f * clearance;
            float[] cornerX = { b.X + w, b.X + w, b.X, b.X }, cornerZ = { b.Z, b.Z + w, b.Z + w, b.Z };
            for (int s = 0; s < 4; s++)
            {
                // Side s runs towards corner s, then the arc round that corner turns to the next side.
                float sx = s == 0 ? b.X : s == 1 ? b.X + w + clearance : s == 2 ? b.X + w : b.X - clearance;
                float sz = s == 0 ? b.Z - clearance : s == 1 ? b.Z : s == 2 ? b.Z + w + clearance : b.Z + w;
                float dx = s == 0 ? 1 : s == 2 ? -1 : 0, dz = s == 1 ? 1 : s == 3 ? -1 : 0;
                if (distance <= w) return (sx + dx * distance, sz + dz * distance);
                distance -= w;
                if (distance <= arc || s == 3)
                {
                    double a = -Math.PI * .5 + s * Math.PI * .5 + Math.Min(distance, arc) / clearance;
                    return (cornerX[s] + (float)Math.Cos(a) * clearance, cornerZ[s] + (float)Math.Sin(a) * clearance);
                }
                distance -= arc;
            }
            return (b.X - clearance, b.Z);
        }
        bool Taken(PrototypeUnit u, float x, float z, object target)
        {
            foreach (var v in Units)
            {
                if (v == u || v.Stats.Flying != u.Stats.Flying || Ghost(v)) continue;
                float reach = (u.Stats.Radius + v.Stats.Radius) * .95f;
                float dx = v.X - x, dz = v.Z - z;
                if (dx * dx + dz * dz < reach * reach) return true;
                if (!v.Moving || !v.engaged || (v.Target != target && v.TargetBuilding != target)) continue;
                dx = v.goalX - x; dz = v.goalZ - z;
                if (dx * dx + dz * dz < reach * reach) return true;
            }
            return false;
        }
        // The point just outside the footprint nearest the unit.
        static (float, float) Approach(PrototypeUnit u, PrototypeBuilding b)
        {
            float cx = b.X + b.Width * .5f, cz = b.Z + b.Width * .5f;
            float x = Math.Max(b.X, Math.Min(u.X, b.X + b.Width)), z = Math.Max(b.Z, Math.Min(u.Z, b.Z + b.Width));
            float dx = x - cx, dz = z - cz, l = (float)Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-4f) { dx = 0; dz = -1; l = 1; }
            float clearance = u.Stats.Radius + .15f;
            return (x + dx / l * clearance, z + dz / l * clearance);
        }
        // Nothing left to fight: an attack-move goes on to its point, any other unit stays where the fight ended.
        void Disengage(PrototypeUnit u)
        {
            u.engaged = false; u.aggressor = null;
            if (u.Order == UnitOrder.AttackMove) { var message = Message; PlanPath(u, u.attackX, u.attackZ); Message = message; return; }
            u.goalX = u.X; u.goalZ = u.Z; Arrive(u);
        }
        PrototypeUnit PickTarget(PrototypeUnit u)
        {
            if (u.Order == UnitOrder.Attack)
            {
                // An attack on a building ignores units on the way, as in StarCraft.
                if (u.orderedBuilding != null) return null;
                if (Valid(u, u.ordered)) return u.ordered;
                // The target died or vanished from sight: the unit becomes idle and looks around like any idle unit.
                u.Order = UnitOrder.Idle; u.ordered = null;
            }
            var current = u.Target;
            bool keep = Valid(u, current);
            if (keep && InRange(u, current)) return current;
            bool hold = u.Order == UnitOrder.Hold;
            float acquire = hold ? u.Stats.AttackRange : AcquireRange(u);
            PrototypeUnit inRange = null, near = null; float inRangeScore = float.MaxValue, nearScore = float.MaxValue;
            foreach (var v in Units)
            {
                if (v.Owner == u.Owner || !Valid(u, v)) continue;
                float gap = Gap(u, v);
                if (gap > acquire) continue;
                float score = gap + (CanAttack(v, u) ? 0 : 100);
                if (gap <= u.Stats.AttackRange && score < inRangeScore) { inRangeScore = score; inRange = v; }
                if (score < nearScore) { nearScore = score; near = v; }
            }
            if (inRange != null) return inRange;
            if (keep && Gap(u, current) <= acquire + AcquireExtra) return current;
            if (near != null) return near;
            if (!hold && Valid(u, u.aggressor) && Gap(u, u.aggressor) <= acquire + AggressorReach) return u.aggressor;
            return null;
        }
        PrototypeBuilding PickBuilding(PrototypeUnit u)
        {
            if (u.Order == UnitOrder.Attack)
            {
                if (Valid(u, u.orderedBuilding)) return u.orderedBuilding;
                u.Order = UnitOrder.Idle; u.orderedBuilding = null;
            }
            float acquire = u.Order == UnitOrder.Hold ? u.Stats.AttackRange : AcquireRange(u);
            var current = u.TargetBuilding;
            if (Valid(u, current) && Gap(u, current) <= acquire + AcquireExtra) return current;
            PrototypeBuilding best = null; float bestGap = acquire;
            foreach (var b in Buildings)
            {
                if (b.Owner == u.Owner || !Valid(u, b)) continue;
                float gap = Gap(u, b);
                if (gap <= bestGap) { bestGap = gap; best = b; }
            }
            return best;
        }
        void Hit(PrototypeUnit attacker, PrototypeUnit target)
        {
            if (!(Invulnerable && target.Owner == Player))
            {
                // A machine worker's nanite cocoon takes the blow first.
                int damage = attacker.Stats.Damage, absorbed = Math.Min(target.Cocoon, damage);
                target.Cocoon -= absorbed; target.Health = Math.Max(0, target.Health - (damage - absorbed));
                target.sinceHit = 0;
            }
            Attacked?.Invoke(attacker, target);
            if (!target.Alive) return;
            target.aggressor = attacker;
            if (target.Owner == Player && attacker.Owner != Player) Alert(target.X, target.Z, false);
            CallForHelp(target.Owner, target.X, target.Z, attacker, target);
        }
        void Hit(PrototypeUnit attacker, PrototypeBuilding target)
        {
            if (!(Invulnerable && target.Owner == Player)) target.Health = Math.Max(0, target.Health - attacker.Stats.Damage);
            AttackedBuilding?.Invoke(attacker, target);
            if (target.Owner == Player && attacker.Owner != Player) Alert(target.X + target.Width * .5f, target.Z + target.Width * .5f, true);
            if (target.Health > 0) CallForHelp(target.Owner, target.X + target.Width * .5f, target.Z + target.Width * .5f, attacker, null);
        }
        void Alert(float x, float z, bool building)
        {
            if (building) { if (Clock - lastBaseAlert < AlertInterval) return; lastBaseAlert = Clock; }
            else { if (Clock - lastArmyAlert < AlertInterval) return; lastArmyAlert = Clock; }
            UnderAttack?.Invoke(x, z, building);
        }
        // Idle allies near the spot that can hit the attacker come to help.
        void CallForHelp(int side, float x, float z, PrototypeUnit attacker, PrototypeUnit victim)
        {
            foreach (var v in Units)
            {
                if (v.Owner != side || v == victim || v.Order != UnitOrder.Idle || v.Target != null || v.HarvestTarget != null || !CanAttack(v, attacker)) continue;
                float dx = v.X - x, dz = v.Z - z;
                if (dx * dx + dz * dz < HelpRadius * HelpRadius) v.aggressor = attacker;
            }
        }
        void RemoveDead()
        {
            bool units = false, buildings = false;
            foreach (var u in Units) if (!u.Alive) { units = true; break; }
            foreach (var b in Buildings) if (b.Health <= 0) { buildings = true; break; }
            if (!units && !buildings) return;
            var dead = Units.FindAll(u => !u.Alive);
            var ruined = Buildings.FindAll(b => b.Health <= 0);
            Units.RemoveAll(u => !u.Alive);
            Buildings.RemoveAll(b => b.Health <= 0);
            foreach (var u in Units)
            {
                if (u.Target != null && !u.Target.Alive) u.Target = null;
                if (u.ordered != null && !u.ordered.Alive) u.ordered = null;
                if (u.aggressor != null && !u.aggressor.Alive) u.aggressor = null;
                if (u.TargetBuilding != null && u.TargetBuilding.Health <= 0) u.TargetBuilding = null;
                if (u.orderedBuilding != null && u.orderedBuilding.Health <= 0) u.orderedBuilding = null;
                if (u.Constructing != null && u.Constructing.Health <= 0) u.Constructing = null;
                if (u.leapTarget != null && !u.leapTarget.Alive) u.leapTarget = null;
            }
            foreach (var u in dead) { u.route.Clear(); u.Target = null; u.TargetBuilding = null; Died?.Invoke(u); }
            if (ruined.Count == 0) return;
            // Freed cells change paths and vision.
            foreach (var b in ruined) NavigationChanged(b.X, b.Z, b.Width);
            foreach (var b in ruined) { b.queue.Clear(); b.started = false; Destroyed?.Invoke(b); }
            if (Winner < 0)
            {
                foreach (var b in ruined)
                {
                    if (Buildings.Exists(other => other.Owner == b.Owner)) continue;
                    Winner = b.Owner == Player ? Enemy : Player;
                    Message = Winner == Player ? "Победа: все здания противника уничтожены." : "Поражение: все ваши здания уничтожены.";
                    break;
                }
            }
            if (!ruined.Exists(b => b.Owner == Player) || Winner >= 0) return;
            Message = "Здание уничтожено.";
        }

        // Test-ground enemy base on the north-east plateau, mirroring the player's start: a command node, a supply node and a
        // factory. Destroying all of them wins the match.
        public void AddEnemyBase()
        {
            AddBuilding(2, 51, 51, Enemy, 1); AddBuilding(0, 46, 48, Enemy, 1); AddBuilding(1, 55, 46, Enemy, 1);
            UpdateVision();
        }
    }
}
