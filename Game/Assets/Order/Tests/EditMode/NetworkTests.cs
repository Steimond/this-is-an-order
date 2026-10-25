using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // The machines' network: coverage, relays and their limit, the network as a connected graph, dark buildings.
    public sealed class NetworkTests
    {
        const int Relay = FactionBalance.Relay;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        // Machines with the whole map in sight (cheat), so sites far from the units can be ordered, and metal to spare.
        static PrototypeMatch Machines(){var match=new PrototypeMatch(null,FactionId.Resonance,FactionId.Expedition);match.ToggleReveal();match.Metal=5000;return match;}
        static PrototypeBuilding Finished(PrototypeMatch match,int x,int z,int kind)=>Sites.Place(match,x,z,kind,finish:true);

        [Test] public void TheVisualLabSetsBuildingsDownAtTheirStagesAndItsNetworkReachesEverywhere()
        {
            var match=new PrototypeMatch(null,FactionId.Resonance,FactionId.Expedition);
            var site=match.PlaceBuilding(1,30,30,PrototypeMatch.Player,.3f);
            Assert.That(site.Ready,Is.EqualTo(.3f));Assert.That(site.SelfBuilt,Is.True,"A machine site rises by itself, as one a worker melted into");
            Assert.That(match.PlaceBuilding(1,50,50,PrototypeMatch.Enemy,.5f).SelfBuilt,Is.False,"A human site waits for its builder");
            var far=match.PlaceBuilding(0,40,24,PrototypeMatch.Player,1);
            Run(match,PrototypeMatch.NetworkGrace+1);
            Assert.That(far.Powered,Is.False,"Out of coverage it goes dark");
            match.ReachEverywhere();match.Tick(.05f);
            Assert.That(far.Linked,Is.True);Assert.That(far.Powered,Is.True);Assert.That(match.Cheated,Is.True);
        }
        [Test] public void MachinesBuildOnlyInsideCoverageHumansAnywhere()
        {
            var match=Machines();
            Assert.That(match.CanBuild(15,14,0,out _),Is.True,"Next to the core");
            Assert.That(match.CanBuild(18,1,0,out var reason),Is.False);Assert.That(reason,Is.EqualTo("Вне покрытия сети"));
            var humans=new PrototypeMatch();humans.ToggleReveal();
            Assert.That(humans.CanBuild(18,1,0,out _),Is.True,"Humans have no network");
        }
        [Test] public void RelayExtendsCoverageOnlyOnceFinishedAndCoresLimitRelays()
        {
            var match=Machines();
            var relay=Sites.Place(match,17,4,Relay);
            Assert.That(match.CanBuild(18,1,0,out _),Is.False,"A rising relay covers nothing yet");
            Run(match,match.Roster(PrototypeMatch.Player).Buildings[Relay].BuildSeconds+.2f);
            Assert.That(relay.Ready,Is.EqualTo(1));Assert.That(relay.Powered,Is.True);
            Assert.That(match.CanBuild(18,1,0,out _),Is.True,"The relay covers the ground round it");
            Assert.That(match.RelaysLeft(PrototypeMatch.Player),Is.EqualTo(2));
            Sites.Place(match,14,17,Relay);Sites.Place(match,6,15,Relay);
            Assert.That(match.CanBuild(8,17,Relay,out var reason),Is.False);Assert.That(reason,Does.Contain("Лимит ретрансляторов"));
        }
        [Test] public void NetworkIsAConnectedGraphAnotherPathKeepsARelayAlive()
        {
            var match=Machines();
            var a=Finished(match,17,17,Relay);var c=Finished(match,19,13,Relay);
            var b=Finished(match,19,19,Relay);
            Assert.That(b.Powered,Is.True);
            a.Health=0;Run(match,.1f);
            Assert.That(match.Buildings,Has.No.Member(a));Assert.That(b.Powered,Is.True,"Still linked to the core through the other relay");
            c.Health=0;Run(match,.1f);
            Assert.That(b.Linked,Is.False,"No path to a core is left");Assert.That(b.Powered,Is.True,"It still works on its grace");
            Run(match,PrototypeMatch.NetworkGrace);
            Assert.That(b.Powered,Is.False,"Then it goes dark");
        }
        [Test] public void ACutOffBuildingWorksOnItsGraceAndANewLinkLightsItAgain()
        {
            var match=Machines();
            var relay=Finished(match,17,17,Relay);
            // A compute node only the relay covers.
            var node=Finished(match,21,20,0);
            Assert.That(match.Covered(PrototypeMatch.Player,22,21),Is.True);int limit=match.SupplyLimit;
            Assert.That(match.CanBuild(21,17,0,out var reason),Is.True,reason);
            relay.Health=0;Run(match,PrototypeMatch.NetworkGrace-1);
            Assert.That(node.Linked,Is.False);Assert.That(node.Powered,Is.True);Assert.That(match.SupplyLimit,Is.EqualTo(limit),"Supply holds on the grace");
            Assert.That(match.CanBuild(21,17,0,out reason),Is.False,"A cut-off zone takes no new buildings");Assert.That(reason,Is.EqualTo("Вне покрытия сети"));
            Run(match,1.2f);
            Assert.That(node.Powered,Is.False);Assert.That(match.SupplyLimit,Is.EqualTo(limit-node.Stats.Supply),"Dark, it gives no supply");
            Finished(match,17,17,Relay);Run(match,.1f);
            Assert.That(node.Linked,Is.True);Assert.That(node.Powered,Is.True);Assert.That(node.DarkFor,Is.Zero);
        }
        [Test] public void MachineBotLinksItsTwoCoresWithARelay()
        {
            // The bot takes its second base by itself from the sixth minute (ExpansionTests); the player cannot be hurt.
            var match=new PrototypeMatch(null,FactionId.Expedition,FactionId.Resonance);match.AddEnemyBase();match.ToggleInvulnerable();match.StartBot();
            Run(match,400);
            PrototypeBuilding second=null;
            for(int i=0;i<200&&(second==null||second.Ready<1);i++){Run(match,1);second=match.Buildings.Find(b=>b.Owner==PrototypeMatch.Enemy&&b.Kind==FactionBalance.Main&&b.X!=51);}
            Assert.That(second,Is.Not.Null);Assert.That(second.Ready,Is.EqualTo(1),"The second core is up");
            var first=match.Buildings.Find(b=>b.Owner==PrototypeMatch.Enemy&&b.Kind==FactionBalance.Main&&b!=second);
            float Away(PrototypeBuilding a,PrototypeBuilding b)=>new UnityEngine.Vector2(a.X+a.Width*.5f-b.X-b.Width*.5f,a.Z+a.Width*.5f-b.Z-b.Width*.5f).magnitude;
            Assert.That(Away(first,second),Is.GreaterThan(first.Stats.Coverage),"The cores do not reach each other");
            Run(match,30);
            var relay=match.Buildings.Find(b=>b.Owner==PrototypeMatch.Enemy&&b.Kind==Relay&&Away(b,first)<=first.Stats.Coverage&&Away(b,second)<=second.Stats.Coverage);
            Assert.That(relay,Is.Not.Null,"A relay within reach of both cores");
            Assert.That(relay.Ready,Is.EqualTo(1));Assert.That(relay.Linked,Is.True);
        }
        [Test] public void UncoveredBuildingsStopRisingTrainingAndGivingSupply()
        {
            var match=Machines();
            // A unit standing at home, to see it outlast the dark network (the workers are spent on the buildings).
            var guard=match.Spawn(UnitKind.Melee,13.5f,16.5f);
            var relay=Finished(match,17,4,Relay);
            var site=Sites.Place(match,18,1,0);
            Run(match,1);float ready=site.Ready;Assert.That(ready,Is.GreaterThan(0));
            relay.Health=0;Run(match,PrototypeMatch.NetworkGrace+.2f);ready=site.Ready;Run(match,2);
            Assert.That(site.Powered,Is.False);Assert.That(site.Ready,Is.EqualTo(ready),"A site outside the network does not rise");
            var node=Finished(match,15,14,0);var assembler=Finished(match,14,17,1);
            Assert.That(match.SupplyLimit,Is.EqualTo(18));Assert.That(match.Train(assembler,UnitKind.Ranged),Is.True);
            var core=match.Buildings.Find(b=>b.Kind==FactionBalance.Main);core.Health=0;Run(match,PrototypeMatch.NetworkGrace+.2f);
            Assert.That(node.Powered,Is.False);Assert.That(match.SupplyLimit,Is.Zero,"A dark compute node gives no supply");
            float trained=assembler.Training;Run(match,2);Assert.That(assembler.Training,Is.EqualTo(trained),"Training stops");
            Assert.That(match.Train(assembler,UnitKind.Ranged),Is.False);Assert.That(match.Message,Does.Contain("сигнала"));
            Assert.That(guard.Alive,Is.True,"Units are never lost to a dark network");Assert.That(match.Units,Has.Member(guard));
            Assert.That(match.Winner,Is.EqualTo(-1));
        }
    }
}
