using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // An order waiting in a unit's queue (Shift in StarCraft): a point to move or attack-move to, a target to attack, a
    // building for a worker to build with its corner at (X, Z) (BuildKind -1: not a build order), or a building or piece
    // of machinery for a worker to repair (Mend).
    public readonly struct QueuedOrder
    {
        public readonly UnitOrder Order;
        public readonly Fix X, Z;
        public readonly PrototypeUnit Unit;
        public readonly PrototypeBuilding Building;
        public readonly int BuildKind;
        public readonly bool Mend;
        public QueuedOrder(UnitOrder order, Fix x, Fix z) { Order = order; X = x; Z = z; Unit = null; Building = null; BuildKind = -1; Mend = false; }
        public static QueuedOrder Build(int kind, int x, int z) => new QueuedOrder(kind, x, z);
        QueuedOrder(int kind, int x, int z) { Order = UnitOrder.Move; X = x; Z = z; Unit = null; Building = null; BuildKind = kind; Mend = false; }
        public QueuedOrder(PrototypeUnit unit, bool mend = false) { Order = mend ? UnitOrder.Move : UnitOrder.Attack; X = unit.X; Z = unit.Z; Unit = unit; Building = null; BuildKind = -1; Mend = mend; }
        public QueuedOrder(PrototypeBuilding building, bool mend = false) { Order = mend ? UnitOrder.Move : UnitOrder.Attack; X = building.X + building.Width * (Fix).5f; Z = building.Z + building.Width * (Fix).5f; Unit = null; Building = building; BuildKind = -1; Mend = mend; }
    }

    // Order queue: a queued order starts once the unit's current order is done (a move or attack-move arrived, the attack
    // target is gone). An idle or harvesting unit starts it at once, a worker on a construction after the building is done.
    // Any order given without Shift, Stop and Hold clear the queue; holding position never ends by itself, so orders queued
    // after it wait.
    public sealed partial class PrototypeMatch
    {
        public void Enqueue(PrototypeUnit unit, QueuedOrder order)
        {
            if (unit.queued.Count == 0 && unit.Order == UnitOrder.Idle && !Busy(unit) && (!unit.Moving || unit.HarvestTarget != null)) { StartQueued(unit, order); return; }
            unit.queued.Add(order);
        }
        public bool EnqueueGroup(IReadOnlyList<PrototypeUnit> units, PrototypeUnit target)
        {
            bool any = false;
            foreach (var u in units) if (target.Alive && target.Owner != u.Owner) { Enqueue(u, new QueuedOrder(target)); any = true; }
            if (any) Message = "Атака цели добавлена в очередь.";
            return any;
        }
        public bool EnqueueGroup(IReadOnlyList<PrototypeUnit> units, PrototypeBuilding target)
        {
            bool any = false;
            foreach (var u in units) if (target.Health > 0 && target.Owner != u.Owner) { Enqueue(u, new QueuedOrder(target)); any = true; }
            if (any) Message = "Атака здания добавлена в очередь.";
            return any;
        }
        public bool QueueMoveGroup(IReadOnlyList<PrototypeUnit> units, Fix x, Fix z) => GroupOrder(units, x, z, false, true);
        public bool QueueAttackMoveGroup(IReadOnlyList<PrototypeUnit> units, Fix x, Fix z) => GroupOrder(units, x, z, true, true);

        bool StartQueued(PrototypeUnit u, QueuedOrder order)
        {
            if (order.BuildKind >= 0) return Build(u, (int)order.X, (int)order.Z, order.BuildKind);
            if (order.Mend) return order.Building != null ? Repair(u, order.Building) : Repair(u, order.Unit);
            if (order.Unit != null) return Attack(u, order.Unit);
            if (order.Building != null) return Attack(u, order.Building);
            return order.Order == UnitOrder.AttackMove ? AttackMove(u, order.X, order.Z) : Move(u, order.X, order.Z);
        }
        // Starts the next queued order of every unit whose current one is done; orders that can no longer run are skipped.
        void RunQueues()
        {
            foreach (var u in Units)
            {
                while (u.queued.Count > 0 && u.Order == UnitOrder.Idle && !u.Moving && !Busy(u))
                {
                    var next = u.queued[0]; u.queued.RemoveAt(0);
                    var rest = new List<QueuedOrder>(u.queued);
                    var message = Message;
                    bool started = StartQueued(u, next);
                    Message = message;
                    // A started order cleared the queue; put the rest back.
                    if (u.queued.Count == 0) u.queued.AddRange(rest);
                    if (started) break;
                }
            }
        }
    }
}
