using System.Linq;
using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // The machine faction: nanite mining with the cocoon, self-building sites, its roster and the bot playing it.
    public sealed class NaniteMiningTests
    {
        const int Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeMatch Machines()=>new PrototypeMatch(null,FactionId.Resonance,FactionId.Expedition);
        static PrototypeUnit Weaver(PrototypeMatch match)=>match.Units.Find(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker);
        // Orders the worker to the deposit and runs until it has wrapped it.
        static void Wrap(PrototypeMatch match,PrototypeUnit worker,ResourceDeposit deposit)
        {
            Assert.That(match.Move(worker,(float)deposit.X+.5f,(float)deposit.Z+.5f),Is.True);Assert.That(worker.HarvestTarget,Is.SameAs(deposit));
            for(int i=0;i<200&&worker.Wrap<1;i++)match.Tick(.05f);
            Assert.That((float)worker.Wrap,Is.EqualTo(1),"The worker wraps the deposit");
        }

        [Test] public void EachSidePlaysItsOwnRoster()
        {
            var match=Machines();match.AddEnemyBase();
            Assert.That(match.FactionOf(PrototypeMatch.Player),Is.EqualTo(FactionId.Resonance));
            Assert.That(match.Buildings[0].Stats.Name,Is.EqualTo("Ядро"));Assert.That(Weaver(match).Stats.Name,Is.EqualTo("Ткач"));
            Assert.That(match.Buildings.Find(b=>b.Owner==Enemy&&b.Kind==2).Stats.Name,Is.EqualTo("Командный узел"));
            var hq=match.Buildings[0];int metal=match.Metal;
            Assert.That(match.Train(hq,UnitKind.Worker),Is.True);Assert.That(match.Metal,Is.EqualTo(metal-50),"A weaver costs its own price: as cheap as a human worker, since every building spends one");
            Run(match,8.1f);
            Assert.That(match.Units.FindAll(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker).Count,Is.EqualTo(2),"Assembled in 8 s");
        }
        [Test] public void WeaverWrapsTheDepositAndAddsPortionsWithoutTrips()
        {
            var match=Machines();var worker=Weaver(match);var deposit=match.Map.ResourceAt(9,5);int metal=match.Metal;
            Wrap(match,worker,deposit);
            Assert.That(match.Metal,Is.EqualTo(metal),"Nothing before the first tick");
            Run(match,8.1f);
            Assert.That(match.Metal,Is.EqualTo(metal+20));Assert.That(deposit.Remaining,Is.EqualTo(match.Balance.MetalDeposit-20));
            Run(match,16.2f);
            Assert.That(match.Metal,Is.EqualTo(metal+60));Assert.That(match.MetalHarvested,Is.EqualTo(60));
            Assert.That(worker.Carrying,Is.Zero,"No trips");Assert.That(worker.Mining,Is.True);Assert.That(worker.Moving,Is.False);
        }
        [Test] public void OneWeaverPerDepositTheSecondTakesAFreeOne()
        {
            var match=Machines();var first=Weaver(match);var second=match.Spawn(UnitKind.Worker,14.5f,13.5f);var deposit=match.Map.ResourceAt(9,5);
            Wrap(match,first,deposit);
            Assert.That(match.Move(second,(float)deposit.X+.5f,(float)deposit.Z+.5f),Is.True);
            Run(match,8);
            Assert.That(first.HarvestTarget,Is.SameAs(deposit));Assert.That(second.HarvestTarget,Is.Not.Null.And.Not.SameAs(deposit));
            Assert.That(second.Mining,Is.True,"The second worker wraps a free deposit next to it");
        }
        [Test] public void CocoonTakesBlowsFirstAndGrowsBackOutOfCombat()
        {
            var match=Machines();var worker=Weaver(match);var stats=worker.Stats;
            Wrap(match,worker,match.Map.ResourceAt(9,5));
            Assert.That(worker.Cocoon,Is.EqualTo(stats.Cocoon),"The wrap grows the full cocoon");
            var raider=match.Spawn(UnitKind.Ranged,worker.X+3,worker.Z,Enemy);match.UpdateVision();
            Assert.That(match.Attack(raider,worker),Is.True);
            Run(match,1.5f);
            Assert.That(worker.Cocoon,Is.LessThan(stats.Cocoon));Assert.That(worker.Health,Is.EqualTo(stats.Health),"The cocoon takes the blows first");
            Run(match,6);
            Assert.That(worker.Cocoon,Is.Zero);Assert.That(worker.Health,Is.LessThan(stats.Health),"Then the worker itself is hit");
            raider.Health=0;int health=worker.Health;
            Run(match,2);Assert.That(worker.Cocoon,Is.Zero,"Nothing grows back for a few seconds after the last blow");
            Run(match,2.4f);
            Assert.That(worker.Cocoon,Is.GreaterThan(0));Assert.That(worker.Health,Is.EqualTo(health),"The cocoon grows back first");
            Run(match,4.6f);
            Assert.That(worker.Cocoon,Is.EqualTo(stats.Cocoon));Assert.That(worker.Health,Is.GreaterThan(health),"Inside the network the worker itself mends later");
        }
        [Test] public void LeavingTheDepositDissolvesTheCocoonAndTheWrapOutlivesItsWorker()
        {
            var match=Machines();var worker=Weaver(match);var deposit=match.Map.ResourceAt(9,5);
            Wrap(match,worker,deposit);
            Assert.That(match.Move(worker,15.5f,15.5f),Is.True);match.Tick(.05f);
            Assert.That(worker.Cocoon,Is.Zero);Assert.That((float)worker.Wrap,Is.Zero);
            worker.Health=0;match.Tick(.05f);
            var next=match.Spawn(UnitKind.Worker,13.5f,13.5f);
            Assert.That(match.Move(next,(float)deposit.X+.5f,(float)deposit.Z+.5f),Is.True);
            for(int i=0;i<200&&next.Moving;i++)match.Tick(.05f);
            match.Tick(.05f);
            Assert.That((float)next.Wrap,Is.EqualTo(1),"A deposit wrapped before is taken at once");Assert.That(next.Mining,Is.True);
            Assert.That(next.Cocoon,Is.LessThan(next.Stats.Cocoon),"The cocoon still has to grow back");
        }
        [Test] public void MachineWorkerBecomesTheBuildingThatThenRisesByItself()
        {
            var match=Machines();var worker=Weaver(match);var stats=match.Roster(PrototypeMatch.Player).Buildings[0];
            int supply=match.SupplyUsed,metal=match.Metal;
            var node=Sites.Place(match,15,14,0,worker:worker);
            Assert.That(node.SelfBuilt,Is.True);Assert.That(node.Stats.Name,Is.EqualTo("Вычислительный узел"));
            Assert.That(match.Units,Has.No.Member(worker),"The worker dissolved into the building");Assert.That(worker.AbsorbedInto,Is.SameAs(node));
            Assert.That(match.Message,Does.Contain("распадается на наниты"));
            Assert.That(match.Metal,Is.EqualTo(metal-stats.Metal));Assert.That(match.SupplyUsed,Is.EqualTo(supply-worker.Stats.Supply));
            Run(match,(float)stats.BuildSeconds+.2f);
            Assert.That((float)node.Ready,Is.EqualTo(1),"With no worker it still rises");Assert.That(match.SupplyLimit,Is.EqualTo(18));
            var weaver=match.Spawn(UnitKind.Worker,13.5f,13.5f);
            Assert.That(match.Resume(weaver,Sites.Place(match,18,14,0)),Is.False,"Nothing to resume");
        }
        [Test] public void MachineWorkerMeltsDownInTheMiddleOfTheSite()
        {
            var match=Machines();var worker=Weaver(match);int w=match.Roster(PrototypeMatch.Player).Buildings[1].Width;
            Assert.That(match.Build(worker,15,14,1),Is.True,match.Message);
            for(int i=0;i<400&&worker.AbsorbedInto==null;i++)match.Tick(.05f);
            Assert.That(worker.AbsorbedInto,Is.Not.Null);
            Assert.That((float)worker.X,Is.EqualTo(15+w*.5f).Within(.35f));Assert.That((float)worker.Z,Is.EqualTo(14+w*.5f).Within(.35f),"It walks into the middle of the site, not to its edge");
        }
        [Test] public void CancellingAMachineSiteGivesTheWorkerBack()
        {
            var match=Machines();var worker=Weaver(match);var stats=match.Roster(PrototypeMatch.Player).Buildings[1];
            var site=Sites.Place(match,15,14,1,worker:worker);int metal=match.Metal,workers=match.Units.FindAll(u=>u.Kind==UnitKind.Worker).Count;
            Assert.That(match.CancelConstruction(site),Is.True);
            Assert.That(match.Metal,Is.EqualTo(metal+(int)(stats.Metal*.75f)));
            Assert.That(match.Units.FindAll(u=>u.Kind==UnitKind.Worker).Count,Is.EqualTo(workers+1),"The nanites gather into a worker again");
            Assert.That(match.Message,Does.Contain("ткач"));
            Run(match,.2f);
            var back=match.Units.Find(u=>u.Kind==UnitKind.Worker&&u!=worker);
            Assert.That(match.CanTravel((float)back.X,(float)back.Z,(float)back.X,(float)back.Z,(float)back.Stats.Radius),Is.True,"On free ground where the site was");
        }
        [Test] public void AMachineWorkerQueuesNoBuildingsAndTheCostsMakeUpForIt()
        {
            var match=Machines();var worker=Weaver(match);
            Assert.That(match.QueueBuild(worker,15,14,0),Is.False);Assert.That(match.Message,Does.Contain("очереди построек у него нет"));
            // A building and the worker it takes cost what the building alone cost before the workers were spent.
            var roster=match.Roster(PrototypeMatch.Player);int weaver=roster.Worker.Metal;
            Assert.That(weaver,Is.EqualTo(50));
            Assert.That(new[]{roster.Buildings[0].Metal,roster.Buildings[1].Metal,roster.Buildings[FactionBalance.Main].Metal,roster.Buildings[FactionBalance.Relay].Metal,roster.Buildings[4].Metal}.Select(m=>m+weaver),
                Is.EqualTo(new[]{100,150,400,75,125}));
        }
        [Test] public void BotPlaysTheMachines()
        {
            var match=new PrototypeMatch(null,FactionId.Expedition,FactionId.Resonance);match.AddTestSquad();match.AddEnemySquad();match.AddEnemyBase();match.StartBot();
            Assert.That(match.Cheated,Is.False);
            int army=match.Units.FindAll(u=>u.Owner==Enemy&&u.Stats.Damage>0).Count;
            Run(match,150);
            var workers=match.Units.FindAll(u=>u.Owner==Enemy&&u.Kind==UnitKind.Worker);
            Assert.That(workers.Count,Is.InRange(7,9),"One weaver for each deposit round its base");
            Assert.That(workers.FindAll(w=>w.Mining).Count,Is.GreaterThanOrEqualTo(workers.Count-1),"Weavers stay on their deposits");
            Assert.That(workers.FindAll(w=>w.Mining&&w.HarvestTarget.Plasma).Count,Is.EqualTo(1),"One on plasma");
            var held=new System.Collections.Generic.HashSet<ResourceDeposit>();
            foreach(var w in workers)if(w.Mining)Assert.That(held.Add(w.HarvestTarget),Is.True,"No two weavers on one deposit");
            Assert.That(match.Buildings.FindAll(b=>b.Owner==Enemy&&b.Kind==0).Count,Is.GreaterThanOrEqualTo(2),"Compute nodes before supply runs out");
            Assert.That(match.Buildings.TrueForAll(b=>b.Owner!=Enemy||(float)b.Ready>=1||b.SelfBuilt),Is.True);
            Assert.That(match.Units.FindAll(u=>u.Owner==Enemy&&u.Stats.Damage>0).Count,Is.GreaterThan(army+3));
        }
    }
}
