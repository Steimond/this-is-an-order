using System.Collections.Generic;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Presentation
{
    // The stress battle (StressLab, the simulation benchmark): up to eight sides on the eight base spots of Temple Basin —
    // the four plateaus, then the four expansions in the basin — humans and machines in turn. Each side gets a main
    // building and an army of all its fighting units in equal shares (but the leaper: legacy, outside the roster),
    // spawned round the building, and every army attack-moves to the middle of the map, where they all meet. No bots and
    // no economy: only movement and combat load.
    public static class StressBattle
    {
        public const int MaxSides = 8;
        public static PrototypeMatch Create(MatchBalance balance, int units, int sides = MaxSides)
        {
            sides = System.Math.Max(2, System.Math.Min(MaxSides, sides));
            var factions = new FactionId[sides];
            for (int side = 0; side < sides; side++) factions[side] = side % 2 == 0 ? FactionId.Expedition : FactionId.Resonance;
            var match = new PrototypeMatch(balance, factions);
            match.Execute(MatchCommand.Cheat(CommandKind.CheatReveal, PrototypeMatch.Player));
            for (int side = 0; side < sides; side++)
            {
                var main = match.AddStart(side, side, 0);
                var kinds = new List<UnitKind>();
                foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
                    if (kind != UnitKind.Leaper && match.Roster(side).Has(kind) && match.Roster(side).Unit(kind).Damage > 0) kinds.Add(kind);
                int count = units / sides + (side < units % sides ? 1 : 0), first = match.Units.Count;
                float x = main.X + main.Width * .5f, z = main.Z + main.Width * .5f;
                for (int k = 0; k < kinds.Count; k++)
                {
                    int share = count / kinds.Count + (k < count % kinds.Count ? 1 : 0);
                    if (share > 0) match.Execute(MatchCommand.Spawn(PrototypeMatch.Player, kinds[k], share, x, z, side));
                }
                // New units join the end of the match's list.
                var army = match.Units.GetRange(first, match.Units.Count - first);
                if (army.Count > 0) match.Execute(MatchCommand.AttackMove(side, army, 32, 32));
            }
            match.UpdateVision();
            return match;
        }
    }
}
