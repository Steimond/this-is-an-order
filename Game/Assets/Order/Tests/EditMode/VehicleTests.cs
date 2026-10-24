using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // The humans' vehicles: the armoured tank, the howitzer that fires deployed, far and over an area, and the VTOL
    // interceptor that hits only air.
    public sealed class VehicleTests
    {
        const int Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeMatch Field(){var match=new PrototypeMatch();match.ToggleReveal();return match;}

        [Test] public void TheFactoryTrainsTheTankTheHowitzerAndTheInterceptor()
        {
            var match=Field();match.Metal=2000;match.Plasma=2000;var roster=match.Roster(PrototypeMatch.Player);
            foreach(var kind in new[]{UnitKind.Tank,UnitKind.Artillery,UnitKind.Fighter})Assert.That(roster.Has(kind),Is.True,kind.ToString());
            Assert.That(roster.Has(UnitKind.Leaper),Is.False,"No leaper for humans");
            Assert.That(match.Roster(Enemy).Has(UnitKind.Tank),Is.True);
            Assert.That(new PrototypeMatch(null,FactionId.Resonance,FactionId.Expedition).Roster(PrototypeMatch.Player).Has(UnitKind.Tank),Is.False,"Machines have no tank");
            var factory=Sites.Place(match,15,14,1,finish:true);int metal=match.Metal,plasma=match.Plasma;
            Assert.That(match.Train(factory,UnitKind.Tank),Is.True,match.Message);
            Assert.That(match.Metal,Is.EqualTo(metal-roster.Unit(UnitKind.Tank).Metal));Assert.That(match.Plasma,Is.EqualTo(plasma-roster.Unit(UnitKind.Tank).Plasma));
            match.ToggleFastBuild();Run(match,roster.Unit(UnitKind.Tank).TrainSeconds/PrototypeMatch.FastFactor+.5f);
            var tank=match.Units.Find(u=>u.Kind==UnitKind.Tank);
            Assert.That(tank,Is.Not.Null);Assert.That(tank.Stats.Mechanical,Is.True,"Workers repair vehicles");
        }
        [Test] public void TankArmourTakesAPointOffEveryBlow()
        {
            var match=new PrototypeMatch(null,FactionId.Expedition,FactionId.Resonance);match.ToggleReveal();
            var tank=match.Spawn(UnitKind.Tank,30,30);var spider=match.Spawn(UnitKind.Melee,31,30,Enemy);match.UpdateVision();
            Assert.That(match.Armor(tank),Is.EqualTo(1));
            match.Hold(tank);Assert.That(match.Attack(spider,tank),Is.True);
            int health=tank.Health;for(int i=0;i<60&&tank.Health==health;i++)match.Tick(.05f);
            Assert.That(health-tank.Health,Is.EqualTo(spider.Stats.Damage-1));
        }
        [Test] public void HowitzerFiresOnlyDeployedFarAndOverAnArea()
        {
            var match=Field();var howitzer=match.Spawn(UnitKind.Artillery,20,30);var stats=howitzer.Stats;
            // A clump of enemy riflemen far beyond any other unit's reach.
            var target=match.Spawn(UnitKind.Ranged,30,30,Enemy);var near=match.Spawn(UnitKind.Ranged,30.7f,30,Enemy);var far=match.Spawn(UnitKind.Ranged,33,30,Enemy);
            match.Hold(target);match.Hold(near);match.Hold(far);
            Run(match,.2f);
            Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Deploying),"It deploys by itself when a target comes within reach");
            Run(match,stats.DeploySeconds-.5f);
            Assert.That(target.Health+near.Health+far.Health,Is.EqualTo(3*target.Stats.Health),"No shot before it is deployed");
            Run(match,1);
            Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Deployed));
            var hit=howitzer.Target;Assert.That(hit,Is.Not.Null);
            int full=match.Damage(howitzer);
            Assert.That(hit.Stats.Health-hit.Health,Is.EqualTo(full),"The target takes the whole shell");
            var neighbour=hit==target?near:hit==near?target:null;
            if(neighbour!=null)Assert.That(neighbour.Stats.Health-neighbour.Health,Is.EqualTo((int)(full*PrototypeMatch.SplashShare)),"Its neighbour half");
            Assert.That(far.Health,Is.EqualTo(far.Stats.Health).Or.EqualTo(far.Stats.Health-full),"Outside the burst only a direct hit");
        }
        [Test] public void HowitzerCannotHitWhatIsTooCloseAndAnchoredItIsNotPushed()
        {
            var match=Field();var howitzer=match.Spawn(UnitKind.Artillery,20,30);
            var close=match.Spawn(UnitKind.Ranged,21.5f,30,Enemy);match.Hold(close);
            Run(match,4);
            Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Packed),"Nothing in its band: it stays packed");
            Assert.That(close.Health,Is.EqualTo(close.Stats.Health),"Inside the dead zone it is safe");
            Assert.That(match.ToggleDeploy(howitzer),Is.True);Run(match,howitzer.Stats.DeploySeconds+.1f);
            Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Deployed));
            float x=howitzer.X,z=howitzer.Z;
            var ally=match.Spawn(UnitKind.Ranged,18,30);Assert.That(match.Move(ally,23,30),Is.True);Run(match,2);
            Assert.That(howitzer.X,Is.EqualTo(x).Within(1e-4f));Assert.That(howitzer.Z,Is.EqualTo(z).Within(1e-4f),"A deployed howitzer is not pushed aside");
        }
        [Test] public void AMoveOrderPacksItFirstAndHandPackingHoldsFire()
        {
            var match=Field();var howitzer=match.Spawn(UnitKind.Artillery,20,30);var stats=howitzer.Stats;
            Assert.That(match.ToggleDeploy(howitzer),Is.True);Run(match,stats.DeploySeconds+.1f);
            Assert.That(match.Move(howitzer,20,40),Is.True);
            Run(match,stats.DeploySeconds-.5f);
            Assert.That(howitzer.Z,Is.EqualTo(30).Within(1e-4f),"It does not move while it packs up");Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Packing));
            Run(match,3);
            Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Packed));Assert.That(howitzer.Z,Is.GreaterThan(31),"Then it drives off");
            Run(match,5);
            var enemy=match.Spawn(UnitKind.Ranged,howitzer.X+8,howitzer.Z,Enemy);match.Hold(enemy);
            Assert.That(match.ToggleDeploy(howitzer),Is.True);Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Deploying),"E deploys it on the spot");
            Assert.That(match.ToggleDeploy(howitzer),Is.True);Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Packing),"E again packs it up");
            Run(match,stats.DeploySeconds+1);
            Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Packed),"Packed by hand it stays packed with a target in reach");
            Assert.That(enemy.Health,Is.EqualTo(enemy.Stats.Health));
        }
        [Test] public void OnAnAttackMoveItDeploysFightsThenPacksAndGoesOn()
        {
            var match=Field();var howitzer=match.Spawn(UnitKind.Artillery,20,30);
            var enemy=match.Spawn(UnitKind.Ranged,31,31,Enemy);match.Hold(enemy);
            Assert.That(match.AttackMove(howitzer,40,40),Is.True);
            for(int i=0;i<600&&enemy.Alive;i++)match.Tick(.05f);
            Assert.That(enemy.Alive,Is.False,"It shells the enemy on the way");
            Run(match,PrototypeMatch.PackAfter+howitzer.Stats.DeploySeconds+12);
            Assert.That(howitzer.SiegeMode,Is.EqualTo(SiegeState.Packed));
            Assert.That(howitzer.X,Is.GreaterThan(37));Assert.That(howitzer.Z,Is.GreaterThan(37),"It goes on to its point");
        }
        [Test] public void InterceptorHitsOnlyAir()
        {
            var match=Field();match.AddEnemyBase();var interceptor=match.Spawn(UnitKind.Fighter,30,30);
            var gunship=match.Spawn(UnitKind.Air,33,30,Enemy);var rifleman=match.Spawn(UnitKind.Ranged,32,31,Enemy);
            Assert.That(match.CanAttack(interceptor,gunship),Is.True);Assert.That(match.CanAttack(interceptor,rifleman),Is.False);
            Assert.That(match.CanAttack(interceptor,match.Buildings.Find(b=>b.Owner==Enemy)),Is.False,"Nor buildings");
            match.Hold(rifleman);
            for(int i=0;i<400&&gunship.Alive;i++)match.Tick(.05f);
            Assert.That(gunship.Alive,Is.False,"It brings the gunship down");
        }
        [Test] public void HumanBotFieldsVehicles()
        {
            var match=new PrototypeMatch();match.AddEnemyBase();match.ToggleInvulnerable();match.StartBot();
            Run(match,420);
            var army=match.Units.FindAll(u=>u.Owner==Enemy&&u.Stats.Damage>0);
            Assert.That(army.Exists(u=>u.Kind==UnitKind.Tank||u.Kind==UnitKind.Artillery),Is.True,"Tanks or howitzers in the army");
            Assert.That(army.Exists(u=>u.Kind==UnitKind.Fighter),Is.False,"No interceptors against a player without air units");
        }
    }
}
