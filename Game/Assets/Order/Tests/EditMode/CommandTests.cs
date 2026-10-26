using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // Orders from people reach the match only as commands: logged, so a fresh match fed the log plays the same match, and
    // checked, so a side orders nothing but its own.
    public sealed class CommandTests
    {
        const int Player = PrototypeMatch.Player, Enemy = PrototypeMatch.Enemy;
        static PrototypeMatch Game()
        {
            var match = new PrototypeMatch(null, FactionId.Expedition, FactionId.Resonance);
            match.AddTestSquad(); match.AddEnemySquad(); match.AddEnemyBase(); match.StartBot();
            return match;
        }
        // A player that trains, builds a supply node, sends its squad forward and later at the enemy base.
        static void Play(PrototypeMatch match)
        {
            var army = match.Units.FindAll(u => u.Owner == Player && u.Kind != UnitKind.Worker);
            var main = match.Buildings.Find(b => b.Owner == Player && b.Kind == FactionBalance.Main);
            var worker = match.Units.Find(u => u.Owner == Player && u.Kind == UnitKind.Worker);
            if (match.Frame == 5) match.Execute(MatchCommand.Train(Player, main, UnitKind.Worker));
            if (match.Frame == 10 && worker != null) match.Execute(MatchCommand.Build(Player, worker, 15, 14, 0));
            if (match.Frame == 40) match.Execute(MatchCommand.AttackMove(Player, army, 34, 41));
            if (match.Frame == 300) match.Execute(MatchCommand.Hold(Player, army.GetRange(0, System.Math.Min(2, army.Count))));
            if (match.Frame == 900) match.Execute(MatchCommand.AttackMove(Player, army, 50, 50, true));
        }

        [Test] public void AFreshMatchFedTheLogPlaysTheSameMatch()
        {
            const int frames = 2400;
            var played = Game(); var hashes = new ulong[frames];
            for (int i = 0; i < frames; i++) { Play(played); played.Advance(); hashes[i] = played.StateHash(); }
            Assert.That(played.Log.Count, Is.GreaterThanOrEqualTo(5));
            var replayed = Game();
            replayed.Replay(played.Log, frames);
            Assert.That(replayed.StateHash(), Is.EqualTo(hashes[frames - 1]));
            Assert.That(replayed.Log.Count, Is.EqualTo(played.Log.Count));
        }
        [Test] public void ASideOrdersOnlyItsOwn()
        {
            var match = Game();
            var enemy = match.Units.FindAll(u => u.Owner == Enemy);
            var before = (enemy[0].X, enemy[0].Z);
            Assert.That(match.Execute(MatchCommand.Move(Player, enemy, 20, 20)), Is.False, "The player cannot move the enemy's units");
            var factory = match.Buildings.Find(b => b.Owner == Enemy && b.Kind == 1);
            int queue = factory.Queue.Count;
            Assert.That(match.Execute(MatchCommand.Train(Player, factory, UnitKind.Melee)), Is.False);
            Assert.That(factory.Queue.Count, Is.EqualTo(queue));
            match.Advance();
            Assert.That(enemy[0].Moving, Is.False); Assert.That((enemy[0].X, enemy[0].Z), Is.EqualTo(before));
        }
        [Test] public void CommandsNamingThingsGoneDoNothing()
        {
            var match = Game();
            var squad = match.Units.FindAll(u => u.Owner == Player && u.Stats.Damage > 0);
            var target = match.Units.Find(u => u.Owner == Enemy);
            var order = MatchCommand.Attack(Player, squad, target);
            target.Health = 0; match.Advance();
            Assert.That(match.Execute(order), Is.False);
            Assert.That(match.Execute(new MatchCommand(CommandKind.Move, 9, new[] { squad[0].Id }, 0, 20, 20)), Is.False, "No such side");
        }
        [Test] public void CommandsWaitForTheStepToEnd()
        {
            var match = Game(); Exception thrown = null;
            var squad = match.Units.FindAll(u => u.Owner == Player && u.Stats.Damage > 0);
            match.Attacked += (a, t) => { try { match.Execute(MatchCommand.Stop(Player, squad)); } catch (InvalidOperationException e) { thrown = e; } };
            match.Execute(MatchCommand.Cheat(CommandKind.CheatReveal, Player));
            Assert.That(match.Execute(MatchCommand.Attack(Player, squad, match.Units.Find(u => u.Owner == Enemy))), Is.True);
            for (int i = 0; i < 600 && thrown == null; i++) match.Advance();
            Assert.That(thrown, Is.Not.Null, "A command from inside a step is refused");
        }
        [Test] public void BuildingsHaveIds()
        {
            var match = Game(); var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var b in match.Buildings) { Assert.That(b.Id, Is.GreaterThan(0)); Assert.That(seen.Add(b.Id), Is.True, "Ids are unique"); Assert.That(match.BuildingById(b.Id), Is.SameAs(b)); }
        }

        // The view gives orders only as commands: none of the match's order methods may be called from it. The visual lab
        // only lays out its scene before the first step.
        static readonly Regex Direct = new Regex(@"\b(?:[Mm]atch|m)\.(?:MoveGroup|AttackMoveGroup|QueueMoveGroup|QueueAttackMoveGroup|AttackGroup|EnqueueGroup|Move|AttackMove|Attack|Stop|Hold|Build|QueueBuild|Resume|CancelConstruction|Train|CancelTraining|SetRally|Research|CancelResearch|Repair|QueueRepair|SetAutoRepair|ToggleDeploy|Disassemble|AddResources|Toggle\w*|WinNow|SendWaveNow|SpawnSquad)\(");
        [Test] public void TheViewOrdersOnlyThroughCommands()
        {
            var folder = Path.Combine(UnityEngine.Application.dataPath, "Order", "Presentation");
            foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "VisualLab.cs") continue;
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    Assert.That(Direct.IsMatch(lines[i]), Is.False, $"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }
    }
}
