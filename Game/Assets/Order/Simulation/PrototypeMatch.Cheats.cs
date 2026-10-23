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
        public const float FastFactor = 10;
        public bool FastBuild { get; private set; }
        public bool NoSupply { get; private set; }
        public bool Revealed { get; private set; }
        public bool Invulnerable { get; private set; }
        public bool Cheated { get; private set; }

        public void AddResources(int minerals, int gas) { Minerals += minerals; Gas += gas; Cheated = true; Message = $"Чит: +{minerals} минералов, +{gas} газа."; }
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
        // New units of either side on free ground around a point, nearest first.
        public List<PrototypeUnit> SpawnSquad(UnitKind kind, int count, float x, float z, int owner = Player)
        {
            Cheated = true;
            var spawned = new List<PrototypeUnit>();
            var stats = Balance.Unit(kind);
            float spacing = stats.Radius * 2.1f;
            for (int ring = 0; ring < 30 && spawned.Count < count; ring++)
            {
                int around = ring == 0 ? 1 : (int)(2 * Math.PI * ring);
                for (int i = 0; i < around && spawned.Count < count; i++)
                {
                    double a = 2 * Math.PI * i / around;
                    float px = x + (float)Math.Cos(a) * ring * spacing, pz = z + (float)Math.Sin(a) * ring * spacing;
                    if (!InMap(px, pz) || px < stats.Radius || pz < stats.Radius || px > 64 - stats.Radius || pz > 64 - stats.Radius) continue;
                    if (!SpawnRoom(stats, px, pz)) continue;
                    spawned.Add(Spawn(kind, px, pz, owner));
                }
            }
            UpdateVision();
            Message = spawned.Count > 0 ? $"Чит: {stats.Name} ×{spawned.Count}{(owner == Player ? "" : " у противника")}." : "Нет места для юнитов.";
            return spawned;
        }
        bool SpawnRoom(UnitStats stats, float x, float z)
        {
            if (!stats.Flying && !CanTravel(x, z, x, z, stats.Radius)) return false;
            foreach (var v in Units)
            {
                if (v.Stats.Flying != stats.Flying) continue;
                float reach = (stats.Radius + v.Stats.Radius) * .95f, dx = v.X - x, dz = v.Z - z;
                if (dx * dx + dz * dz < reach * reach) return false;
            }
            return true;
        }
    }
}
