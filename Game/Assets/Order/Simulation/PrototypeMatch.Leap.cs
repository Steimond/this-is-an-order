using System;
using Order.Content;

namespace Order.Simulation
{
    // The machines' leaper (SETTING.md): it attacks by jumping through one enemy at a time.
    // - It jumps at its target once the target is within the reach of the jump and the jump has recharged; the target is
    //   hit as the leaper passes through it, and only the target.
    // - It lands behind the target, closer to it than it stood before the jump (half the distance, at least clear of it).
    // - Another unit on the line before the target stops the jump: the leaper crashes into it, deals no damage, and the
    //   jump still has to recharge.
    // - If the landing spot is taken, it stops right before the obstacle, still behind the target if there is room.
    // - It cannot jump across buildings, walls or cliffs, up or down: then it walks like any melee unit.
    // - In flight it passes over everything and cannot be pushed; a new order waits until it lands.
    // Against buildings it strikes in melee.
    public sealed partial class PrototypeMatch
    {
        const float LeapSpeed = 13, LeapShare = .5f, BuildingReach = .3f;
        enum LeapStart { Jumped, Recharging, Blocked, OutOfReach }

        LeapStart TryLeap(PrototypeUnit u, PrototypeUnit target)
        {
            if (target.Stats.Flying || Gap(u, target) > u.Stats.AttackRange) return LeapStart.OutOfReach;
            if (u.cooldown > 0) return LeapStart.Recharging;
            float dx = target.X - u.X, dz = target.Z - u.Z, d = (float)Math.Sqrt(dx * dx + dz * dz), r = u.Stats.Radius;
            if (d < 1e-3f) return LeapStart.Blocked;
            dx /= d; dz /= d;
            if (!CanTravel(u.X, u.Z, target.X, target.Z, r)) return LeapStart.Blocked;
            // The first unit on the line before the target, if any.
            float stop = float.MaxValue;
            foreach (var v in Units)
            {
                if (v == u || v == target || v.Stats.Flying || !v.Alive) continue;
                float along = (v.X - u.X) * dx + (v.Z - u.Z) * dz, across = Math.Abs((v.X - u.X) * dz - (v.Z - u.Z) * dx), reach = r + v.Stats.Radius;
                if (along <= 0 || along >= d || across >= reach) continue;
                stop = Math.Min(stop, along - (float)Math.Sqrt(reach * reach - across * across));
            }
            if (stop < float.MaxValue)
            {
                // Pressed against that unit already: nothing to jump, it walks round instead.
                if (stop < .25f) return LeapStart.Blocked;
                StartLeap(u, u.X + dx * stop, u.Z + dz * stop, null, 0);
                return LeapStart.Jumped;
            }
            // Behind the target, as far as the ground and the units there allow.
            float pass = target.Stats.Radius + r + .02f, land = Math.Max(pass, Math.Min(d * LeapShare, d - .05f));
            if (!CanTravel(target.X, target.Z, target.X + dx * pass, target.Z + dz * pass, r)) return LeapStart.Blocked;
            for (float step = land; ; step -= .1f)
            {
                if (step <= pass) { land = pass; break; }
                float lx = target.X + dx * step, lz = target.Z + dz * step;
                if (CanTravel(target.X, target.Z, lx, lz, r) && LandingFree(u, target, lx, lz)) { land = step; break; }
            }
            StartLeap(u, target.X + dx * land, target.Z + dz * land, target, d - target.Stats.Radius);
            return LeapStart.Jumped;
        }
        bool LandingFree(PrototypeUnit u, PrototypeUnit target, float x, float z)
        {
            foreach (var v in Units)
            {
                if (v == u || v == target || v.Stats.Flying || !v.Alive) continue;
                float reach = u.Stats.Radius + v.Stats.Radius;
                if ((v.X - x) * (v.X - x) + (v.Z - z) * (v.Z - z) < reach * reach) return false;
            }
            return true;
        }
        void StartLeap(PrototypeUnit u, float x, float z, PrototypeUnit target, float passAt)
        {
            u.leaping = true; u.leapFromX = u.X; u.leapFromZ = u.Z; u.leapToX = x; u.leapToZ = z;
            u.leapLength = Math.Max(.01f, (float)Math.Sqrt((x - u.X) * (x - u.X) + (z - u.Z) * (z - u.Z)));
            u.leapDone = 0; u.leapPass = passAt; u.leapTarget = target; u.leapHit = false;
            u.cooldown = u.Stats.AttackCooldown;
            u.route.Clear(); u.hasGoal = false; u.arrived = false;
        }
        // One step of flight along the line; the target takes the blow as the leaper goes through it.
        void StepLeap(PrototypeUnit u, float dt)
        {
            u.leapDone = Math.Min(u.leapLength, u.leapDone + LeapSpeed * dt);
            float t = u.leapDone / u.leapLength;
            u.X = u.leapFromX + (u.leapToX - u.leapFromX) * t; u.Z = u.leapFromZ + (u.leapToZ - u.leapFromZ) * t;
            var target = u.leapTarget;
            if (target != null && !u.leapHit && u.leapDone >= u.leapPass) { u.leapHit = true; if (target.Alive) Hit(u, target); }
            if (u.leapDone < u.leapLength) return;
            u.leaping = false; u.leapTarget = null;
            u.arrived = true; u.restX = u.X; u.restZ = u.Z; u.calmTime = 0;
        }
        // A leaper's part of Fight: jump, wait for the next jump within reach, or walk up to the target.
        void FightByLeaps(PrototypeUnit u, PrototypeUnit target, float dt)
        {
            var start = TryLeap(u, target);
            u.engaged = true;
            if (start == LeapStart.Jumped) return;
            if (start == LeapStart.Recharging) { if (u.route.Count > 0) { u.route.Clear(); u.hasGoal = false; u.arrived = false; } return; }
            // Up against a target it cannot jump through (a wall right behind it, say), it strikes it where it stands.
            if (start == LeapStart.Blocked && Gap(u, target) <= BuildingReach)
            {
                if (u.route.Count > 0) { u.route.Clear(); u.hasGoal = false; u.arrived = false; }
                if (u.cooldown <= 0) { u.cooldown = u.Stats.AttackCooldown; Hit(u, target); }
                return;
            }
            if (u.Order == UnitOrder.Hold) return;
            if (u.route.Count == 0 || (u.chaseTimer -= dt) <= 0) Chase(u, target.X, target.Z);
        }
    }
}
