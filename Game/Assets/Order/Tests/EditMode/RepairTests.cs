using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // Health coming back: human workers repair buildings and machinery, machines mend inside their network and can be
    // taken apart there for part of their price.
    public sealed class RepairTests
    {
        const int Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeMatch Humans(){var match=new PrototypeMatch();match.ToggleReveal();match.Metal=2000;match.Plasma=500;return match;}
        static PrototypeMatch Machines(){var match=new PrototypeMatch(null,FactionId.Resonance,FactionId.Expedition);match.Metal=2000;match.Plasma=500;return match;}
        static PrototypeUnit Worker(PrototypeMatch match)=>match.Units.Find(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker);

        [Test] public void WorkerRepairsABuildingForAQuarterOfItsPriceAtBuildingSpeed()
        {
            var match=Humans();var factory=Sites.Place(match,15,14,1,finish:true);var worker=Worker(match);
            var stats=factory.Stats;factory.Health=stats.Health/2;
            int metal=match.Metal,plasma=match.Plasma;
            Assert.That(match.Repair(worker,factory),Is.True,match.Message);Assert.That(worker.Repairing,Is.SameAs(factory));
            float seconds=System.Math.Max(PrototypeMatch.MinRepairSeconds,stats.BuildSeconds);
            Run(match,seconds*.25f);
            Assert.That(factory.Health,Is.GreaterThan(stats.Health/2));Assert.That(factory.Health,Is.LessThan(stats.Health),"Health comes back over time");
            Assert.That(worker.Welding,Is.True);
            Run(match,seconds*.3f+2);
            Assert.That(factory.Health,Is.EqualTo(stats.Health),"Half the health in half the repair time, plus the walk");
            Assert.That(worker.Repairing,Is.Null);Assert.That(match.Message,Does.Contain("Отремонтировано"));
            // Half the health for a quarter of half the price, in whole units.
            Assert.That(metal-match.Metal,Is.EqualTo((int)(stats.Metal*PrototypeMatch.RepairShare/2)));
            Assert.That(plasma-match.Plasma,Is.EqualTo((int)(stats.Plasma*PrototypeMatch.RepairShare/2)));
        }
        [Test] public void SeveralWorkersRepairFaster()
        {
            int Mended(int workers)
            {
                var match=Humans();var factory=Sites.Place(match,15,14,1,finish:true);
                for(int i=1;i<workers;i++)match.Spawn(UnitKind.Worker,14.5f,13.2f+i*.7f);
                factory.Health=200;
                foreach(var w in match.Units.FindAll(u=>u.Kind==UnitKind.Worker))Assert.That(match.Repair(w,factory),Is.True,match.Message);
                Run(match,6);return factory.Health-200;
            }
            int one=Mended(1),three=Mended(3);
            Assert.That(three,Is.GreaterThan(one*2),"Three workers mend well over twice as fast as one");
        }
        [Test] public void OnlyOwnFinishedBuildingsAndMachineryAreRepaired()
        {
            var match=Humans();match.AddEnemyBase();var worker=Worker(match);
            var rifleman=match.Spawn(UnitKind.Ranged,14.5f,15.5f);rifleman.Health=10;
            Assert.That(match.Repair(worker,rifleman),Is.False);Assert.That(match.Message,Does.Contain("не техника"));
            Assert.That(match.Repair(worker,worker),Is.False,"A worker does not mend itself");
            var hq=match.Buildings.Find(b=>b.Owner==PrototypeMatch.Player&&b.Kind==FactionBalance.Main);
            Assert.That(match.Repair(worker,hq),Is.False);Assert.That(match.Message,Does.Contain("Ремонт не нужен"));
            var enemy=match.Buildings.Find(b=>b.Owner==Enemy);enemy.Health-=100;
            Assert.That(match.Repair(worker,enemy),Is.False,"Not the enemy's");
            var site=Sites.Place(match,15,14,0);
            Assert.That(match.Repair(match.Spawn(UnitKind.Worker,14.5f,13.5f),site),Is.False,"A building still rising is resumed, not repaired");
            var gunship=match.Spawn(UnitKind.Air,13.5f,17.5f);gunship.Health=50;
            var mechanic=match.Spawn(UnitKind.Worker,12.5f,16.5f);
            Assert.That(match.Repair(mechanic,gunship),Is.True,match.Message);
            Run(match,8);Assert.That(gunship.Health,Is.GreaterThan(50),"Machinery is mended, an air unit from the ground beneath");
        }
        [Test] public void RepairStopsWhenTheMetalRunsOutOrOnANewOrder()
        {
            var match=Humans();var factory=Sites.Place(match,15,14,1,finish:true);var worker=Worker(match);
            factory.Health=100;match.Metal=4;
            Assert.That(match.Repair(worker,factory),Is.True);Run(match,15);
            Assert.That(worker.Repairing,Is.Null);Assert.That(match.Message,Does.Contain("недостаточно металла"));
            Assert.That(factory.Health,Is.LessThan(factory.Stats.Health));Assert.That(match.Metal,Is.GreaterThanOrEqualTo(0));
            match.Metal=500;Assert.That(match.Repair(worker,factory),Is.True);Run(match,3);
            int health=factory.Health;Assert.That(match.Move(worker,20.5f,20.5f),Is.True);
            Assert.That(worker.Repairing,Is.Null,"Any new order ends the repair");Run(match,2);Assert.That(factory.Health,Is.EqualTo(health));
        }
        [Test] public void MachinesMendInsideTheNetworkOnceLeftAlone()
        {
            var match=Machines();
            var inside=match.Spawn(UnitKind.Melee,14.5f,16);var outside=match.Spawn(UnitKind.Melee,30.5f,30.5f);outside.Health=15;
            // One real blow, then the raider is gone.
            var raider=match.Spawn(UnitKind.Ranged,inside.X+4,inside.Z,Enemy);match.UpdateVision();
            Assert.That(match.Attack(raider,inside),Is.True);
            for(int i=0;i<200&&inside.Health==inside.Stats.Health;i++)match.Tick(.05f);
            raider.Health=0;int hit=inside.Health;Assert.That(hit,Is.LessThan(inside.Stats.Health));
            Run(match,PrototypeMatch.MendCalm-.5f);
            Assert.That(inside.Health,Is.EqualTo(hit),"Not right after the blow");
            Run(match,6);
            Assert.That(inside.Health,Is.GreaterThan(hit));
            Assert.That(outside.Health,Is.EqualTo(15),"Outside the coverage nothing comes back");
            var node=Sites.Place(match,15,14,0,finish:true);node.Health=300;int before=node.Health;
            Run(match,4);
            Assert.That(node.Health,Is.GreaterThan(before),"Buildings mend too, more slowly");
            Assert.That(node.Health-before,Is.LessThanOrEqualTo((int)(node.Stats.Health*PrototypeMatch.MendBuildings*4.1f)+1));
            var humans=new PrototypeMatch();var rifleman=humans.Spawn(UnitKind.Ranged,14.5f,16);rifleman.Health=20;
            Run(humans,12);Assert.That(rifleman.Health,Is.EqualTo(20),"Humans do not mend by themselves");
        }
        [Test] public void AMachineIsTakenApartForHalfItsPriceLessTheDamage()
        {
            var match=Machines();var leaper=match.Spawn(UnitKind.Leaper,14.5f,16);var stats=leaper.Stats;
            leaper.Health=stats.Health/2;int metal=match.Metal,plasma=match.Plasma,supply=match.SupplyUsed;
            var (backMetal,backPlasma)=match.DisassemblyReturn(leaper);
            Assert.That((backMetal,backPlasma),Is.EqualTo(((int)(stats.Metal*.25f),(int)(stats.Plasma*.25f))));
            Assert.That(match.Disassemble(leaper),Is.True,match.Message);
            Run(match,PrototypeMatch.DisassemblySeconds-.5f);
            Assert.That(leaper.Alive,Is.True);Assert.That(leaper.Disassembly,Is.GreaterThan(.5f));
            Run(match,.6f);
            Assert.That(match.Units.Contains(leaper),Is.False,"Taken apart");
            Assert.That(match.Metal,Is.EqualTo(metal+backMetal));Assert.That(match.Plasma,Is.EqualTo(plasma+backPlasma));
            Assert.That(match.SupplyUsed,Is.EqualTo(supply-stats.Supply));
            var far=match.Spawn(UnitKind.Melee,30.5f,30.5f);
            Assert.That(match.Disassemble(far),Is.False);Assert.That(match.Message,Is.EqualTo("Разобрать можно только в покрытии сети."));
            var spider=match.Spawn(UnitKind.Melee,13.5f,16);
            Assert.That(match.Disassemble(spider),Is.True);Run(match,1);
            Assert.That(match.Move(spider,15.5f,17.5f),Is.True);Assert.That(spider.Disassembling,Is.False,"Any order stops it");
            Run(match,2);Assert.That(spider.Alive,Is.True);
            var humans=new PrototypeMatch();
            Assert.That(humans.Disassemble(humans.Units[0]),Is.False);Assert.That(humans.Message,Does.Contain("только машины"));
        }
        [Test] public void BotRepairsADamagedBuildingOnceTheEnemyIsGone()
        {
            var match=new PrototypeMatch();match.AddEnemyBase();match.StartBot();Run(match,5);
            var supply=match.Buildings.Find(b=>b.Owner==Enemy&&b.Kind==0);supply.Health=200;
            Run(match,3);
            Assert.That(match.Units.Exists(u=>u.Owner==Enemy&&u.Repairing==supply),Is.True,"A worker goes to mend it");
            Run(match,30);
            Assert.That(supply.Health,Is.EqualTo(supply.Stats.Health));
        }
    }
}
