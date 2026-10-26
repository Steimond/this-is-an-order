using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class CheatTests
    {
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}

        [Test] public void FastBuildSpeedsUpConstructionAndTrainingOfThePlayerOnly()
        {
            var match=new PrototypeMatch();match.AddEnemyBase();
            Assert.That(match.Cheated,Is.False);
            Assert.That(match.ToggleFastBuild(),Is.True);Assert.That(match.Cheated,Is.True);
            var node=Sites.Place(match,15,7,0);
            var hq=match.Buildings.Find(b=>b.Kind==2&&b.Owner==PrototypeMatch.Player);Assert.That(match.Train(hq,UnitKind.Worker),Is.True);
            Run(match,(float)match.Balance.Expedition.Buildings[0].BuildSeconds/(float)PrototypeMatch.FastFactor+.1f);
            Assert.That((float)node.Ready,Is.EqualTo(1));
            Run(match,(float)match.Balance.Expedition.Worker.TrainSeconds/(float)PrototypeMatch.FastFactor+.1f);
            Assert.That(match.Units.FindAll(u=>u.Kind==UnitKind.Worker).Count,Is.EqualTo(2));
            Assert.That(match.ToggleFastBuild(),Is.False);
        }
        [Test] public void NoSupplyTrainsPastTheLimit()
        {
            var match=new PrototypeMatch();match.AddTestSquad();var hq=match.Buildings[0];
            Assert.That(match.SupplyUsed,Is.EqualTo(match.SupplyLimit));
            match.ToggleNoSupply();match.Train(hq,UnitKind.Worker);
            Run(match,(float)match.Balance.Expedition.Worker.TrainSeconds+.1f);
            Assert.That(hq.SupplyBlocked,Is.False);Assert.That(match.SupplyUsed,Is.GreaterThan(match.SupplyLimit));
        }
        [Test] public void RevealShowsTheWholeMapAndInvulnerabilityStopsDamage()
        {
            var match=new PrototypeMatch();match.AddEnemySquad();
            var enemy=match.Units.Find(u=>u.Owner==PrototypeMatch.Enemy);
            Assert.That(match.SeenBy(PrototypeMatch.Player,enemy),Is.False);
            match.ToggleReveal();
            Assert.That(match.SeenBy(PrototypeMatch.Player,enemy),Is.True);Assert.That(match.Explored[63,63],Is.True);
            match.ToggleInvulnerable();
            var bait=match.SpawnSquad(UnitKind.Ranged,1,enemy.X,enemy.Z-3)[0];
            Run(match,3);
            Assert.That(bait.Health,Is.EqualTo(bait.Stats.Health));
            match.ToggleReveal();Assert.That(match.Visible[63,63],Is.False,"Turning the cheat off brings the fog back");
        }
        [Test] public void SpawnedSquadStandsOnFreeGroundAndWinNowEndsTheMatch()
        {
            var match=new PrototypeMatch();match.AddEnemyBase();
            var squad=match.SpawnSquad(UnitKind.Melee,12,30,30);var enemies=match.SpawnSquad(UnitKind.Air,3,40,40,PrototypeMatch.Enemy);
            Assert.That(squad.Count,Is.EqualTo(12));Assert.That(enemies.Count,Is.EqualTo(3));Assert.That(enemies[0].Owner,Is.EqualTo(PrototypeMatch.Enemy));
            for(int i=0;i<squad.Count;i++)
            {
                Assert.That(match.CanTravel((float)squad[i].X,(float)squad[i].Z,(float)squad[i].X,(float)squad[i].Z,(float)squad[i].Stats.Radius),Is.True);
                for(int j=i+1;j<squad.Count;j++)
                    Assert.That(System.Math.Sqrt(((float)squad[i].X-(float)squad[j].X)*((float)squad[i].X-(float)squad[j].X)+((float)squad[i].Z-(float)squad[j].Z)*((float)squad[i].Z-(float)squad[j].Z)),Is.GreaterThan((float)squad[i].Stats.Radius*1.9f));
            }
            match.WinNow();Run(match,.1f);
            Assert.That(match.Winner,Is.EqualTo(PrototypeMatch.Player));Assert.That(match.Buildings.Exists(b=>b.Owner!=PrototypeMatch.Player),Is.False);
        }
    }
}
