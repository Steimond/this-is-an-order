using System.Collections.Generic;
using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class BuildingCombatTests
    {
        static void Run(PrototypeMatch match,float seconds){match.UpdateVision();for(float t=0;t<seconds;t+=.05f)match.Tick(.05f);}
        static PrototypeBuilding EnemyNode(PrototypeMatch match)=>match.Buildings.Find(b=>b.Owner==PrototypeMatch.Enemy&&b.Kind==0);

        [Test] public void OrderedUnitsDestroyABuildingAndFreeItsCells()
        {
            var match=new PrototypeMatch();match.AddEnemyBase();var node=EnemyNode(match);
            Assert.That(match.Occupied(node.X,node.Z),Is.True);
            var squad=new List<PrototypeUnit>();for(int i=0;i<4;i++)squad.Add(match.Spawn(UnitKind.Ranged,44+i*.8f,44));
            match.UpdateVision();
            Assert.That(match.SeenBy(PrototypeMatch.Player,node),Is.True);
            PrototypeBuilding destroyed=null;match.Destroyed+=b=>destroyed=b;
            Assert.That(match.AttackGroup(squad,node),Is.True);
            Run(match,30);
            Assert.That(destroyed,Is.SameAs(node));Assert.That(match.Buildings,Has.No.Member(node));
            Assert.That(match.Occupied(node.X,node.Z),Is.False,"A destroyed building no longer blocks movement");
            foreach(var u in squad)Assert.That(u.Order,Is.EqualTo(UnitOrder.Idle),"The order ends with its target; idle units may go on to the next building");
        }
        // As in StarCraft, melee units sent at one target spread round it instead of pressing into the backs of those striking.
        [Test] public void MeleeSquadSpreadsRoundABuilding()
        {
            var match=new PrototypeMatch();match.AddEnemyBase();var factory=match.Buildings.Find(b=>b.Owner==PrototypeMatch.Enemy&&b.Kind==1);factory.Health=100000;
            var squad=new List<PrototypeUnit>();for(int i=0;i<9;i++)squad.Add(match.Spawn(UnitKind.Melee,48.5f+(i%3)*.9f,45.5f+(i/3)*.9f));
            match.UpdateVision();Assert.That(match.AttackGroup(squad,factory),Is.True);
            Run(match,6);
            foreach(var u in squad)
            {
                Assert.That(PrototypeMatch.Gap(u,factory),Is.LessThanOrEqualTo(u.Stats.AttackRange),$"#{u.Id} at {u.X:0.00},{u.Z:0.00} did not reach the building");
                Assert.That(match.CanTravel(u.X,u.Z,u.X,u.Z,u.Stats.Radius),Is.True);
            }
        }
        [Test] public void MeleeSquadSurroundsASingleEnemy()
        {
            var match=new PrototypeMatch();var enemy=match.Spawn(UnitKind.Ranged,32,32,PrototypeMatch.Enemy);enemy.Health=100000;
            var squad=new List<PrototypeUnit>();for(int i=0;i<6;i++)squad.Add(match.Spawn(UnitKind.Melee,27+(i%3)*.9f,31+(i/3)*.9f));
            match.UpdateVision();Assert.That(match.AttackGroup(squad,enemy),Is.True);
            Run(match,5);
            foreach(var u in squad)Assert.That(PrototypeMatch.InRange(u,enemy),Is.True,$"#{u.Id} at {u.X:0.00},{u.Z:0.00} is not striking");
        }
        [Test] public void UnitsComeBeforeBuildingsAndIdleUnitsHitBuildingsNearby()
        {
            var match=new PrototypeMatch();match.AddEnemyBase();var node=EnemyNode(match);
            var rifleman=match.Spawn(UnitKind.Ranged,47,45.6f);var guard=match.Spawn(UnitKind.Melee,49,45.6f,PrototypeMatch.Enemy);
            Run(match,.3f);
            Assert.That(rifleman.Target,Is.SameAs(guard),"An enemy unit is preferred to a building");
            guard.Health=0;Run(match,1.5f);
            Assert.That(rifleman.Target,Is.Null);Assert.That(rifleman.TargetBuilding,Is.SameAs(node),"With no enemy near, an idle unit attacks the building");
            Assert.That(node.Health,Is.LessThan(match.Balance.Buildings[0].Health));
        }
        [Test] public void DestroyingEveryEnemyBuildingWins()
        {
            var match=new PrototypeMatch();match.AddEnemyBase();
            Assert.That(match.Winner,Is.EqualTo(-1));
            foreach(var b in match.Buildings.FindAll(b=>b.Owner==PrototypeMatch.Enemy))b.Health=1;
            var gunship=match.Spawn(UnitKind.Air,50,50);
            var targets=match.Buildings.FindAll(b=>b.Owner==PrototypeMatch.Enemy);
            // Once idle, the gunship also goes on by itself to buildings nearby.
            foreach(var b in targets){if(b.Health<=0)continue;match.UpdateVision();Assert.That(match.Attack(gunship,b),Is.True);Run(match,4);}
            Assert.That(match.Buildings.Exists(b=>b.Owner==PrototypeMatch.Enemy),Is.False);
            Assert.That(match.Winner,Is.EqualTo(PrototypeMatch.Player));Assert.That(match.Message,Does.Contain("Победа"));
        }
        [Test] public void ConstructionStartsFragileAndLosingASupplyNodeLowersTheLimit()
        {
            var match=new PrototypeMatch();var stats=match.Balance.Buildings[0];
            Assert.That(match.Build(15,7,0),Is.True);var node=match.Buildings[1];
            Assert.That(node.Health,Is.EqualTo(stats.Health/10));
            Run(match,stats.BuildSeconds+.1f);
            Assert.That(node.Health,Is.EqualTo(stats.Health));Assert.That(match.SupplyLimit,Is.EqualTo(18));
            var raider=match.Spawn(UnitKind.Melee,17.5f,7.5f,PrototypeMatch.Enemy);match.UpdateVision();
            Assert.That(match.Attack(raider,node),Is.True);
            node.Health=12;Run(match,3);
            Assert.That(match.Buildings,Has.No.Member(node));Assert.That(match.SupplyLimit,Is.EqualTo(10));Assert.That(match.Winner,Is.EqualTo(-1));
        }
    }
}
