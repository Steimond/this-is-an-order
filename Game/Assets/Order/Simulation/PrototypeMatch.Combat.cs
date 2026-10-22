using System;
using System.Collections.Generic;

namespace Order.Simulation
{
    // Combat in the spirit of StarCraft:
    // - a unit strikes once per cooldown while the gap between its edge and the target's edge is within its range;
    // - idle and attack-moving units pick a visible enemy within acquire range, enemies that can strike back first, then
    //   the nearest, and keep their target while it stays close;
    // - a unit that is hit answers its attacker, idle allies nearby join in;
    // - a plain move ignores enemies; holding position never leaves the spot and shoots only what is in range.
    public sealed partial class PrototypeMatch
    {
        // Every strike (attacker, target) and every death, for effects.
        public event Action<PrototypeUnit, PrototypeUnit> Attacked;
        public event Action<PrototypeUnit> Died;
        // Acquire range is the attack range plus this much; a current target is kept this much farther still.
        const float AcquireExtra = 2.5f, ChaseInterval = .4f, HelpRadius = 6, AggressorReach = 6;

        public bool CanAttack(PrototypeUnit attacker, PrototypeUnit target) =>
            target != null && attacker != target && attacker.Alive && target.Alive && attacker.Owner != target.Owner && attacker.Stats.Damage > 0 &&
            (target.Stats.Flying ? attacker.Stats.HitsAir : attacker.Stats.HitsGround);
        public static float Gap(PrototypeUnit a, PrototypeUnit b) =>
            (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z)) - a.Stats.Radius - b.Stats.Radius;
        public static bool InRange(PrototypeUnit attacker, PrototypeUnit target) => Gap(attacker, target) <= attacker.Stats.AttackRange;
        static float AcquireRange(PrototypeUnit u) => Math.Max(u.Stats.AttackRange + AcquireExtra, 4);
        static float Reaction(PrototypeUnit u) => u.Id * .618034f % 1 * .25f;
        bool Valid(PrototypeUnit u, PrototypeUnit target) => CanAttack(u, target) && SeenBy(u.Owner, target);

        // ---------- Orders ----------
        // Pursue one visible enemy until it dies; a unit that cannot hit it walks up to it instead.
        public bool Attack(PrototypeUnit unit, PrototypeUnit target)
        {
            if (target == null || !target.Alive || target.Owner == unit.Owner || !SeenBy(unit.Owner, target)) return false;
            if (!CanAttack(unit, target)) return Move(unit, target.X, target.Z);
            GiveOrder(unit, UnitOrder.Attack); unit.ordered = target; unit.Target = target;
            Chase(unit, target);
            Message = "Приказ: атаковать цель.";
            return true;
        }
        public bool AttackGroup(IReadOnlyList<PrototypeUnit> units, PrototypeUnit target)
        {
            bool any = false, attacking = false;
            foreach (var u in units) { bool can = CanAttack(u, target); if (Attack(u, target)) { any = true; attacking |= can; } }
            if (any) Message = attacking ? "Приказ: атаковать цель." : "Эти юниты не бьют такую цель: подходят к ней.";
            return any;
        }
        // Go to the point and fight every enemy met on the way; a unit without a weapon simply moves.
        public bool AttackMove(PrototypeUnit unit, float x, float z)
        {
            if (!InMap(x, z)) return false;
            if (unit.Stats.Damage <= 0) return Move(unit, x, z);
            GiveOrder(unit, UnitOrder.AttackMove); unit.attackX = x; unit.attackZ = z;
            if (!PlanPath(unit, x, z)) return false;
            if (!Message.StartsWith("Точка недоступна")) Message = "Приказ: атака с движением.";
            return true;
        }
        public bool AttackMoveGroup(IReadOnlyList<PrototypeUnit> units, float x, float z) => GroupOrder(units, x, z, true);
        public void Hold(PrototypeUnit unit)
        {
            GiveOrder(unit, UnitOrder.Hold);
            unit.route.Clear(); unit.HarvestTarget = null; unit.hasGoal = false; unit.arrived = false;
            Message = "Приказ: держать позицию.";
        }
        void GiveOrder(PrototypeUnit unit, UnitOrder order)
        {
            unit.Order = order; unit.ordered = null; unit.Target = null; unit.aggressor = null; unit.engaged = false;
            unit.returning = false; unit.returns = 0;
        }

        // ---------- Steps ----------
        // Runs before movement every step: pick a target, then shoot it from where the unit stands or close in on it.
        void Fight(PrototypeUnit u, float dt)
        {
            if (u.cooldown > 0) u.cooldown -= dt;
            if (!u.Alive || u.Stats.Damage <= 0 || u.Order == UnitOrder.Move || u.HarvestTarget != null) { u.Target = null; return; }
            var target = PickTarget(u);
            // A new target takes a moment to aim at, different for every unit, so a squad does not fire in one volley.
            if (target != null && target != u.Target && u.cooldown <= 0) u.cooldown = Reaction(u);
            u.Target = target;
            if (target == null) { if (u.engaged) Disengage(u); return; }
            if (InRange(u, target))
            {
                // Stand and shoot; an attack-move picks its route up again once the fight is over.
                if (u.route.Count > 0) { u.route.Clear(); u.hasGoal = false; u.arrived = false; }
                u.engaged = true;
                if (u.cooldown <= 0) { u.cooldown += u.Stats.AttackCooldown; Hit(u, target); }
                return;
            }
            if (u.Order == UnitOrder.Hold) return;
            if (!u.engaged || u.route.Count == 0 || (u.chaseTimer -= dt) <= 0) Chase(u, target);
        }
        void Chase(PrototypeUnit u, PrototypeUnit target)
        {
            var message = Message;
            PlanPath(u, target.X, target.Z);
            Message = message; u.engaged = true; u.chaseTimer = ChaseInterval;
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
        void Hit(PrototypeUnit attacker, PrototypeUnit target)
        {
            target.Health = Math.Max(0, target.Health - attacker.Stats.Damage);
            Attacked?.Invoke(attacker, target);
            if (!target.Alive) return;
            target.aggressor = attacker;
            // Idle allies nearby that can hit the attacker come to help.
            foreach (var v in Units)
            {
                if (v.Owner != target.Owner || v == target || v.Order != UnitOrder.Idle || v.Target != null || v.HarvestTarget != null || !CanAttack(v, attacker)) continue;
                float dx = v.X - target.X, dz = v.Z - target.Z;
                if (dx * dx + dz * dz < HelpRadius * HelpRadius) v.aggressor = attacker;
            }
        }
        void RemoveDead()
        {
            bool any = false;
            foreach (var u in Units) if (!u.Alive) { any = true; break; }
            if (!any) return;
            var dead = Units.FindAll(u => !u.Alive);
            Units.RemoveAll(u => !u.Alive);
            foreach (var u in Units)
            {
                if (u.Target != null && !u.Target.Alive) u.Target = null;
                if (u.ordered != null && !u.ordered.Alive) u.ordered = null;
                if (u.aggressor != null && !u.aggressor.Alive) u.aggressor = null;
            }
            foreach (var u in dead) { u.route.Clear(); u.Target = null; Died?.Invoke(u); }
        }
    }
}
