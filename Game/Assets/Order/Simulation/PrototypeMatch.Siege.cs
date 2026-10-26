using System;
using Order.Content;

namespace Order.Simulation
{
    public enum SiegeState { Packed, Deploying, Deployed, Packing }

    // The humans' howitzer, in the spirit of StarCraft's siege mode:
    // - it fires only deployed; deploying and packing up take DeploySeconds, and while it is not packed it does not move;
    // - it reaches far (AttackRange) but not near: a target nearer than MinRange is safe from it; its shell hits the target
    //   and half as hard every other enemy ground unit within Splash of it;
    // - so that the player and the voice need no extra command, it deploys by itself when something it can hit comes
    //   within reach and it is not under a move order; a move order packs it up first, and a deployed one on an
    //   attack-move with nothing left to shoot packs up after PackAfter seconds and goes on;
    // - E deploys it on the spot and keeps it there, or packs it up and keeps it packed until the next order.
    public sealed partial class PrototypeMatch
    {
        public static readonly Fix PackAfter = 3, SplashShare = (Fix).5f;
        static bool Anchored(PrototypeUnit u) => u.Stats.Siege && u.SiegeMode != SiegeState.Packed;

        public bool ToggleDeploy(PrototypeUnit u)
        {
            if (u == null || !u.Alive || !u.Stats.Siege) return false;
            bool deploy = u.SiegeMode == SiegeState.Packed || u.SiegeMode == SiegeState.Packing;
            GiveOrder(u, UnitOrder.Idle);
            u.route.Clear(); u.hasGoal = false;
            if (deploy) Deploy(u); else { Pack(u); u.packedByHand = true; }
            if (u.Owner == Player) Message = deploy ? $"{u.Stats.Name}: развёртывание." : $"{u.Stats.Name}: сворачивается.";
            return true;
        }
        // Either change picks up from where the other one was, so turning back halfway costs only the way back.
        static void Deploy(PrototypeUnit u)
        {
            if (u.SiegeMode == SiegeState.Deployed || u.SiegeMode == SiegeState.Deploying) return;
            u.siegeTimer = u.SiegeMode == SiegeState.Packing ? Math.Max(0, u.Stats.DeploySeconds - u.siegeTimer) : 0;
            u.SiegeMode = SiegeState.Deploying; u.siegeIdle = 0;
            u.route.Clear(); u.hasGoal = false;
        }
        static void Pack(PrototypeUnit u)
        {
            if (u.SiegeMode == SiegeState.Packed || u.SiegeMode == SiegeState.Packing) return;
            u.siegeTimer = u.SiegeMode == SiegeState.Deploying ? Math.Max(0, u.Stats.DeploySeconds - u.siegeTimer) : 0;
            u.SiegeMode = SiegeState.Packing;
        }

        // Runs instead of the usual fight step for a howitzer.
        void FightDeployed(PrototypeUnit u, Fix dt)
        {
            var stats = u.Stats;
            if (u.SiegeMode == SiegeState.Deploying || u.SiegeMode == SiegeState.Packing)
            {
                if ((u.siegeTimer += dt) < stats.DeploySeconds) return;
                u.siegeTimer = 0; u.SiegeMode = u.SiegeMode == SiegeState.Deploying ? SiegeState.Deployed : SiegeState.Packed;
            }
            if (!u.Alive || stats.Damage <= 0) return;
            // A move order packs it up; the move starts once it is packed.
            if (u.Order == UnitOrder.Move) { u.Target = null; u.TargetBuilding = null; u.engaged = false; Pack(u); return; }
            var target = u.packedByHand ? null : SiegeTarget(u);
            var building = target == null && !u.packedByHand ? SiegeBuilding(u) : null;
            if ((target != null && target != u.Target) || (building != null && building != u.TargetBuilding)) { if (u.cooldown <= 0) u.cooldown = Reaction(u); }
            u.Target = target; u.TargetBuilding = building;
            if (target != null || building != null)
            {
                u.siegeIdle = 0; u.engaged = true;
                if (u.SiegeMode == SiegeState.Packed) { Deploy(u); return; }
                if (u.SiegeMode != SiegeState.Deployed) return;
                if (u.route.Count > 0) { u.route.Clear(); u.hasGoal = false; }
                if (u.cooldown <= 0) { u.cooldown += stats.AttackCooldown; if (target != null) Hit(u, target); else Hit(u, building); }
                return;
            }
            u.engaged = false;
            // An attack on a target out of reach: pack up, drive closer, deploy again once it is within reach.
            if (u.Order == UnitOrder.Attack)
            {
                bool alive = u.orderedBuilding != null ? Valid(u, u.orderedBuilding) : Valid(u, u.ordered);
                if (!alive) { u.Order = UnitOrder.Idle; u.ordered = null; u.orderedBuilding = null; return; }
                if (u.SiegeMode == SiegeState.Deployed) { Pack(u); return; }
                if (u.SiegeMode != SiegeState.Packed || (u.route.Count > 0 && (u.chaseTimer -= dt) > 0)) return;
                if (u.ordered != null) Chase(u, u.ordered.X, u.ordered.Z);
                else { var (x, z) = Approach(u, u.orderedBuilding); Chase(u, x, z); }
                return;
            }
            if (u.Order != UnitOrder.AttackMove) return;
            // An attack-move with nothing left to shoot: after a while it packs up and goes on to its point.
            if (u.SiegeMode == SiegeState.Deployed) { if ((u.siegeIdle += dt) >= PackAfter) Pack(u); return; }
            if (u.SiegeMode == SiegeState.Packed && u.route.Count == 0) { var message = Message; PlanPath(u, u.attackX, u.attackZ); Message = message; }
        }
        bool InBand(PrototypeUnit u, Fix gap) => gap >= u.Stats.MinRange && gap <= u.Stats.AttackRange;
        // What it can shell from where it stands: the ordered target, the current one, then the nearest of those that can
        // strike back, then the nearest.
        PrototypeUnit SiegeTarget(PrototypeUnit u)
        {
            if (u.Order == UnitOrder.Attack) return u.orderedBuilding == null && Valid(u, u.ordered) && InBand(u, Gap(u, u.ordered)) ? u.ordered : null;
            if (Valid(u, u.Target) && InBand(u, Gap(u, u.Target))) return u.Target;
            PrototypeUnit best = null; Fix bestScore = Fix.MaxValue;
            foreach (var v in Units)
            {
                if (v.Owner == u.Owner || !Valid(u, v)) continue;
                Fix gap = Gap(u, v);
                if (!InBand(u, gap)) continue;
                Fix score = gap + (CanAttack(v, u) ? 0 : 100);
                if (score < bestScore) { bestScore = score; best = v; }
            }
            return best;
        }
        PrototypeBuilding SiegeBuilding(PrototypeUnit u)
        {
            if (u.Order == UnitOrder.Attack) return Valid(u, u.orderedBuilding) && InBand(u, Gap(u, u.orderedBuilding)) ? u.orderedBuilding : null;
            if (Valid(u, u.TargetBuilding) && InBand(u, Gap(u, u.TargetBuilding))) return u.TargetBuilding;
            PrototypeBuilding best = null; Fix bestGap = Fix.MaxValue;
            foreach (var b in Buildings)
            {
                if (b.Owner == u.Owner || !Valid(u, b)) continue;
                Fix gap = Gap(u, b);
                if (InBand(u, gap) && gap < bestGap) { bestGap = gap; best = b; }
            }
            return best;
        }
        // The shell bursts on the target: every other enemy ground unit within Splash takes half the blow, less its armour.
        void Splash(PrototypeUnit attacker, PrototypeUnit target)
        {
            Fix reach = attacker.Stats.Splash;
            foreach (var v in Units)
            {
                if (v == target || !v.Alive || v.Owner == attacker.Owner || v.Stats.Flying) continue;
                Fix dx = v.X - target.X, dz = v.Z - target.Z;
                if (Math.Sqrt(dx * dx + dz * dz) - v.Stats.Radius > reach) continue;
                Wound(v, Math.Max(1, (int)(Damage(attacker) * SplashShare) - Armor(v)));
                if (v.Alive) v.aggressor ??= attacker;
                if (v.Owner == Player && attacker.Owner != Player) Alert(v.X, v.Z, false);
            }
        }
    }
}
