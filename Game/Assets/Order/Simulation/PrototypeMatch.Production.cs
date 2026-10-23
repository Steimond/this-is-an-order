using System;
using Order.Content;

namespace Order.Simulation
{
    // Production in the spirit of StarCraft:
    // - a finished building trains one unit at a time from a queue of up to five;
    // - the cost is paid when a unit is queued and refunded in full when it is cancelled;
    // - supply is taken when training starts; while supply is short the building waits;
    // - a new unit appears at the building edge nearest the rally point and goes there; a worker rallied to a deposit
    //   starts harvesting it.
    public sealed partial class PrototypeMatch
    {
        public const int QueueSize = 5;

        PrototypeBuilding AddBuilding(int kind, int x, int z, int owner, float ready)
        {
            var stats = Roster(owner).Buildings[kind];
            var building = new PrototypeBuilding { X = x, Z = z, Width = stats.Width, Kind = kind, Owner = owner, Stats = stats, Ready = ready, Health = ready >= 1 ? stats.Health : Math.Max(1, stats.Health / 10) };
            Buildings.Add(building);
            NavigationChanged(x, z, stats.Width);
            return building;
        }
        public bool Trains(PrototypeBuilding b, UnitKind kind) => Array.IndexOf(b.Stats.Trains, kind) >= 0;

        public bool Train(PrototypeBuilding b, UnitKind kind)
        {
            var stats = Roster(b.Owner).Unit(kind);
            string reason = b.Ready < 1 ? "Здание ещё строится." : !b.Powered ? "Нет сигнала сети." : !Trains(b, kind) ? $"{b.Stats.Name} не обучает: {stats.Name}."
                : b.queue.Count >= QueueSize ? "Очередь заполнена." : metalStock[b.Owner] < stats.Metal ? "Недостаточно металла." : plasmaStock[b.Owner] < stats.Plasma ? "Недостаточно плазмы." : null;
            if (reason != null) { if (b.Owner == Player) Message = reason; return false; }
            metalStock[b.Owner] -= stats.Metal; plasmaStock[b.Owner] -= stats.Plasma;
            b.queue.Add(kind);
            if (b.Owner == Player) Message = $"Обучение: {stats.Name}.";
            return true;
        }
        // Cancels a queued unit and returns its cost; the unit in training gives its supply back too.
        public bool CancelTraining(PrototypeBuilding b, int index)
        {
            if (index < 0 || index >= b.queue.Count) return false;
            var stats = Roster(b.Owner).Unit(b.queue[index]);
            metalStock[b.Owner] += stats.Metal; plasmaStock[b.Owner] += stats.Plasma;
            b.queue.RemoveAt(index);
            if (index == 0) { b.started = false; b.Training = 0; b.SupplyBlocked = false; }
            if (b.Owner == Player) Message = $"Отменено: {stats.Name}.";
            return true;
        }
        public void SetRally(PrototypeBuilding b, float x, float z)
        {
            b.HasRally = InMap(x, z); b.RallyX = x; b.RallyZ = z;
            if (b.Owner == Player && b.HasRally) Message = "Точка сбора задана.";
        }

        void TickProduction(float dt)
        {
            foreach (var b in Buildings)
            {
                if (b.Ready < 1 || b.queue.Count == 0 || !b.Powered) continue;
                var stats = Roster(b.Owner).Unit(b.queue[0]);
                if (!b.started)
                {
                    bool blocked = !(NoSupply && b.Owner == Player) && Supply(b.Owner) + stats.Supply > Limit(b.Owner);
                    if (blocked && !b.SupplyBlocked && b.Owner == Player) Message = "Не хватает снабжения: постройте узел снабжения.";
                    b.SupplyBlocked = blocked;
                    if (blocked) continue;
                    b.started = true;
                }
                b.Training += dt * (FastBuild && b.Owner == Player ? FastFactor : 1);
                if (b.Training < stats.TrainSeconds) continue;
                if (!ExitSpot(b, stats, out float x, out float z)) { b.Training = stats.TrainSeconds; continue; }
                var kind = b.queue[0];
                b.queue.RemoveAt(0); b.started = false; b.Training = 0;
                var unit = Spawn(kind, x, z, b.Owner);
                var before = Message;
                if (b.HasRally)
                {
                    int cx = (int)b.RallyX, cz = (int)b.RallyZ;
                    var deposit = b.Owner != Player || Explored[cx, cz] ? Map.ResourceAt(cx, cz) : null;
                    if (deposit != null) Move(unit, deposit.X + .5f, deposit.Z + .5f); else Move(unit, b.RallyX, b.RallyZ);
                }
                // Only the player hears about its own new units.
                Message = b.Owner == Player ? $"Готов: {stats.Name}." : before;
            }
        }
        // Free spot next to the footprint, on the building's level, nearest the rally point (by default the south-east
        // corner); air units may appear over anything.
        bool ExitSpot(PrototypeBuilding b, UnitStats stats, out float x, out float z)
        {
            float tx = b.HasRally ? b.RallyX : b.X + b.Width + 1, tz = b.HasRally ? b.RallyZ : b.Z - 1, best = float.MaxValue;
            float level = Map.Height(b.X, b.Z);
            x = z = 0;
            for (int cz = b.Z - 1; cz <= b.Z + b.Width; cz++)
            for (int cx = b.X - 1; cx <= b.X + b.Width; cx++)
            {
                if (cx >= b.X && cx < b.X + b.Width && cz >= b.Z && cz < b.Z + b.Width) continue;
                float px = cx + .5f, pz = cz + .5f;
                if (!Map.Inside(cx, cz)) continue;
                if (!stats.Flying && (Occupied(cx, cz) || Map.Ramp(cx, cz) || Math.Abs(Map.Height(cx, cz) - level) > .01f || !CanTravel(px, pz, px, pz, stats.Radius))) continue;
                float d = (px - tx) * (px - tx) + (pz - tz) * (pz - tz);
                if (d < best) { best = d; x = px; z = pz; }
            }
            return best < float.MaxValue;
        }
    }
}
