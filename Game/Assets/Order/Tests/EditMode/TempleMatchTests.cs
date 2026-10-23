using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class TempleMatchTests
    {
        [Test] public void MovementIsStraightToExactFractionalDestination()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];float tx=17.23f,tz=17.81f;
            Assert.That(match.Move(worker,tx,tz),Is.True);Assert.That(worker.WaypointCount,Is.EqualTo(1));
            match.Tick(.1f);
            Assert.That((worker.X-13.5f)/(tx-13.5f),Is.EqualTo((worker.Z-13.5f)/(tz-13.5f)).Within(.0001f));
            match.Tick(10);Assert.That(worker.X,Is.EqualTo(tx).Within(.0001f));Assert.That(worker.Z,Is.EqualTo(tz).Within(.0001f));
        }
        [Test] public void SmoothedRoutePreservesCollisionRadiusAroundBuildings()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];float radius=worker.Stats.Radius;Sites.Place(match,15,14,0);
            Assert.That(match.CanTravel(worker.X,worker.Z,18.7f,17.2f,radius),Is.False);
            Assert.That(match.Move(worker,18.7f,17.2f),Is.True);
            for(int i=0;i<150;i++){float x=worker.X,z=worker.Z;match.Tick(.05f);Assert.That(match.CanTravel(x,z,worker.X,worker.Z,radius),Is.True);}
            Assert.That(worker.X,Is.EqualTo(18.7f).Within(.001f));Assert.That(worker.Z,Is.EqualTo(17.2f).Within(.001f));
        }
        [Test] public void BalanceControlsEconomyMovementAndConstructionDuration()
        {
            var balance=new MatchBalance{StartingMetal=999,StartingPlasma=777,MetalDeposit=111,PlasmaDeposit=333};
            balance.Expedition.Worker.Speed=2;balance.Expedition.Buildings[0].BuildSeconds=12;balance.Expedition.Buildings[0].Metal=37;
            var match=new PrototypeMatch(balance);var worker=match.Units[0];
            Assert.That(match.Plasma,Is.EqualTo(777));Assert.That(match.Map.ResourceAt(5,5).Remaining,Is.EqualTo(111));
            var node=Sites.Place(match,15,14,0);Assert.That(match.Metal,Is.EqualTo(962));
            match.Tick(6);Assert.That(node.Ready,Is.EqualTo(.5f));
            match.Tick(5.9f);Assert.That(node.Ready,Is.LessThan(1));
            match.Tick(.1f);Assert.That(node.Ready,Is.EqualTo(1).Within(.0001f));
            // The builder may have walked round the building: it leaves along whichever side is open.
            float dx=0,dz=0;foreach(var (ox,oz) in new[]{(-1f,0f),(1f,0f),(0f,-1f),(0f,1f)})if(match.CanTravel(worker.X,worker.Z,worker.X+ox*2,worker.Z+oz*2,worker.Stats.Radius)){dx=ox;dz=oz;break;}
            float x=worker.X,z=worker.Z;Assert.That(match.Move(worker,x+dx*2,z+dz*2),Is.True);match.Tick(.5f);
            Assert.That(worker.X,Is.EqualTo(x+dx).Within(.0001f));Assert.That(worker.Z,Is.EqualTo(z+dz).Within(.0001f));
        }
        [Test] public void FourStartsHaveEqualResourceBudgets()
        {
            var map=new TempleMap();Assert.That(map.Resources.Count,Is.EqualTo(56));
            var positions=new System.Collections.Generic.HashSet<int>();
            foreach(var resource in map.Resources)Assert.That(positions.Add(resource.Z*64+resource.X),Is.True,"Overlapping deposits");
            for(int c=0;c<4;c++)Assert.That(map.Height((c&1)==0?12:51,(c&2)==0?12:51),Is.EqualTo(3));
        }
        [Test] public void ScoutCannotCrossCliffsButCanReachAllFourStartsViaRamps()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];Assert.That(match.CanStep(19,16,20,16),Is.False);
            Assert.That(match.CanStep(19,10,20,10),Is.True);
            foreach(var destination in new[]{new[]{50,13},new[]{50,50},new[]{13,50},new[]{13,13}})
            {
                Assert.That(match.Move(worker,destination[0],destination[1]),Is.True);
                for(int i=0;i<500;i++)match.Tick(.1f);
                Assert.That(worker.X,Is.EqualTo(destination[0]+.5f).Within(.01f));
                Assert.That(worker.Z,Is.EqualTo(destination[1]+.5f).Within(.01f));
            }
        }
        [Test] public void ConstructionRejectsOccupiedFogRampAndUnaffordableCells()
        {
            var match=new PrototypeMatch();
            Assert.That(match.CanBuild(10,10,0,out _),Is.False);
            Assert.That(match.CanBuild(50,50,0,out _),Is.False);
            Assert.That(match.CanBuild(20,10,0,out _),Is.False);
            var node=Sites.Place(match,15,14,0);
            Assert.That(match.Metal,Is.EqualTo(400));
            Assert.That(match.Build(match.Units[0],15,14,0),Is.False);Assert.That(match.Metal,Is.EqualTo(400));
            match.Tick(5);Assert.That(node.Ready,Is.EqualTo(1));
            match.Metal=0;Assert.That(match.CanBuild(17,14,0,out _),Is.False);
        }
        [Test] public void ExploredCellsRemainRememberedAndHighGroundBlocksLowGroundVision()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];worker.X=28.5f;worker.Z=11.5f;match.UpdateVision();
            Assert.That(match.Visible[19,11],Is.False);Assert.That(match.Visible[28,11],Is.True);
            worker.X=45.5f;worker.Z=30.5f;match.UpdateVision();
            Assert.That(match.Visible[28,11],Is.False);Assert.That(match.Explored[28,11],Is.True);
        }
        [Test] public void ResourceOrderHarvestsOnlyAfterArrivalAndDepletesDeposit()
        {
            var match=new PrototypeMatch();Assert.That(match.Move(match.Units[0],9,5),Is.True);
            int before=match.Metal;match.Tick(.01f);Assert.That(match.Metal,Is.EqualTo(before));
            for(int i=0;i<100;i++)match.Tick(.1f);
            Assert.That(match.Metal,Is.GreaterThan(before));Assert.That(match.Map.ResourceAt(9,5).Remaining,Is.LessThan(1500));
        }
        [Test] public void PlasmaCannotBeHarvestedPastRemainingAmount()
        {
            var match=new PrototypeMatch();var plasma=match.Map.ResourceAt(5,10);plasma.Remaining=3;
            Assert.That(match.Move(match.Units[0],5,10),Is.True);for(int i=0;i<100;i++)match.Tick(.1f);
            Assert.That(match.Plasma,Is.EqualTo(103));Assert.That(plasma.Remaining,Is.Zero);
        }
        [Test] public void FactoryChargesBothResourcesAndBoundaryPlacementFails()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];Sites.Place(match,15,14,1);
            Assert.That(match.Metal,Is.EqualTo(350));Assert.That(match.Plasma,Is.EqualTo(50));
            Assert.That(match.Build(worker,63,63,1),Is.False);Assert.That(match.Build(worker,-1,12,0),Is.False);
            Assert.That(match.Metal,Is.EqualTo(350));Assert.That(match.Move(match.Units[0],-1,0),Is.False);
        }
        [Test] public void TestSquadHasEveryKindOnFreeGroundWithinSupply()
        {
            var match=new PrototypeMatch();match.AddTestSquad();
            foreach(UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))if(match.Roster(PrototypeMatch.Player).Has(kind))Assert.That(match.Units.Exists(u=>u.Kind==kind),Is.True,kind.ToString());
            Assert.That(match.SupplyUsed,Is.LessThanOrEqualTo(match.SupplyLimit));
            var ids=new System.Collections.Generic.HashSet<int>();
            foreach(var unit in match.Units)
            {
                Assert.That(ids.Add(unit.Id),Is.True);Assert.That(unit.Health,Is.EqualTo(unit.Stats.Health));
                if(!unit.Stats.Flying)Assert.That(match.CanTravel(unit.X,unit.Z,unit.X,unit.Z,unit.Stats.Radius),Is.True,unit.Stats.Name);
            }
        }
        [Test] public void AirUnitFliesStraightOverCliffsWhileGroundUnitsUseRamps()
        {
            var match=new PrototypeMatch();match.AddTestSquad();
            var air=match.Units.Find(u=>u.Kind==UnitKind.Air);var melee=match.Units.Find(u=>u.Kind==UnitKind.Melee);
            Assert.That(match.Move(air,30.5f,30.5f),Is.True);Assert.That(air.WaypointCount,Is.EqualTo(1));
            Assert.That(match.Move(melee,30.5f,30.5f),Is.True);Assert.That(melee.WaypointCount,Is.GreaterThan(1));
            for(int i=0;i<300;i++)match.Tick(.1f);
            Assert.That(air.X,Is.EqualTo(30.5f).Within(.01f));Assert.That(melee.X,Is.EqualTo(30.5f).Within(.01f));
            Assert.That(match.Visible[30,30],Is.True);
        }
        [Test] public void OnlyWorkersHarvestAndOnlyEnemyGroundUnitsBlockConstruction()
        {
            var match=new PrototypeMatch();match.AddTestSquad();
            var ranged=match.Units.Find(u=>u.Kind==UnitKind.Ranged);
            Assert.That(match.Move(ranged,9,5),Is.True);Assert.That(ranged.HarvestTarget,Is.Null);
            int metal=match.Metal;for(int i=0;i<100;i++)match.Tick(.1f);Assert.That(match.Metal,Is.EqualTo(metal));
            var melee=match.Units.Find(u=>u.Kind==UnitKind.Melee);
            Assert.That(match.CanBuild((int)melee.X,(int)melee.Z,0,out _),Is.True,"Own units step off a site");
            match.Spawn(UnitKind.Melee,16.5f,15.5f,PrototypeMatch.Enemy);match.Spawn(UnitKind.Air,16.5f,8.5f,PrototypeMatch.Enemy);match.UpdateVision();
            Assert.That(match.CanBuild(16,15,0,out var reason),Is.False);Assert.That(reason,Is.EqualTo("Здесь стоит юнит противника"));
            Assert.That(match.CanBuild(16,8,0,out _),Is.True,"Air units do not block");
        }
        [Test] public void NewBuildingReroutesUnitsWalkingThroughIt()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];
            Assert.That(match.Move(worker,13.5f,18.5f),Is.True);Assert.That(worker.WaypointCount,Is.EqualTo(1));
            // A second worker right by the site places the building on the next step, across the first one's way.
            var builder=match.Spawn(UnitKind.Worker,12.6f,17);
            Assert.That(match.Build(builder,13,16,0),Is.True);match.Tick(.05f);
            Assert.That(match.Buildings.Exists(b=>b.X==13&&b.Z==16),Is.True);Assert.That(worker.WaypointCount,Is.GreaterThan(1));
            for(int i=0;i<100;i++){float x=worker.X,z=worker.Z;match.Tick(.05f);Assert.That(match.CanTravel(x,z,worker.X,worker.Z,worker.Stats.Radius),Is.True);}
            Assert.That(worker.Z,Is.EqualTo(18.5f).Within(.01f));
        }
    }
}
