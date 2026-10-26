using System;
using Order.Content;

namespace Order.Simulation
{
    // Weapon and armour upgrades, three levels each for every side, as in StarCraft (SETTING.md: the humans' workshop,
    // the machines' firmware for the whole network). The research building of the side researches one level at a time;
    // the cost is paid when it starts and returned in full when it is cancelled. A weapon level adds a tenth of a unit's
    // base damage (at least one) to every blow; an armour level takes one more point off every blow the unit receives,
    // down to one. Buildings get no armour from it.
    public enum Upgrade { Weapons, Armor }
    public sealed partial class PrototypeMatch
    {
        public const int MaxUpgrade = 3;
        readonly int[,] levels = new int[2, 2];
        public int Level(int side, Upgrade upgrade) => levels[side, (int)upgrade];
        // What the next level costs: level 1 — 100 metal and 100 plasma, 60 s; each further level 75 and 15 s more.
        public static (int metal, int plasma, Fix seconds) UpgradeCost(int level) => (25 + 75 * level, 25 + 75 * level, 45 + 15 * level);
        public static string UpgradeName(Upgrade upgrade) => upgrade == Upgrade.Weapons ? "Оружие" : "Броня";

        // Why the building cannot start this research now, or null.
        public string ResearchBlocked(PrototypeBuilding b, Upgrade upgrade)
        {
            if (!b.Stats.Researches) return $"{b.Stats.Name} не ведёт исследований.";
            if (b.Ready < 1) return "Здание ещё строится.";
            if (!b.Powered) return "Нет сигнала сети.";
            if (b.research.HasValue) return "Уже идёт исследование.";
            int next = Level(b.Owner, upgrade) + 1;
            if (next > MaxUpgrade) return $"{UpgradeName(upgrade)}: высший уровень.";
            foreach (var other in Buildings) if (other.Owner == b.Owner && other.research == upgrade) return $"{UpgradeName(upgrade)} уже исследуется.";
            var (metal, plasma, _) = UpgradeCost(next);
            return metalStock[b.Owner] < metal ? "Недостаточно металла." : plasmaStock[b.Owner] < plasma ? "Недостаточно плазмы." : null;
        }
        public bool Research(PrototypeBuilding b, Upgrade upgrade)
        {
            var reason = ResearchBlocked(b, upgrade);
            if (reason != null) { if (b.Owner == Player) Message = reason; return false; }
            int next = Level(b.Owner, upgrade) + 1;
            var (metal, plasma, _) = UpgradeCost(next);
            metalStock[b.Owner] -= metal; plasmaStock[b.Owner] -= plasma;
            b.research = upgrade; b.ResearchTime = 0;
            if (b.Owner == Player) Message = $"Исследование: {UpgradeName(upgrade)} {next}.";
            return true;
        }
        public bool CancelResearch(PrototypeBuilding b)
        {
            if (!b.research.HasValue) return false;
            var upgrade = b.research.Value; var (metal, plasma, _) = UpgradeCost(Level(b.Owner, upgrade) + 1);
            metalStock[b.Owner] += metal; plasmaStock[b.Owner] += plasma;
            b.research = null; b.ResearchTime = 0;
            if (b.Owner == Player) Message = $"Исследование отменено: {UpgradeName(upgrade)}.";
            return true;
        }
        void TickResearch(Fix dt)
        {
            foreach (var b in Buildings)
            {
                if (!b.research.HasValue || b.Ready < 1 || !b.Powered) continue;
                var upgrade = b.research.Value; int next = Level(b.Owner, upgrade) + 1;
                b.ResearchTime += dt * (FastBuild && b.Owner == Player ? FastFactor : 1);
                if (b.ResearchTime < UpgradeCost(next).seconds) continue;
                levels[b.Owner, (int)upgrade] = next; b.research = null; b.ResearchTime = 0;
                if (b.Owner == Player) Message = $"Исследовано: {UpgradeName(upgrade)} {next}.";
            }
        }
        // A blow with the attacker's weapon level, less the target's armour.
        public int Damage(PrototypeUnit attacker) => attacker.Stats.Damage + Level(attacker.Owner, Upgrade.Weapons) * Math.Max(1, (int)Math.Round(attacker.Stats.Damage * (Fix).1f));
        public int Armor(PrototypeUnit unit) => unit.Stats.Armor + Level(unit.Owner, Upgrade.Armor);
        int Damage(PrototypeUnit attacker, PrototypeUnit target) => Math.Max(1, Strike(attacker, target) - Armor(target));
        // The machines' emitter holds its beam on one target: the blow grows from the weapon's damage to Ramp times it over
        // RampSeconds; a new target, or a pause longer than a shot and FocusPause, starts it over.
        public static readonly Fix FocusPause = (Fix).6f;
        int Strike(PrototypeUnit attacker, object target)
        {
            int damage = Damage(attacker);
            var stats = attacker.Stats;
            if (stats.Ramp <= 1) return damage;
            if (attacker.focus != target || Clock - attacker.lastFocusHit > stats.AttackCooldown + FocusPause) { attacker.focus = target; attacker.focusStart = Clock; }
            attacker.lastFocusHit = Clock;
            return (int)Math.Round(damage * (1 + (stats.Ramp - 1) * Focus(attacker)));
        }
        // How far the beam has built up on its target, 0 to 1; 0 once it has paused.
        public Fix Focus(PrototypeUnit u)
        {
            if (u.Stats.Ramp <= 1 || u.focus == null || Clock - u.lastFocusHit > u.Stats.AttackCooldown + FocusPause) return 0;
            return Math.Min(1, (Clock - u.focusStart) / Math.Max((Fix).01f, u.Stats.RampSeconds));
        }
    }
}
