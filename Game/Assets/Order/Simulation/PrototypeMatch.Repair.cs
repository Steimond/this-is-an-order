using System;
using Order.Content;

namespace Order.Simulation
{
    // How the races get their health back (SETTING.md).
    // Humans repair, the way SCVs do in StarCraft: a worker mends a finished building or a piece of machinery of its side
    // (the drone, the gunship) while it stands by it. A full repair takes as long as building or training the target, but
    // never less than MinRepairSeconds, and costs RepairShare of its price, paid as the health comes back; several
    // workers can mend one target, and a side out of metal or plasma stops repairing. A repair can wait in the Shift queue,
    // and a worker with automatic repair on mends what is damaged near it whenever it stands idle.
    // Machines mend themselves instead: inside the coverage of their network a unit or building that has not been hit
    // for MendCalm seconds recovers a share of its health every second. A machine unit can also be taken apart there:
    // after DisassemblySeconds it is gone and the side gets back half its price, less the share of health it had lost.
    public sealed partial class PrototypeMatch
    {
        public const float RepairShare = .25f, MinRepairSeconds = 20, RepairReach = .6f;
        public const float MendCalm = 5, MendUnits = .02f, MendBuildings = .005f, DisassemblySeconds = 2, DisassemblyRefund = .5f;

        // Why the worker cannot repair this target now, or null.
        public string RepairBlocked(PrototypeUnit worker, PrototypeBuilding building) =>
            RepairBlocked(worker, building?.Owner ?? -1, building == null ? 0 : building.Health, building?.Stats.Health ?? 0)
            ?? (building.Ready < 1 ? "Здание ещё строится: ПКМ рабочим — продолжить стройку." : null);
        public string RepairBlocked(PrototypeUnit worker, PrototypeUnit target) =>
            RepairBlocked(worker, target?.Owner ?? -1, target?.Health ?? 0, target?.Stats.Health ?? 0)
            ?? (target == worker ? "Рабочий не может чинить сам себя." : !target.Stats.Mechanical ? $"{target.Stats.Name} — не техника: ремонт не нужен." : null);
        public bool Repair(PrototypeUnit worker, PrototypeBuilding building)
        {
            var reason = RepairBlocked(worker, building);
            if (reason != null) { if (worker == null || worker.Owner == Player) Message = reason; return false; }
            StartRepair(worker);
            worker.Repairing = building;
            if (!SiteSpot(worker, building.X, building.Z, building.Width, out float sx, out float sz)) { worker.Repairing = null; if (worker.Owner == Player) Message = "К зданию не подойти."; return false; }
            Walk(worker, sx, sz);
            if (worker.Owner == Player) Message = $"{worker.Stats.Name}: ремонт — {building.Stats.Name}.";
            return true;
        }
        public bool Repair(PrototypeUnit worker, PrototypeUnit target)
        {
            var reason = RepairBlocked(worker, target);
            if (reason != null) { if (worker == null || worker.Owner == Player) Message = reason; return false; }
            StartRepair(worker);
            worker.RepairingUnit = target;
            WalkTo(worker, target);
            if (worker.Owner == Player) Message = $"{worker.Stats.Name}: ремонт — {target.Stats.Name}.";
            return true;
        }
        // Shift: the worker repairs it after what it is doing now; the target is checked again when its turn comes.
        public bool QueueRepair(PrototypeUnit worker, PrototypeBuilding building) => QueueRepair(worker, RepairBlocked(worker, building), new QueuedOrder(building, true), building?.Stats.Name);
        public bool QueueRepair(PrototypeUnit worker, PrototypeUnit target) => QueueRepair(worker, RepairBlocked(worker, target), new QueuedOrder(target, true), target?.Stats.Name);
        bool QueueRepair(PrototypeUnit worker, string reason, QueuedOrder order, string name)
        {
            if (reason != null) { if (worker == null || worker.Owner == Player) Message = reason; return false; }
            int waiting = worker.queued.Count;
            Enqueue(worker, order);
            if (worker.Owner == Player && worker.queued.Count > waiting) Message = $"Ремонт добавлен в очередь: {name}.";
            return true;
        }

        // Autocast, as SCVs have it: an idle worker with automatic repair on mends the nearest damaged building or machinery
        // of its side within AutoRepairReach, without a word in the event line.
        public const float AutoRepairReach = 6;
        float autoRepairTimer;
        public bool SetAutoRepair(PrototypeUnit worker, bool on)
        {
            if (worker == null || worker.Kind != UnitKind.Worker || !Roster(worker.Owner).Repairs) return false;
            worker.AutoRepair = on;
            return true;
        }
        void TickAutoRepair(float dt)
        {
            if ((autoRepairTimer += dt) < .5f) return;
            autoRepairTimer = 0;
            foreach (var u in Units)
            {
                if (!u.AutoRepair || !u.Alive || u.Order != UnitOrder.Idle || u.Moving || u.HarvestTarget != null || Busy(u) || u.queued.Count > 0) continue;
                PrototypeBuilding building = null; PrototypeUnit machine = null; float best = AutoRepairReach;
                foreach (var b in Buildings)
                {
                    if (b.Owner != u.Owner || b.Ready < 1 || b.Health <= 0 || b.Health >= b.Stats.Health) continue;
                    float gap = Gap(u, b);
                    if (gap <= best) { best = gap; building = b; }
                }
                foreach (var v in Units)
                {
                    if (v == u || v.Owner != u.Owner || !v.Alive || !v.Stats.Mechanical || v.Health >= v.Stats.Health) continue;
                    float gap = Gap(u, v);
                    if (gap <= best) { best = gap; machine = v; building = null; }
                }
                if (building == null && machine == null) continue;
                var message = Message;
                if (machine != null) Repair(u, machine); else Repair(u, building);
                Message = message;
            }
        }
        string RepairBlocked(PrototypeUnit worker, int owner, int health, int max)
        {
            if (worker == null || !worker.Alive || worker.Kind != UnitKind.Worker || !Roster(worker.Owner).Repairs) return "Чинить могут только рабочие людей.";
            if (owner != worker.Owner) return "Чинить можно только своё.";
            return health <= 0 ? "Цели больше нет." : health >= max ? "Ремонт не нужен: прочность полная." : null;
        }
        void StartRepair(PrototypeUnit worker)
        {
            GiveOrder(worker, UnitOrder.Move);
            worker.repairHealth = worker.repairMetal = worker.repairPlasma = worker.repairTimer = 0;
        }
        void Walk(PrototypeUnit worker, float x, float z) { var message = Message; PlanPath(worker, x, z); Message = message; }
        // Up to the unit's side nearest the worker; an air unit is mended from the ground beneath it.
        void WalkTo(PrototypeUnit worker, PrototypeUnit target)
        {
            float dx = worker.X - target.X, dz = worker.Z - target.Z, d = (float)Math.Sqrt(dx * dx + dz * dz), reach = target.Stats.Radius + worker.Stats.Radius + .1f;
            if (d < 1e-3f) { dx = 1; dz = 0; d = 1; }
            Walk(worker, target.X + dx / d * reach, target.Z + dz / d * reach);
            worker.repairTimer = 0;
        }

        void TickRepair(float dt)
        {
            foreach (var u in Units)
            {
                u.Welding = false;
                if (!u.Alive || (u.Repairing == null && u.RepairingUnit == null)) continue;
                var b = u.Repairing; var target = u.RepairingUnit;
                if (b != null && (b.Health <= 0 || !Buildings.Contains(b))) { u.Repairing = null; continue; }
                if (target != null && !target.Alive) { u.RepairingUnit = null; continue; }
                int health = b != null ? b.Health : target.Health, max = b != null ? b.Stats.Health : target.Stats.Health;
                string name = b != null ? b.Stats.Name : target.Stats.Name;
                if (health >= max) { EndRepair(u, $"Отремонтировано: {name}."); continue; }
                bool near = b != null ? Gap(u, b) <= BuildReach : Gap(u, target) <= RepairReach;
                if (!near)
                {
                    // On its way; a unit that moved off is followed, a worker pushed away from a building walks back.
                    if (u.Moving && (target == null || (u.repairTimer += dt) < .5f)) continue;
                    u.repairTimer = 0;
                    if (target != null) WalkTo(u, target);
                    else if (SiteSpot(u, b.X, b.Z, b.Width, out float sx, out float sz)) Walk(u, sx, sz);
                    else EndRepair(u, "К зданию не подойти.");
                    continue;
                }
                // At the target the worker has arrived: its move is over, so orders queued after the repair can start.
                if (u.Moving) Arrive(u);
                u.Welding = true;
                float seconds = Math.Max(MinRepairSeconds, b != null ? b.Stats.BuildSeconds : target.Stats.TrainSeconds);
                int metal = b != null ? b.Stats.Metal : target.Stats.Metal, plasma = b != null ? b.Stats.Plasma : target.Stats.Plasma;
                u.repairHealth += max * dt * (FastBuild && u.Owner == Player ? FastFactor : 1) / seconds;
                int points = Math.Min((int)u.repairHealth, max - health);
                if (points <= 0) continue;
                // The price of the health restored, owed in fractions and paid in whole units.
                float owedMetal = u.repairMetal + points * metal * RepairShare / max, owedPlasma = u.repairPlasma + points * plasma * RepairShare / max;
                int payMetal = (int)owedMetal, payPlasma = (int)owedPlasma;
                if (metalStock[u.Owner] < payMetal || plasmaStock[u.Owner] < payPlasma)
                {
                    EndRepair(u, $"Ремонт остановлен: недостаточно {(metalStock[u.Owner] < payMetal ? "металла" : "плазмы")}.");
                    continue;
                }
                metalStock[u.Owner] -= payMetal; plasmaStock[u.Owner] -= payPlasma;
                u.repairMetal = owedMetal - payMetal; u.repairPlasma = owedPlasma - payPlasma; u.repairHealth -= points;
                if (b != null) b.Health += points; else target.Health += points;
            }
        }
        void EndRepair(PrototypeUnit u, string message)
        {
            u.Repairing = null; u.RepairingUnit = null; u.Welding = false;
            if (u.Owner == Player) Message = message;
        }

        // Machines inside their network get their health back once they have not been hit for a while.
        void TickMending(float dt)
        {
            foreach (var u in Units)
            {
                u.sinceHit += dt;
                u.Mending = Roster(u.Owner).Mends && u.Alive && !u.Disassembling && u.Health < u.Stats.Health && u.sinceHit >= MendCalm && Covered(u.Owner, u.X, u.Z);
                if (!u.Mending) { u.mended = 0; continue; }
                u.mended += u.Stats.Health * MendUnits * dt;
                int points = (int)u.mended; u.mended -= points;
                u.Health = Math.Min(u.Stats.Health, u.Health + points);
            }
            foreach (var b in Buildings)
            {
                b.sinceHit += dt;
                // A powered machine building stands in the network; one still rising gains its health as it rises.
                b.Mending = Roster(b.Owner).Mends && b.Health > 0 && b.Ready >= 1 && b.Linked && b.Health < b.Stats.Health && b.sinceHit >= MendCalm;
                if (!b.Mending) { b.mended = 0; continue; }
                b.mended += b.Stats.Health * MendBuildings * dt;
                int points = (int)b.mended; b.mended -= points;
                b.Health = Math.Min(b.Stats.Health, b.Health + points);
            }
        }

        // What taking the unit apart now would give back: half its price, less the share of health it has lost.
        public (int metal, int plasma) DisassemblyReturn(PrototypeUnit unit)
        {
            float share = DisassemblyRefund * Math.Max(0, unit.Health) / unit.Stats.Health;
            return ((int)(unit.Stats.Metal * share), (int)(unit.Stats.Plasma * share));
        }
        public string DisassemblyBlocked(PrototypeUnit unit)
        {
            if (unit == null || !unit.Alive) return "Юнита больше нет.";
            if (!Roster(unit.Owner).Mends) return "Разбирать на части можно только машины.";
            if (unit.Leaping) return "В прыжке разобрать нельзя.";
            return Covered(unit.Owner, unit.X, unit.Z) ? null : "Разобрать можно только в покрытии сети.";
        }
        public bool Disassemble(PrototypeUnit unit)
        {
            var reason = DisassemblyBlocked(unit);
            if (reason != null) { if (unit == null || unit.Owner == Player) Message = reason; return false; }
            GiveOrder(unit, UnitOrder.Idle);
            StopHarvest(unit); unit.route.Clear(); unit.hasGoal = false; unit.arrived = false;
            unit.Disassembling = true; unit.Disassembly = 0;
            var (metal, plasma) = DisassemblyReturn(unit);
            if (unit.Owner == Player) Message = $"{unit.Stats.Name}: разборка, вернётся металл {metal}" + (plasma > 0 ? $", плазма {plasma}." : ".");
            return true;
        }
        void TickDisassembly(float dt)
        {
            foreach (var u in Units)
            {
                if (!u.Disassembling || !u.Alive) continue;
                if (!Covered(u.Owner, u.X, u.Z)) { u.Disassembling = false; u.Disassembly = 0; if (u.Owner == Player) Message = "Разборка прервана: нет сигнала сети."; continue; }
                u.Disassembly = Math.Min(1, u.Disassembly + dt / DisassemblySeconds);
                if (u.Disassembly < 1) continue;
                var (metal, plasma) = DisassemblyReturn(u);
                metalStock[u.Owner] += metal; plasmaStock[u.Owner] += plasma;
                // Taken apart: it leaves the match like a fallen unit, and the view shows the nanites flowing away.
                u.Health = 0;
                if (u.Owner == Player) Message = $"Разобран: {u.Stats.Name}. Возвращено: металл {metal}" + (plasma > 0 ? $", плазма {plasma}." : ".");
            }
        }
    }
}
