using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class ProductionTests
    {
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeBuilding Headquarters(PrototypeMatch match)=>match.Buildings.Find(b=>b.Kind==2);
        static PrototypeBuilding Factory(PrototypeMatch match)=>Sites.Place(match,14,15,1,finish:true);

        [Test] public void HeadquartersTrainsAWorkerThatAppearsBesideIt()
        {
            var match=new PrototypeMatch();var hq=Headquarters(match);int metal=match.Metal;
            Assert.That(match.Train(hq,UnitKind.Worker),Is.True);
            Assert.That(match.Metal,Is.EqualTo(metal-match.Balance.Expedition.Worker.Metal),"The cost is paid when queued");
            Assert.That(match.SupplyUsed,Is.EqualTo(1));Run(match,.05f);
            Assert.That(match.SupplyUsed,Is.EqualTo(2),"Supply is taken once training starts");
            Run(match,(float)match.Balance.Expedition.Worker.TrainSeconds-.15f);Assert.That(match.Units.Count,Is.EqualTo(1));
            Run(match,.2f);
            Assert.That(match.Units.Count,Is.EqualTo(2));Assert.That(hq.Queue,Is.Empty);
            var worker=match.Units[1];
            Assert.That(match.CanTravel((float)worker.X,(float)worker.Z,(float)worker.X,(float)worker.Z,(float)worker.Stats.Radius),Is.True,"The new unit stands on free ground");
            Assert.That(System.Math.Abs((float)worker.X-((float)hq.X+1.5f)),Is.LessThan(3));Assert.That(System.Math.Abs((float)worker.Z-((float)hq.Z+1.5f)),Is.LessThan(3));
        }
        [Test] public void QueueHoldsFiveAndCancelRefunds()
        {
            var match=new PrototypeMatch();var hq=Headquarters(match);match.Metal=1000;
            for(int i=0;i<PrototypeMatch.QueueSize;i++)Assert.That(match.Train(hq,UnitKind.Worker),Is.True);
            Assert.That(match.Train(hq,UnitKind.Worker),Is.False);Assert.That(match.Message,Does.Contain("Очередь"));
            Assert.That(match.Metal,Is.EqualTo(1000-5*50));
            Run(match,3);
            Assert.That(match.CancelTraining(hq,0),Is.True);
            Assert.That(match.Metal,Is.EqualTo(1000-4*50));Assert.That(hq.Queue.Count,Is.EqualTo(4));Assert.That((float)hq.Training,Is.Zero);
            Assert.That(match.CancelTraining(hq,3),Is.True);Assert.That(hq.Queue.Count,Is.EqualTo(3));
        }
        [Test] public void BuildingsTrainOnlyTheirOwnUnitsAndNotWhileBuilding()
        {
            var match=new PrototypeMatch();var hq=Headquarters(match);
            Assert.That(match.Train(hq,UnitKind.Ranged),Is.False);
            var factory=Sites.Place(match,14,15,1);
            Assert.That(match.Train(factory,UnitKind.Ranged),Is.False);Assert.That(match.Message,Does.Contain("строится"));
            Run(match,8.1f);
            Assert.That(match.Train(factory,UnitKind.Worker),Is.False);
            Assert.That(match.Train(factory,UnitKind.Ranged),Is.True);
            match.Plasma=0;Assert.That(match.Train(factory,UnitKind.Air),Is.False);Assert.That(match.Message,Does.Contain("плазмы"));
        }
        [Test] public void FullSupplyHoldsTrainingUntilANewSupplyNodeIsReady()
        {
            var match=new PrototypeMatch();match.AddTestSquad();match.Metal=1000;var hq=Headquarters(match);
            Assert.That(match.SupplyUsed,Is.EqualTo(match.SupplyLimit));
            Assert.That(match.Train(hq,UnitKind.Worker),Is.True);
            Run(match,20);
            Assert.That(hq.SupplyBlocked,Is.True);Assert.That((float)hq.Training,Is.Zero);Assert.That(match.Units.Count,Is.EqualTo(7));
            Sites.Place(match,15,7,0);
            Run(match,(float)match.Balance.Expedition.Buildings[0].BuildSeconds+(float)match.Balance.Expedition.Worker.TrainSeconds+.2f);
            Assert.That(hq.SupplyBlocked,Is.False);Assert.That(match.Units.Count,Is.EqualTo(8));
        }
        [Test] public void NewUnitsGoToTheRallyPointAndRalliedWorkersHarvest()
        {
            var match=new PrototypeMatch();var factory=Factory(match);var hq=Headquarters(match);
            match.SetRally(factory,8.5f,17.5f);
            Assert.That(match.Train(factory,UnitKind.Ranged),Is.True);
            Run(match,(float)match.Balance.Expedition.Unit(UnitKind.Ranged).TrainSeconds+4);
            var rifleman=match.Units.Find(u=>u.Kind==UnitKind.Ranged);
            Assert.That(rifleman,Is.Not.Null);Assert.That((float)rifleman.X,Is.EqualTo(8.5f).Within(.6f));Assert.That((float)rifleman.Z,Is.EqualTo(17.5f).Within(.6f));
            var deposit=match.Map.Resources.Find(r=>r.X==9&&r.Z==5);
            match.SetRally(hq,deposit.X+.5f,deposit.Z+.5f);
            Assert.That(match.Train(hq,UnitKind.Worker),Is.True);
            Run(match,(float)match.Balance.Expedition.Worker.TrainSeconds+5);
            var worker=match.Units.FindLast(u=>u.Kind==UnitKind.Worker);
            Assert.That(worker,Is.Not.SameAs(match.Units[0]));Assert.That(worker.HarvestTarget,Is.SameAs(deposit));
        }
    }
}
