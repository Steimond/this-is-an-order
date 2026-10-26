using System;
using System.Collections.Generic;
using Order.Content;

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
        public event Action<Fix, Fix, bool> UnderAttack;
        static readonly Fix AlertInterval = 10;
        Fix lastBaseAlert = -AlertInterval, lastArmyAlert = -AlertInterval;
        // -1 while the match goes on, then the side that won. A side that has lost all its buildings is defeated (its units
        // left fight on); the last side not defeated wins.
        public int Winner { get; private set; } = -1;
        readonly bool[] defeated;
        public bool Defeated(int side) => defeated[side];
        // Acquire range is the attack range plus this much; a current target is kept this much farther still.
        static readonly Fix AcquireExtra = (Fix)2.5f, ChaseInterval = (Fix).4f, HelpRadius = 6, AggressorReach = 6;

        public bool CanAttack(PrototypeUnit attacker, PrototypeUnit target) =>
            target != null && attacker != target && attacker.Alive && target.Alive && attacker.Owner != target.Owner && attacker.Stats.Damage > 0 &&
            (target.Stats.Flying ? attacker.Stats.HitsAir : attacker.Stats.HitsGround);
        public bool CanAttack(PrototypeUnit attacker, PrototypeBuilding target) =>
            target != null && attacker.Alive && target.Health > 0 && attacker.Owner != target.Owner && attacker.Stats.Damage > 0 && attacker.Stats.HitsGround;
        public static Fix Gap(PrototypeUnit a, PrototypeUnit b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z)) - a.Stats.Radius - b.Stats.Radius;
        // From the unit's edge to the nearest point of the footprint.
        public static Fix Gap(PrototypeUnit a, PrototypeBuilding b)
        {
            Fix x = Math.Max(b.X, Math.Min(a.X, b.X + b.Width)), z = Math.Max(b.Z, Math.Min(a.Z, b.Z + b.Width));
            return Math.Sqrt((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z)) - a.Stats.Radius;
        }
        public static bool InRange(PrototypeUnit attacker, PrototypeUnit target) => Gap(attacker, target) <= attacker.Stats.AttackRange;
        // Whether the gap between the two is certainly beyond the reach, from the squared distance alone: a quick test
        // before the exact Gap, with a margin far above the rounding of the root, so it never rejects what Gap accepts.
        static readonly Fix BeyondMargin = Fix.One / 1024;
        // Whether a candidate beats the best so far: a lower score, or the same score and earlier in Units, as a pass over
        // all units in their order would pick (Near may list them in any order).
        static bool First(Fix score, PrototypeUnit candidate, Fix bestScore, PrototypeUnit best) =>
            score < bestScore || (score == bestScore && best != null && candidate.index < best.index);
        static bool Beyond(PrototypeUnit a, PrototypeUnit b, Fix reach)
        {
            Fix dx = a.X - b.X, dz = a.Z - b.Z, far = reach + a.Stats.Radius + b.Stats.Radius + BeyondMargin;
            return far > 0 && dx * dx + dz * dz > far * far;
        }
        static Fix AcquireRange(PrototypeUnit u) => Math.Max(u.Stats.AttackRange + AcquireExtra, 4);
        static Fix Reaction(PrototypeUnit u) => u.Id * (Fix).618034f % 1 * (Fix).25f;
        bool Valid(PrototypeUnit u, PrototypeUnit target) => CanAttack(u, target) && SeenBy(u.Owner, target);
        bool Valid(PrototypeUnit u, PrototypeBuilding target) => CanAttack(u, target) && SeenBy(u.Owner, target);
        // A building is seen when any of its cells is.
        public bool SeenBy(int side, PrototypeBuilding b)
        {
            if (b.Owner == side) return true;
            var grid = ground[side];
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
        public bool AttackMove(PrototypeUnit unit, Fix x, Fix z)
        {
            if (!InMap(x, z)) return false;
            if (unit.Stats.Damage <= 0) return Move(unit, x, z);
            GiveOrder(unit, UnitOrder.AttackMove); unit.attackX = x; unit.attackZ = z;
            if (!PlanPath(unit, x, z)) return false;
            if (Message != Unreachable) Message = "Приказ: атака с движением.";
            return true;
        }
        public bool AttackMoveGroup(IReadOnlyList<PrototypeUnit> units, Fix x, Fix z) => GroupOrder(units, x, z, true, false);
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
            // Any new order takes a worker off its construction: the building waits, a planned site is dropped. It also
            // ends a repair and stops a machine being taken apart.
            unit.Constructing = null; unit.SiteKind = -1;
            unit.Repairing = null; unit.RepairingUnit = null; unit.Welding = false; unit.Disassembling = false; unit.Disassembly = 0; unit.packedByHand = false;
        }

        // ---------- Steps ----------
        // Runs before movement every step: pick a target, then shoot it from where the unit stands or close in on it.
        void Fight(PrototypeUnit u, Fix dt)
        {
            if (u.cooldown > 0) u.cooldown -= dt;
            if (u.leaping) return;
            if (u.Stats.Siege) { FightDeployed(u, dt); return; }
            if (!u.Alive || u.Stats.Damage <= 0 || u.Order == UnitOrder.Move || u.HarvestTarget != null || u.Disassembling) { u.Target = null; u.TargetBuilding = null; return; }
            long since = Mark();
            var target = PickTarget(u);
            var building = target == null ? PickBuilding(u) : null;
            Spent(StepProfile.Phase.FightTargets, since);
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
                if (u.cooldown <= 0) { since = Mark(); u.cooldown += u.Stats.AttackCooldown; if (target != null) Hit(u, target); else Hit(u, building); Spent(StepProfile.Phase.FightStrikes, since); }
                return;
            }
            if (u.Order == UnitOrder.Hold) return;
            if (!u.engaged || u.route.Count == 0 || (u.chaseTimer -= dt) <= 0)
            {
                since = Mark();
                if (target != null) CloseIn(u, target);
                else CloseIn(u, building);
                Spent(StepProfile.Phase.FightApproach, since);
            }
        }
        void Chase(PrototypeUnit u, Fix x, Fix z)
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
            Fix r = u.Stats.Radius, clearance = r + Math.Min((Fix).15f, u.Stats.AttackRange * (Fix).5f), outer = clearance + 2 * r + (Fix).05f;
            Fix perimeter = b.Width * 4 + 2 * Math.PI * clearance, outerPerimeter = b.Width * 4 + 2 * Math.PI * outer;
            Fix level = Map.Height(b.X, b.Z);
            int count = Math.Max(8, (int)(perimeter / Math.Max((Fix).4f, r * (Fix)2.05f)));
            spots.Clear();
            for (int i = 0; i < count; i++)
            {
                Fix along = (i + (Fix).5f) / count;
                var (px, pz) = AroundFootprint(b, clearance, perimeter * along);
                var (ox, oz) = AroundFootprint(b, outer, outerPerimeter * along);
                bool usable = u.Stats.Flying || (InMap(px, pz) && Math.Abs(HeightAt(px, pz) - level) <= (Fix).51f && CanTravel(px, pz, px, pz, r));
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
            Fix r = u.Stats.Radius, ring = t.Stats.Radius + r + Math.Min((Fix).22f, u.Stats.AttackRange * (Fix).75f), outer = ring + 2 * r + (Fix).05f;
            int count = Math.Max(6, (int)(2 * Math.PI * ring / (r * (Fix)2.05f)));
            spots.Clear();
            for (int i = 0; i < count; i++)
            {
                Fix a = 2 * Math.PI * i / count;
                Fix c = Math.Cos(a), s = Math.Sin(a), px = t.X + c * ring, pz = t.Z + s * ring;
                spots.Add((px, pz, u.Stats.Flying || (InMap(px, pz) && CanTravel(px, pz, px, pz, r)), t.X + c * outer, t.Z + s * outer));
            }
            int spot = BestSpot(u, t, 2 * Math.PI * ring / count, out int entry);
            if (spot < 0) Chase(u, t.X, t.Z); else GoRound(u, spot, entry);
        }
        readonly List<(Fix x, Fix z, bool usable, Fix outerX, Fix outerZ)> spots = new List<(Fix, Fix, bool, Fix, Fix)>();
        // The unit meets the outline at the spot nearest to it; from there it walks round, `step` apart from spot to spot.
        int BestSpot(PrototypeUnit u, object target, Fix step, out int entry)
        {
            entry = 0; Fix entryDistance = Fix.MaxValue;
            for (int i = 0; i < spots.Count; i++)
            {
                Fix d = (spots[i].x - u.X) * (spots[i].x - u.X) + (spots[i].z - u.Z) * (spots[i].z - u.Z);
                if (d < entryDistance) { entryDistance = d; entry = i; }
            }
            entryDistance = Math.Sqrt(entryDistance);
            int best = -1; Fix bestScore = Fix.MaxValue;
            for (int i = 0; i < spots.Count; i++)
            {
                if (!spots[i].usable) continue;
                int around = Math.Abs(i - entry); around = Math.Min(around, spots.Count - around);
                Fix score = entryDistance + around * step + (Taken(u, spots[i].x, spots[i].z, target) ? 1000 : 0);
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
            Fix x = u.X, z = u.Z;
            for (int k = 0; k < steps; k++)
            {
                var p = spots[(entry + way * k + n) % n];
                if (!u.Stats.Flying && !CanTravel(x, z, p.outerX, p.outerZ, u.Stats.Radius)) { Chase(u, spots[spot].x, spots[spot].z); return; }
                route.Add(new Waypoint(p.outerX, p.outerZ)); x = p.outerX; z = p.outerZ;
            }
            var end = spots[spot];
            if (!u.Stats.Flying && !CanTravel(x, z, end.x, end.z, u.Stats.Radius)) { Chase(u, end.x, end.z); return; }
            route.Add(new Waypoint(end.x, end.z));
            u.HarvestTarget = null; u.arrived = false; u.stuckTime = 0; u.bestDistance = Fix.MaxValue; u.replans = 0;
            SetRoute(u, route);
            u.engaged = true; u.chaseTimer = ChaseInterval;
        }
        // A point at `distance` along the outline that runs `clearance` outside the footprint: sides and rounded corners.
        static (Fix, Fix) AroundFootprint(PrototypeBuilding b, Fix clearance, Fix distance)
        {
            Fix w = b.Width, arc = Math.PI * (Fix).5f * clearance;
            Fix[] cornerX = { b.X + w, b.X + w, b.X, b.X }, cornerZ = { b.Z, b.Z + w, b.Z + w, b.Z };
            for (int s = 0; s < 4; s++)
            {
                // Side s runs towards corner s, then the arc round that corner turns to the next side.
                Fix sx = s == 0 ? b.X : s == 1 ? b.X + w + clearance : s == 2 ? b.X + w : b.X - clearance;
                Fix sz = s == 0 ? b.Z - clearance : s == 1 ? b.Z : s == 2 ? b.Z + w + clearance : b.Z + w;
                Fix dx = s == 0 ? 1 : s == 2 ? -1 : 0, dz = s == 1 ? 1 : s == 3 ? -1 : 0;
                if (distance <= w) return (sx + dx * distance, sz + dz * distance);
                distance -= w;
                if (distance <= arc || s == 3)
                {
                    Fix a = -Math.PI * (Fix).5 + s * Math.PI * (Fix).5 + Math.Min(distance, arc) / clearance;
                    return (cornerX[s] + Math.Cos(a) * clearance, cornerZ[s] + Math.Sin(a) * clearance);
                }
                distance -= arc;
            }
            return (b.X - clearance, b.Z);
        }
        // A unit standing on the spot, or an ally of the same layer heading for it to strike the same target.
        bool Taken(PrototypeUnit u, Fix x, Fix z, object target)
        {
            foreach (var v in Near(x, z, u.Stats.Radius + widest, false))
            {
                if (v == u || v.Stats.Flying != u.Stats.Flying || Ghost(v)) continue;
                Fix reach = (u.Stats.Radius + v.Stats.Radius) * (Fix).95f, dx = v.X - x, dz = v.Z - z;
                if (dx * dx + dz * dz < reach * reach) return true;
            }
            var heading = target is PrototypeUnit unit ? unit.targetedBy : target is PrototypeBuilding building ? building.targetedBy : null;
            if (heading == null) return false;
            foreach (var v in heading)
            {
                if (v == u || v.Stats.Flying != u.Stats.Flying || Ghost(v) || !v.Moving || !v.engaged) continue;
                Fix reach = (u.Stats.Radius + v.Stats.Radius) * (Fix).95f, dx = v.goalX - x, dz = v.goalZ - z;
                if (dx * dx + dz * dz < reach * reach) return true;
            }
            return false;
        }
        // The point just outside the footprint nearest the unit.
        static (Fix, Fix) Approach(PrototypeUnit u, PrototypeBuilding b)
        {
            Fix cx = b.X + b.Width * (Fix).5f, cz = b.Z + b.Width * (Fix).5f;
            Fix x = Math.Max(b.X, Math.Min(u.X, b.X + b.Width)), z = Math.Max(b.Z, Math.Min(u.Z, b.Z + b.Width));
            Fix dx = x - cx, dz = z - cz, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < (Fix)1e-4f) { dx = 0; dz = -1; l = 1; }
            Fix clearance = u.Stats.Radius + (Fix).15f;
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
            Fix acquire = hold ? u.Stats.AttackRange : AcquireRange(u);
            PrototypeUnit inRange = null, near = null; Fix inRangeScore = Fix.MaxValue, nearScore = Fix.MaxValue;
            foreach (var v in Near(u.X, u.Z, acquire + u.Stats.Radius + widest, false))
            {
                if (v.Owner == u.Owner || Beyond(u, v, acquire) || !Valid(u, v)) continue;
                Fix gap = Gap(u, v);
                if (gap > acquire) continue;
                Fix score = gap + (CanAttack(v, u) ? 0 : 100);
                if (gap <= u.Stats.AttackRange && First(score, v, inRangeScore, inRange)) { inRangeScore = score; inRange = v; }
                if (First(score, v, nearScore, near)) { nearScore = score; near = v; }
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
            Fix acquire = u.Order == UnitOrder.Hold ? u.Stats.AttackRange : AcquireRange(u);
            var current = u.TargetBuilding;
            if (Valid(u, current) && Gap(u, current) <= acquire + AcquireExtra) return current;
            PrototypeBuilding best = null; Fix bestGap = acquire;
            foreach (var b in Buildings)
            {
                if (b.Owner == u.Owner || !Valid(u, b)) continue;
                Fix gap = Gap(u, b);
                if (gap <= bestGap) { bestGap = gap; best = b; }
            }
            return best;
        }
        void Hit(PrototypeUnit attacker, PrototypeUnit target)
        {
            Wound(target, Damage(attacker, target));
            if (attacker.Stats.Splash > 0) Splash(attacker, target);
            Attacked?.Invoke(attacker, target);
            if (!target.Alive) return;
            target.aggressor = attacker;
            if (target.Owner == Player && attacker.Owner != Player) Alert(target.X, target.Z, false);
            CallForHelp(target.Owner, target.X, target.Z, attacker, target);
        }
        // A blow, armour already taken off, lands here: a machine worker's nanite cocoon takes it first.
        void Wound(PrototypeUnit target, int damage)
        {
            if (Invulnerable && target.Owner == Player) return;
            int absorbed = Math.Min(target.Cocoon, damage);
            target.Cocoon -= absorbed; target.Health = Math.Max(0, target.Health - (damage - absorbed));
            target.sinceHit = 0;
        }
        void Hit(PrototypeUnit attacker, PrototypeBuilding target)
        {
            if (!(Invulnerable && target.Owner == Player)) { target.Health = Math.Max(0, target.Health - Strike(attacker, target)); target.sinceHit = 0; }
            AttackedBuilding?.Invoke(attacker, target);
            if (target.Owner == Player && attacker.Owner != Player) Alert(target.X + target.Width * (Fix).5f, target.Z + target.Width * (Fix).5f, true);
            if (target.Health > 0) CallForHelp(target.Owner, target.X + target.Width * (Fix).5f, target.Z + target.Width * (Fix).5f, attacker, null);
        }
        void Alert(Fix x, Fix z, bool building)
        {
            if (building) { if (Clock - lastBaseAlert < AlertInterval) return; lastBaseAlert = Clock; }
            else { if (Clock - lastArmyAlert < AlertInterval) return; lastArmyAlert = Clock; }
            UnderAttack?.Invoke(x, z, building);
        }
        // Idle allies near the spot that can hit the attacker come to help.
        void CallForHelp(int side, Fix x, Fix z, PrototypeUnit attacker, PrototypeUnit victim)
        {
            foreach (var v in Near(x, z, HelpRadius, false))
            {
                if (v.Owner != side || v == victim || v.Order != UnitOrder.Idle || v.Target != null || v.HarvestTarget != null || !CanAttack(v, attacker)) continue;
                Fix dx = v.X - x, dz = v.Z - z;
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
                if (u.Repairing != null && u.Repairing.Health <= 0) { u.Repairing = null; u.Welding = false; }
                if (u.RepairingUnit != null && !u.RepairingUnit.Alive) { u.RepairingUnit = null; u.Welding = false; }
                if (u.leapTarget != null && !u.leapTarget.Alive) u.leapTarget = null;
            }
            foreach (var u in dead) { u.route.Clear(); u.Target = null; u.TargetBuilding = null; Died?.Invoke(u); }
            if (ruined.Count == 0) return;
            // Freed cells change paths and vision.
            foreach (var b in ruined) NavigationChanged(b.X, b.Z, b.Width);
            foreach (var b in ruined) { b.queue.Clear(); b.started = false; b.research = null; Destroyed?.Invoke(b); }
            if (Winner < 0)
            {
                foreach (var b in ruined)
                {
                    if (defeated[b.Owner] || Buildings.Exists(other => other.Owner == b.Owner)) continue;
                    defeated[b.Owner] = true;
                    if (b.Owner == Player) Message = "Поражение: все ваши здания уничтожены.";
                }
                // The last side standing wins.
                int standing = -1, count = 0;
                for (int side = 0; side < Sides; side++) if (!defeated[side]) { standing = side; count++; }
                if (count == 1)
                {
                    Winner = standing;
                    if (Winner == Player) Message = "Победа: все здания противника уничтожены.";
                }
            }
            if (!ruined.Exists(b => b.Owner == Player) || defeated[Player]) return;
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
