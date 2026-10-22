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
            var match=new PrototypeMatch();var worker=match.Units[0];float radius=worker.Stats.Radius;Assert.That(match.Build(15,14,0),Is.True);
            Assert.That(match.CanTravel(worker.X,worker.Z,18.7f,17.2f,radius),Is.False);
            Assert.That(match.Move(worker,18.7f,17.2f),Is.True);
            for(int i=0;i<150;i++){float x=worker.X,z=worker.Z;match.Tick(.05f);Assert.That(match.CanTravel(x,z,worker.X,worker.Z,radius),Is.True);}
            Assert.That(worker.X,Is.EqualTo(18.7f).Within(.001f));Assert.That(worker.Z,Is.EqualTo(17.2f).Within(.001f));
        }
        [Test] public void BalanceControlsEconomyMovementAndConstructionDuration()
        {
            var balance=new MatchBalance{StartingMinerals=999,StartingGas=777,MineralDeposit=111,GasDeposit=333};
            balance.Worker.Speed=2;balance.Buildings[0].BuildSeconds=12;balance.Buildings[0].Minerals=37;
            var match=new PrototypeMatch(balance);var worker=match.Units[0];
            Assert.That(match.Gas,Is.EqualTo(777));Assert.That(match.Map.ResourceAt(5,5).Remaining,Is.EqualTo(111));
            Assert.That(match.Build(15,14,0),Is.True);Assert.That(match.Minerals,Is.EqualTo(962));
            match.Tick(6);Assert.That(match.Buildings[1].Ready,Is.EqualTo(.5f));
            match.Tick(5.9f);Assert.That(match.Buildings[1].Ready,Is.LessThan(1));
            match.Tick(.1f);Assert.That(match.Buildings[1].Ready,Is.EqualTo(1).Within(.0001f));
            Assert.That(match.Move(worker,18.5f,13.5f),Is.True);match.Tick(.5f);Assert.That(worker.X,Is.EqualTo(14.5f).Within(.0001f));
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
            Assert.That(match.Build(15,14,0),Is.True);
            Assert.That(match.Minerals,Is.EqualTo(400));
            Assert.That(match.Build(15,14,0),Is.False);Assert.That(match.Minerals,Is.EqualTo(400));
            match.Tick(5);Assert.That(match.Buildings[1].Ready,Is.EqualTo(1));
            match.Minerals=0;Assert.That(match.CanBuild(17,14,0,out _),Is.False);
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
            int before=match.Minerals;match.Tick(.01f);Assert.That(match.Minerals,Is.EqualTo(before));
            for(int i=0;i<100;i++)match.Tick(.1f);
            Assert.That(match.Minerals,Is.GreaterThan(before));Assert.That(match.Map.ResourceAt(9,5).Remaining,Is.LessThan(1500));
        }
        [Test] public void GasCannotBeHarvestedPastRemainingAmount()
        {
            var match=new PrototypeMatch();var gas=match.Map.ResourceAt(5,10);gas.Remaining=3;
            Assert.That(match.Move(match.Units[0],5,10),Is.True);for(int i=0;i<100;i++)match.Tick(.1f);
            Assert.That(match.Gas,Is.EqualTo(103));Assert.That(gas.Remaining,Is.Zero);
        }
        [Test] public void FactoryChargesBothResourcesAndBoundaryPlacementFails()
        {
            var match=new PrototypeMatch();Assert.That(match.Build(15,14,1),Is.True);
            Assert.That(match.Minerals,Is.EqualTo(350));Assert.That(match.Gas,Is.EqualTo(50));
            Assert.That(match.Build(63,63,1),Is.False);Assert.That(match.Build(-1,12,0),Is.False);
            Assert.That(match.Minerals,Is.EqualTo(350));Assert.That(match.Move(match.Units[0],-1,0),Is.False);
        }
        [Test] public void TestSquadHasEveryKindOnFreeGroundWithinSupply()
        {
            var match=new PrototypeMatch();match.AddTestSquad();
            foreach(UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))Assert.That(match.Units.Exists(u=>u.Kind==kind),Is.True,kind.ToString());
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
        [Test] public void OnlyWorkersHarvestAndGroundUnitsBlockConstruction()
        {
            var match=new PrototypeMatch();match.AddTestSquad();
            var ranged=match.Units.Find(u=>u.Kind==UnitKind.Ranged);
            Assert.That(match.Move(ranged,9,5),Is.True);Assert.That(ranged.HarvestTarget,Is.Null);
            int minerals=match.Minerals;for(int i=0;i<100;i++)match.Tick(.1f);Assert.That(match.Minerals,Is.EqualTo(minerals));
            var melee=match.Units.Find(u=>u.Kind==UnitKind.Melee);var air=match.Units.Find(u=>u.Kind==UnitKind.Air);
            Assert.That(match.CanBuild((int)melee.X,(int)melee.Z,0,out var reason),Is.False);Assert.That(reason,Is.EqualTo("Здесь стоит юнит"));
            Assert.That(match.CanBuild((int)air.X,(int)air.Z,0,out _),Is.True);
        }
        [Test] public void NewBuildingReroutesUnitsWalkingThroughIt()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];
            Assert.That(match.Move(worker,13.5f,18.5f),Is.True);Assert.That(worker.WaypointCount,Is.EqualTo(1));
            Assert.That(match.Build(13,16,0),Is.True);Assert.That(worker.WaypointCount,Is.GreaterThan(1));
            for(int i=0;i<100;i++){float x=worker.X,z=worker.Z;match.Tick(.05f);Assert.That(match.CanTravel(x,z,worker.X,worker.Z,worker.Stats.Radius),Is.True);}
            Assert.That(worker.Z,Is.EqualTo(18.5f).Within(.01f));
        }
    }
}
