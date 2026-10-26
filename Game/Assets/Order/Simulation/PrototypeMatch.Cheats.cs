using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // Test-ground cheats in the spirit of StarCraft's, to check things without waiting for resources and production. They
    // help the player's side only; Cheated stays set once any of them was used in the match.
    public sealed partial class PrototypeMatch
    {
        // Construction and training run this many times faster with FastBuild (StarCraft's "operation cwal").
        public static readonly Fix FastFactor = 10;
        public bool FastBuild { get; private set; }
        public bool NoSupply { get; private set; }
        public bool Revealed { get; private set; }
        public bool Invulnerable { get; private set; }
        public bool Cheated { get; private set; }

        public void AddResources(int metal, int plasma) { Metal += metal; Plasma += plasma; Cheated = true; Message = $"Чит: +{metal} металла, +{plasma} плазмы."; }
        public bool ToggleFastBuild() { FastBuild = !FastBuild; return Cheat(FastBuild, $"стройка и обучение ×{FastFactor:0}"); }
        public bool ToggleNoSupply() { NoSupply = !NoSupply; return Cheat(NoSupply, "без лимита снабжения"); }
        public bool ToggleReveal() { Revealed = !Revealed; UpdateVision(); return Cheat(Revealed, "вся карта открыта"); }
        public bool ToggleInvulnerable() { Invulnerable = !Invulnerable; return Cheat(Invulnerable, "бессмертие"); }
        bool Cheat(bool on, string what) { Cheated = true; Message = $"Чит {(on ? "включён" : "выключен")}: {what}."; return on; }
        // StarCraft's "there is no cow level": the match is won at once.
        public void WinNow()
        {
            Cheated = true;
            foreach (var b in Buildings) if (b.Owner != Player) b.Health = 0;
            if (!Buildings.Exists(b => b.Owner != Player)) { Winner = Player; Message = "Победа: все здания противника уничтожены."; }
        }
        // Cheat: new units of either side on free ground around a point, nearest first.
        public List<PrototypeUnit> SpawnSquad(UnitKind kind, int count, Fix x, Fix z, int owner = Player)
        {
            Cheated = true;
            var spawned = SpawnAround(kind, count, x, z, owner);
            Message = spawned.Count > 0 ? $"Чит: {Roster(owner).Unit(kind).Name} ×{spawned.Count}{(owner == Player ? "" : " у противника")}." : "Нет места для юнитов.";
            return spawned;
        }
        // Units on free ground around a point, nearest first: for cheats and for the bot's starting workers.
        List<PrototypeUnit> SpawnAround(UnitKind kind, int count, Fix x, Fix z, int owner)
        {
            var spawned = new List<PrototypeUnit>();
            var stats = Roster(owner).Unit(kind);
            Fix spacing = stats.Radius * (Fix)2.1f;
            for (int ring = 0; ring < 30 && spawned.Count < count; ring++)
            {
                int around = ring == 0 ? 1 : (int)(2 * Math.PI * ring);
                for (int i = 0; i < around && spawned.Count < count; i++)
                {
                    Fix a = 2 * Math.PI * i / around;
                    Fix px = x + Math.Cos(a) * ring * spacing, pz = z + Math.Sin(a) * ring * spacing;
                    if (!InMap(px, pz) || px < stats.Radius || pz < stats.Radius || px > Size - stats.Radius || pz > Size - stats.Radius) continue;
                    if (!SpawnRoom(stats, px, pz)) continue;
                    spawned.Add(Spawn(kind, px, pz, owner));
                }
            }
            UpdateVision();
            return spawned;
        }
        // The visual lab's cheats (VisualLab): a building of either side set down at once, finished or at a stage of its
        // construction, where a machine site rises by itself as one a worker melted into; and a network that reaches
        // everywhere, so the lab's machine buildings never go dark.
        public bool WholeNetwork { get; private set; }
        public void ReachEverywhere() { WholeNetwork = true; Cheated = true; }
        public PrototypeBuilding PlaceBuilding(int kind, int x, int z, int owner, Fix ready)
        {
            var building = AddBuilding(kind, x, z, owner, Math.Min(1, ready));
            building.SelfBuilt = ready < 1 && Roster(owner).SelfBuilding;
            Cheated = true; UpdateVision();
            return building;
        }
        bool SpawnRoom(UnitStats stats, Fix x, Fix z)
        {
            if (!stats.Flying && !CanTravel(x, z, x, z, stats.Radius)) return false;
            foreach (var v in Units)
            {
                if (v.Stats.Flying != stats.Flying) continue;
                Fix reach = (stats.Radius + v.Stats.Radius) * (Fix).95f, dx = v.X - x, dz = v.Z - z;
                if (dx * dx + dz * dz < reach * reach) return false;
            }
            return true;
        }
    }
}
