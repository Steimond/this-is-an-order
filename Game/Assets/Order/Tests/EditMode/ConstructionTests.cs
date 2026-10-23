using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // Placing buildings for the match tests: a worker of the side builds them, as in the game.
    static class Sites
    {
        // The side's first worker (or the given one) builds the building; the match runs until it is placed, or finished.
        public static PrototypeBuilding Place(PrototypeMatch match,int x,int z,int kind,int owner=PrototypeMatch.Player,bool finish=false,PrototypeUnit worker=null)
        {
            worker??=match.Units.Find(u=>u.Owner==owner&&u.Kind==UnitKind.Worker);
            Assert.That(match.Build(worker,x,z,kind),Is.True,match.Message);
            PrototypeBuilding building=null;
            for(int i=0;i<1200&&(building=match.Buildings.Find(b=>b.X==x&&b.Z==z&&b.Kind==kind))==null;i++)match.Tick(.05f);
            Assert.That(building,Is.Not.Null,"The worker places the building: "+match.Message);
            if(finish){for(int i=0;i<4000&&building.Ready<1;i++)match.Tick(.05f);Assert.That(building.Ready,Is.EqualTo(1));}
            return building;
        }
    }

    public sealed class ConstructionTests
    {
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeUnit Worker(PrototypeMatch match)=>match.Units.Find(u=>u.Kind==UnitKind.Worker&&u.Owner==PrototypeMatch.Player);

        [Test] public void WorkerWalksToTheSiteAndPaysWhenItPlacesTheBuilding()
        {
            var match=new PrototypeMatch();var worker=Worker(match);int metal=match.Metal;
            Assert.That(match.Build(worker,18,8,0),Is.True,match.Message);
            Assert.That(worker.SiteKind,Is.EqualTo(0));Assert.That(worker.Moving,Is.True);
            Assert.That(match.Metal,Is.EqualTo(metal),"Nothing is paid before the worker gets there");
            Assert.That(match.Buildings.Count,Is.EqualTo(1));
            var node=Sites.Place(match,18,8,0,worker:worker);
            Assert.That(match.Metal,Is.EqualTo(metal-match.Balance.Expedition.Buildings[0].Metal));
            Assert.That(node.Builder,Is.SameAs(worker));Assert.That(worker.Constructing,Is.SameAs(node));Assert.That(worker.SiteKind,Is.EqualTo(-1));
            Assert.That(PrototypeMatch.Gap(worker,node),Is.LessThanOrEqualTo(PrototypeMatch.BuildReach));
            Assert.That(match.CanTravel(worker.X,worker.Z,worker.X,worker.Z,worker.Stats.Radius),Is.True,"The worker stands outside the footprint");
            Run(match,match.Balance.Expedition.Buildings[0].BuildSeconds+.1f);
            Assert.That(node.Ready,Is.EqualTo(1));Assert.That(worker.Constructing,Is.Null);Assert.That(match.SupplyLimit,Is.EqualTo(18));
            Assert.That(match.Message,Does.Contain("Построено"));
        }
        [Test] public void BuildingRisesOnlyWhileItsWorkerIsByItAndAnyWorkerCanResume()
        {
            var match=new PrototypeMatch();var worker=Worker(match);
            var node=Sites.Place(match,15,14,0);
            Run(match,1);float ready=node.Ready;Assert.That(ready,Is.GreaterThan(0));
            Assert.That(match.Move(worker,18.5f,17.5f),Is.True);
            Assert.That(worker.Constructing,Is.Null);Assert.That(node.Builder,Is.Null);
            Run(match,3);Assert.That(node.Ready,Is.EqualTo(ready),"Without its worker the construction waits");
            var helper=match.Spawn(UnitKind.Worker,13.5f,17.5f);
            Assert.That(match.Resume(helper,node),Is.True,match.Message);Assert.That(node.Builder,Is.SameAs(helper));
            Run(match,match.Balance.Expedition.Buildings[0].BuildSeconds*(1-ready)+1);
            Assert.That(node.Ready,Is.EqualTo(1));Assert.That(helper.Constructing,Is.Null);
        }
        [Test] public void OneWorkerPerBuildingAndStopLeavesItWaiting()
        {
            var match=new PrototypeMatch();var worker=Worker(match);
            var node=Sites.Place(match,15,14,0);
            var other=match.Spawn(UnitKind.Worker,13.5f,17.5f);
            Assert.That(match.Resume(other,node),Is.False);Assert.That(match.Message,Does.Contain("другой рабочий"));
            Assert.That(match.Resume(match.Spawn(UnitKind.Melee,13.5f,18.5f),node),Is.False,"Only workers build");
            match.Stop(worker);Run(match,1);float ready=node.Ready;Run(match,1);
            Assert.That(node.Ready,Is.EqualTo(ready));
            Assert.That(match.Resume(other,node),Is.True);
        }
        [Test] public void KilledWorkerLeavesTheBuildingWaiting()
        {
            var match=new PrototypeMatch();var worker=Worker(match);
            var node=Sites.Place(match,15,14,0);Run(match,1);
            worker.Health=0;Run(match,.1f);float ready=node.Ready;
            Assert.That(match.Units,Has.No.Member(worker));Assert.That(node.Builder,Is.Null);
            Run(match,3);Assert.That(node.Ready,Is.EqualTo(ready));Assert.That(match.Buildings,Has.Member(node));
        }
        [Test] public void CancelReturnsThreeQuartersAndFreesTheWorker()
        {
            var match=new PrototypeMatch();var worker=Worker(match);var stats=match.Balance.Expedition.Buildings[1];
            var factory=Sites.Place(match,15,14,1);Run(match,2);
            int metal=match.Metal,plasma=match.Plasma;
            Assert.That(match.CancelConstruction(factory),Is.True);
            Assert.That(match.Metal,Is.EqualTo(metal+stats.Metal*3/4));Assert.That(match.Plasma,Is.EqualTo(plasma+stats.Plasma*3/4));
            Assert.That(match.Buildings,Has.No.Member(factory));Assert.That(worker.Constructing,Is.Null);
            Assert.That(match.CanBuild(15,14,1,out _),Is.True,"The footprint is free again");
            Assert.That(match.CancelConstruction(match.Buildings[0]),Is.False,"A finished building is not cancelled");
        }
        [Test] public void OwnUnitsStepOffTheSiteAndEnemiesBlockIt()
        {
            var match=new PrototypeMatch();var worker=Worker(match);
            var guard=match.Spawn(UnitKind.Melee,16.2f,15.1f);match.UpdateVision();
            Assert.That(match.CanBuild(15,14,0,out _),Is.True,"Own units do not block a site");
            var node=Sites.Place(match,15,14,0);
            Assert.That(guard.X<15-guard.Stats.Radius||guard.X>17+guard.Stats.Radius||guard.Z<14-guard.Stats.Radius||guard.Z>16+guard.Stats.Radius,Is.True,"The guard stepped off the site");
            Assert.That(match.CanTravel(guard.X,guard.Z,guard.X,guard.Z,guard.Stats.Radius),Is.True);
            match.Stop(worker);
            var enemy=match.Spawn(UnitKind.Worker,19.1f,15.1f,PrototypeMatch.Enemy);match.UpdateVision();
            Assert.That(match.CanBuild(18,14,0,out var reason),Is.False);Assert.That(reason,Does.Contain("противника"));
            // An enemy that walks onto a site after the order: the worker waits, then gives up without paying.
            enemy.X=24.5f;match.UpdateVision();int metal=match.Metal;
            Assert.That(match.Build(worker,18,14,0),Is.True,match.Message);
            enemy.X=19.1f;
            Run(match,6);
            Assert.That(match.Buildings.Exists(b=>b.X==18&&b.Z==14),Is.False);Assert.That(match.Metal,Is.EqualTo(metal));
            Assert.That(worker.SiteKind,Is.EqualTo(-1));Assert.That(match.Message,Does.Contain("занято"));
            Assert.That(match.Buildings,Has.Member(node));
        }
        [Test] public void ShortOfMetalOnArrivalTheSiteIsDropped()
        {
            var match=new PrototypeMatch();var worker=Worker(match);
            Assert.That(match.Build(worker,18,8,0),Is.True);match.Metal=10;
            Run(match,4);
            Assert.That(match.Buildings.Count,Is.EqualTo(1));Assert.That(worker.SiteKind,Is.EqualTo(-1));
            Assert.That(match.Message,Does.Contain("металла"));Assert.That(match.Metal,Is.EqualTo(10));
        }
        [Test] public void QueuedOrderWaitsUntilTheBuildingIsDone()
        {
            var match=new PrototypeMatch();var worker=Worker(match);
            var node=Sites.Place(match,15,14,0);
            match.Enqueue(worker,new QueuedOrder(UnitOrder.Move,18.5f,11.5f));
            Run(match,match.Balance.Expedition.Buildings[0].BuildSeconds-1);
            Assert.That(worker.Constructing,Is.SameAs(node));Assert.That(worker.Queued.Count,Is.EqualTo(1));
            Run(match,4);
            Assert.That(node.Ready,Is.EqualTo(1));Assert.That(worker.X,Is.EqualTo(18.5f).Within(.05f));Assert.That(worker.Z,Is.EqualTo(11.5f).Within(.05f));
        }
        [Test] public void HealthRisesWithTheBuildingWhateverTheFrameRate()
        {
            foreach(var frame in new[]{.005f,.05f})
            {
                var match=new PrototypeMatch();var stats=match.Balance.Expedition.Buildings[0];
                var node=Sites.Place(match,15,14,0);
                for(float t=0;t<stats.BuildSeconds*.5f-1e-4f;t+=frame)match.Tick(frame);
                Assert.That(node.Ready,Is.EqualTo(.5f).Within(.01f));
                Assert.That(node.Health,Is.EqualTo(stats.Health/10+stats.Health*.45f).Within(stats.Health*.01f),"Frame "+frame);
                node.Health-=30;int hurt=node.Health;
                for(float t=0;t<stats.BuildSeconds*.5f+.1f;t+=frame)match.Tick(frame);
                Assert.That(node.Ready,Is.EqualTo(1));Assert.That(node.Health,Is.EqualTo(stats.Health-30).Within(1),"Damage taken while building stays");
                Assert.That(hurt,Is.LessThan(node.Health));
            }
        }
        [Test] public void BuilderIsNotPushedOffItsBuilding()
        {
            var match=new PrototypeMatch();var worker=Worker(match);
            var node=Sites.Place(match,15,14,0);float x=worker.X,z=worker.Z;
            var runner=match.Spawn(UnitKind.Ranged,x-2.5f,z);
            Assert.That(match.Move(runner,x+2.5f,z),Is.True);
            Run(match,2);
            Assert.That(worker.X,Is.EqualTo(x).Within(.01f));Assert.That(worker.Z,Is.EqualTo(z).Within(.01f));
            Assert.That(node.Ready,Is.GreaterThan(.3f));
        }
    }
}
