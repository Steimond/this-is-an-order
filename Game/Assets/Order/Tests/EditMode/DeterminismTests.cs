using NUnit.Framework;
using Order.Contracts;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // Lockstep needs the match to be a pure function of its orders: twin runs fed the same orders on the same frames stay
    // equal bit for bit, frame after frame.
    public sealed class DeterminismTests
    {
        const int Player = PrototypeMatch.Player, Enemy = PrototypeMatch.Enemy;
        // The test ground as the game lays it out, with the bot playing the enemy, and a scripted player: it trains units,
        // sends its squad across the map on an attack-move and later throws everything at the enemy base.
        static PrototypeMatch Game(FactionId player, FactionId enemy)
        {
            var match = new PrototypeMatch(null, player, enemy);
            match.AddTestSquad(); match.AddEnemySquad(); match.AddEnemyBase(); match.StartBot();
            return match;
        }
        static void Orders(PrototypeMatch match)
        {
            var army = match.Units.FindAll(u => u.Owner == Player && u.Kind != UnitKind.Worker);
            if (match.Frame == 10) { var main = match.Buildings.Find(b => b.Owner == Player); match.Train(main, UnitKind.Worker); }
            if (match.Frame == 40) match.AttackMoveGroup(army, 34, 41);
            if (match.Frame == 900) match.AttackMoveGroup(army, 50, 50);
        }
        static ulong[] Play(FactionId player, FactionId enemy, int frames)
        {
            var match = Game(player, enemy); var hashes = new ulong[frames];
            for (int i = 0; i < frames; i++) { Orders(match); match.Advance(); hashes[i] = match.StateHash(); }
            return hashes;
        }

        [TestCase(FactionId.Expedition, FactionId.Expedition)]
        [TestCase(FactionId.Expedition, FactionId.Resonance)]
        [TestCase(FactionId.Resonance, FactionId.Expedition)]
        public void TwinMatchesStayEqualOnEveryFrame(FactionId player, FactionId enemy)
        {
            // Two minutes of play: the bot builds and raids, the squads fight.
            const int frames = 2400;
            var a = Play(player, enemy, frames); var b = Play(player, enemy, frames);
            for (int i = 0; i < frames; i++) Assert.That(b[i], Is.EqualTo(a[i]), $"The twins part on frame {i + 1}");
        }
        [Test] public void FramesCountFixedSteps()
        {
            var match = Game(FactionId.Expedition, FactionId.Expedition);
            for (int i = 0; i < 20; i++) match.Advance();
            Assert.That(match.Frame, Is.EqualTo(20));
            Assert.That(match.Clock, Is.EqualTo(20 * PrototypeMatch.TickSeconds).Within(1e-4f));
        }
        [Test] public void HashSeesAMove()
        {
            var a = Game(FactionId.Expedition, FactionId.Expedition); var b = Game(FactionId.Expedition, FactionId.Expedition);
            Assert.That(b.StateHash(), Is.EqualTo(a.StateHash()));
            var unit = b.Units.Find(u => u.Owner == Player && u.Kind == UnitKind.Ranged);
            b.Move(unit, 20f, 20f); a.Advance(); b.Advance();
            Assert.That(b.StateHash(), Is.Not.EqualTo(a.StateHash()), "One order to one unit changes the state");
        }
        [Test] public void UnitsStartWhereTheyStand()
        {
            var match = Game(FactionId.Expedition, FactionId.Expedition);
            var unit = match.Spawn(UnitKind.Ranged, 12.25f, 17.5f);
            Assert.That((unit.PreviousX, unit.PreviousZ), Is.EqualTo((12.25f, 17.5f)), "A new unit is not drawn flying in from the map corner");
        }
    }
}
