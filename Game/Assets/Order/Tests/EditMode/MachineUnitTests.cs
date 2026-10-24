using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // The machines' own combat units: the spider swarm and the leaper (rules in SETTING.md).
    public sealed class MachineUnitTests
    {
        const int Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeMatch Machines()=>new PrototypeMatch(null,FactionId.Resonance,FactionId.Expedition);
        // A leaper in the open basin west of a standing enemy worker, which cannot hit back or run.
        static (PrototypeMatch match,PrototypeUnit leaper,PrototypeUnit target) Duel(float targetX=34)
        {
            var match=Machines();
            var leaper=match.Spawn(UnitKind.Leaper,30.5f,25.5f);var target=match.Spawn(UnitKind.Worker,targetX,25.5f,Enemy);
            match.UpdateVision();
            return (match,leaper,target);
        }
        static void RunUntilLanded(PrototypeMatch match,PrototypeUnit leaper)
        {
            for(int i=0;i<40&&!leaper.Leaping;i++)match.Tick(.05f);
            Assert.That(leaper.Leaping,Is.True,"The leaper jumps");
            for(int i=0;i<40&&leaper.Leaping;i++)match.Tick(.05f);
        }

        [Test] public void MachinesTrainSpidersAndLeapersHumansDoNot()
        {
            var match=Machines();var roster=match.Roster(PrototypeMatch.Player);
            Assert.That(roster.Unit(UnitKind.Melee).Name,Is.EqualTo("Паучок"));Assert.That(roster.Unit(UnitKind.Leaper).Name,Is.EqualTo("Прыгун"));
            Assert.That(roster.Unit(UnitKind.Melee).Speed,Is.GreaterThan(new MatchBalance().Expedition.Unit(UnitKind.Melee).Speed),"Spiders are faster than assault units");
            Assert.That(roster.Buildings[1].Trains,Does.Contain(UnitKind.Leaper));
            Assert.That(new PrototypeMatch().Roster(PrototypeMatch.Player).Has(UnitKind.Leaper),Is.False);
        }
        [Test] public void LeaperJumpsThroughItsTargetHitsOnceAndLandsBehindIt()
        {
            var (match,leaper,target)=Duel();var stats=leaper.Stats;float before=target.X-leaper.X;
            RunUntilLanded(match,leaper);
            Assert.That(target.Health,Is.EqualTo(target.Stats.Health-stats.Damage),"Hit as the leaper passes through");
            Assert.That(leaper.X,Is.GreaterThan(target.X),"Lands behind the target");
            Assert.That(leaper.X-target.X,Is.LessThan(before),"Closer to it than before the jump");
            Assert.That(match.CanTravel(leaper.X,leaper.Z,leaper.X,leaper.Z,stats.Radius),Is.True);
            int health=target.Health;Run(match,stats.AttackCooldown-.5f);
            Assert.That(target.Health,Is.EqualTo(health),"Nothing until the jump recharges");
            Run(match,2);Assert.That(target.Alive,Is.False,"The next jump finishes it");
        }
        [Test] public void UnitOnTheLineStopsTheJumpWithoutDamage()
        {
            var (match,leaper,target)=Duel();
            var blocker=match.Spawn(UnitKind.Worker,32.3f,25.5f);
            RunUntilLanded(match,leaper);
            Assert.That(target.Health,Is.EqualTo(target.Stats.Health),"No damage");
            Assert.That(leaper.X,Is.LessThan(blocker.X),"Stopped before the unit in the way");
            Assert.That(blocker.X-leaper.X,Is.EqualTo(leaper.Stats.Radius+blocker.Stats.Radius).Within(.1f));
        }
        [Test] public void TakenLandingSpotStopsTheLeaperRightBeforeTheObstacle()
        {
            var (match,leaper,target)=Duel();
            float landing=target.X+(target.X-leaper.X)*.5f;var standing=match.Spawn(UnitKind.Worker,landing,25.5f);
            RunUntilLanded(match,leaper);
            Assert.That(target.Health,Is.EqualTo(target.Stats.Health-leaper.Stats.Damage),"It still went through the target");
            Assert.That(leaper.X,Is.GreaterThan(target.X));
            Assert.That(standing.X-leaper.X,Is.GreaterThanOrEqualTo(leaper.Stats.Radius+standing.Stats.Radius-.01f).And.LessThan(leaper.Stats.Radius+standing.Stats.Radius+.15f));
        }
        [Test] public void LeaperDoesNotJumpAcrossObstacles()
        {
            var match=Machines();
            // A row of metal veins between the two: the leaper has to walk round.
            var leaper=match.Spawn(UnitKind.Leaper,28.5f,7.5f);var target=match.Spawn(UnitKind.Worker,28.5f,10.6f,Enemy);match.UpdateVision();
            for(int i=0;i<16;i++){match.Tick(.05f);Assert.That(leaper.Leaping,Is.False);}
            Assert.That(target.Health,Is.EqualTo(target.Stats.Health));
            Assert.That(leaper.Moving,Is.True,"It walks round instead");
        }
        [Test] public void EmitterBeamGrowsOnOneTargetAndStartsOverOnANewOneOrAfterAPause()
        {
            var match=new PrototypeMatch(null,FactionId.Resonance,FactionId.Expedition);match.ToggleReveal();match.AddEnemyBase();
            var emitter=match.Spawn(UnitKind.Ranged,48.8f,49.5f);var stats=emitter.Stats;match.UpdateVision();
            Assert.That(stats.Ramp,Is.EqualTo(3));Assert.That(stats.Name,Is.EqualTo("Излучатель"));
            var supply=match.Buildings.Find(b=>b.Owner==Enemy&&b.Kind==0);var hq=match.Buildings.Find(b=>b.Owner==Enemy&&b.Kind==FactionBalance.Main);
            // The size of the next blow on the building.
            int Blow(PrototypeBuilding b){int health=b.Health;for(int i=0;i<40&&b.Health==health;i++)match.Tick(.05f);return health-b.Health;}
            Assert.That(match.Attack(emitter,supply),Is.True);
            Assert.That(Blow(supply),Is.EqualTo(stats.Damage),"The beam starts weak");
            Run(match,stats.RampSeconds+.2f);
            Assert.That(Blow(supply),Is.EqualTo((int)(stats.Damage*stats.Ramp)),"Held on one target it grows to three times");
            Assert.That(match.Focus(emitter),Is.EqualTo(1).Within(1e-3f));
            Assert.That(match.Attack(emitter,hq),Is.True);
            Assert.That(Blow(hq),Is.EqualTo(stats.Damage),"A new target starts it over");
            // A real pause: it walks off out of reach (left alone next to a building, an idle unit goes on striking it).
            Run(match,2);Assert.That(match.Move(emitter,40.5f,44.5f),Is.True);Run(match,1.5f);
            Assert.That(match.Focus(emitter),Is.Zero,"A pause drops it");
            Assert.That(match.Attack(emitter,hq),Is.True);Assert.That(Blow(hq),Is.EqualTo(stats.Damage));
        }
    }
}
