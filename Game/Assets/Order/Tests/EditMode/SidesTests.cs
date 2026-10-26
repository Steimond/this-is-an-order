using System.Collections.Generic;
using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // Matches of more than two sides: every side has its own resources, supply cap, vision and bot, and the last side
    // standing wins.
    public sealed class SidesTests
    {
        static readonly FactionId[] Four = { FactionId.Expedition, FactionId.Resonance, FactionId.Expedition, FactionId.Resonance };
        static void Run(PrototypeMatch match, float seconds) { for (float t = 0; t < seconds - 1e-4f; t += .05f) match.Advance(); }
        // Four sides on the four start plateaus, each played by a bot.
        static PrototypeMatch FreeForAll()
        {
            var match = new PrototypeMatch(null, Four);
            for (int side = 0; side < 4; side++) match.AddStart(side, side);
            for (int side = 0; side < 4; side++) match.StartBot(side);
            match.UpdateVision();
            return match;
        }

        [Test] public void AMatchHasTwoToEightSides()
        {
            Assert.That(() => new PrototypeMatch(null, new[] { FactionId.Expedition }), Throws.ArgumentException);
            var nine = new FactionId[PrototypeMatch.MaxSides + 1];
            Assert.That(() => new PrototypeMatch(null, nine), Throws.ArgumentException);
            var eight = new PrototypeMatch(null, new FactionId[PrototypeMatch.MaxSides]);
            Assert.That(eight.Sides, Is.EqualTo(8));
            for (int side = 0; side < 8; side++) Assert.That(eight.MetalOf(side), Is.EqualTo(eight.Balance.StartingMetal));
        }
        [Test] public void EverySeatGetsABaseAndWorkers()
        {
            var match = new PrototypeMatch(null, Four);
            for (int side = 0; side < 4; side++)
            {
                var main = match.AddStart(side, side);
                Assert.That((main.X, main.Z), Is.EqualTo(match.Map.Bases[side]));
                Assert.That(match.Units.FindAll(u => u.Owner == side && u.Kind == UnitKind.Worker).Count, Is.EqualTo(4));
            }
        }
        [Test] public void FourBotsBuildMineAndFightEachOther()
        {
            var match = FreeForAll(); var hurt = new HashSet<(int, int)>();
            match.Attacked += (attacker, target) => hurt.Add((attacker.Owner, target.Owner));
            match.AttackedBuilding += (attacker, target) => hurt.Add((attacker.Owner, target.Owner));
            Run(match, 420);
            for (int side = 0; side < 4; side++)
            {
                Assert.That(match.Buildings.FindAll(b => b.Owner == side).Count, Is.GreaterThan(1), $"Side {side} builds");
                Assert.That(match.Units.Exists(u => u.Owner == side && u.Stats.Damage > 0) || match.WavesOf(side) > 0, Is.True, $"Side {side} trains an army");
            }
            bool among = false; foreach (var (attacker, target) in hurt) among |= attacker != 0 && target != 0;
            Assert.That(among, Is.True, "Bots fight each other, not only the player's side");
            foreach (var (attacker, target) in hurt) Assert.That(attacker, Is.Not.EqualTo(target), "Nobody strikes its own side");
        }
        [Test] public void TwinFreeForAllsStayEqual()
        {
            var a = FreeForAll(); var b = FreeForAll();
            for (int frame = 0; frame < 2400; frame++)
            {
                a.Advance(); b.Advance();
                Assert.That(b.StateHash(), Is.EqualTo(a.StateHash()), $"The twins part on frame {frame + 1}");
            }
        }
        [Test] public void TheLastSideStandingWins()
        {
            var match = new PrototypeMatch(null, new[] { FactionId.Expedition, FactionId.Expedition, FactionId.Resonance });
            var mains = new List<PrototypeBuilding>();
            for (int side = 0; side < 3; side++) mains.Add(match.AddStart(side, side, 0));
            mains[1].Health = 0; match.Tick(.05f);
            Assert.That(match.Defeated(1), Is.True); Assert.That(match.Winner, Is.EqualTo(-1), "Two sides still fight");
            mains[2].Health = 0; match.Tick(.05f);
            Assert.That(match.Defeated(2), Is.True); Assert.That(match.Winner, Is.EqualTo(0));
            Assert.That(match.Defeated(0), Is.False);
        }
        [Test] public void TheScenarioCapsSupplyForEachSide()
        {
            var match = new PrototypeMatch(null, Four); var main = match.AddStart(0, 0, 0); match.AddStart(1, 1, 0);
            Assert.That(match.SupplyLimit, Is.EqualTo(main.Stats.Supply));
            match.SetSupplyCap(PrototypeMatch.Player, 1); match.Spawn(UnitKind.Worker, 14.5f, 14.5f);
            Assert.That(match.SupplyLimit, Is.EqualTo(1), "The cap wins over what the buildings give");
            Assert.That(match.SupplyCapOf(1), Is.EqualTo(match.Balance.SupplyCap), "Other sides keep the scenario's cap");
            Assert.That(match.Train(main, UnitKind.Worker), Is.True); Run(match, 20);
            Assert.That(match.Units.FindAll(u => u.Owner == 0).Count, Is.EqualTo(1), "Training waits for supply under the cap");
        }
        [Test] public void EachSideSeesOnlyWithItsOwnEyes()
        {
            var match = new PrototypeMatch(null, Four);
            var scout = match.Spawn(UnitKind.Ranged, 32, 32, 2); var near = match.Spawn(UnitKind.Ranged, 34, 32, 3); var far = match.Spawn(UnitKind.Ranged, 5, 58, 1);
            match.UpdateVision();
            Assert.That(match.SeenBy(3, scout), Is.True, "Side 3 stands next to it");
            Assert.That(match.SeenBy(1, scout), Is.False, "Side 1 is across the map");
            Assert.That(match.SeenBy(2, far), Is.False);
            Assert.That(match.ExploredBy(3)[32, 32], Is.True); Assert.That(match.ExploredBy(1)[32, 32], Is.False);
        }
    }
}
