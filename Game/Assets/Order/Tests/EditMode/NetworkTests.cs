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
            Assert.That(b.Powered,Is.False,"No path to a core is left");
        }
        [Test] public void UncoveredBuildingsStopRisingTrainingAndGivingSupply()
        {
            var match=Machines();
            var relay=Finished(match,17,4,Relay);
            var site=Sites.Place(match,18,1,0);
            Run(match,1);float ready=site.Ready;Assert.That(ready,Is.GreaterThan(0));
            relay.Health=0;Run(match,2);
            Assert.That(site.Powered,Is.False);Assert.That(site.Ready,Is.EqualTo(ready),"A site outside the network does not rise");
            var node=Finished(match,15,14,0);var assembler=Finished(match,14,17,1);
            Assert.That(match.SupplyLimit,Is.EqualTo(18));Assert.That(match.Train(assembler,UnitKind.Ranged),Is.True);
            var core=match.Buildings.Find(b=>b.Kind==FactionBalance.Main);core.Health=0;Run(match,.1f);
            Assert.That(node.Powered,Is.False);Assert.That(match.SupplyLimit,Is.Zero,"A dark compute node gives no supply");
            float trained=assembler.Training;Run(match,2);Assert.That(assembler.Training,Is.EqualTo(trained),"Training stops");
            Assert.That(match.Train(assembler,UnitKind.Ranged),Is.False);Assert.That(match.Message,Does.Contain("сигнала"));
            Assert.That(match.Units.FindAll(u=>u.Owner==PrototypeMatch.Player).Count,Is.GreaterThan(0),"Units are never lost to a dark network");
            Assert.That(match.Winner,Is.EqualTo(-1));
        }
    }
}
