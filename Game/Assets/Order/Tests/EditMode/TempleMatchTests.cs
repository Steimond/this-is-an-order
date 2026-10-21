using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class TempleMatchTests
    {
        [Test] public void MovementIsStraightToExactFractionalDestination()
        {
            var match=new PrototypeMatch();float tx=17.23f,tz=17.81f;
            Assert.That(match.Move(tx,tz),Is.True);Assert.That(match.WaypointCount,Is.EqualTo(1));
            match.Tick(.1f);
            Assert.That((match.X-13.5f)/(tx-13.5f),Is.EqualTo((match.Z-13.5f)/(tz-13.5f)).Within(.0001f));
            match.Tick(10);Assert.That(match.X,Is.EqualTo(tx).Within(.0001f));Assert.That(match.Z,Is.EqualTo(tz).Within(.0001f));
        }
        [Test] public void SmoothedRoutePreservesCollisionRadiusAroundBuildings()
        {
            var match=new PrototypeMatch();Assert.That(match.Build(15,14,0),Is.True);
            Assert.That(match.CanTravel(match.X,match.Z,18.7f,17.2f),Is.False);
            Assert.That(match.Move(18.7f,17.2f),Is.True);
            for(int i=0;i<150;i++){float x=match.X,z=match.Z;match.Tick(.05f);Assert.That(match.CanTravel(x,z,match.X,match.Z),Is.True);}
            Assert.That(match.X,Is.EqualTo(18.7f).Within(.001f));Assert.That(match.Z,Is.EqualTo(17.2f).Within(.001f));
        }
        [Test] public void BalanceControlsEconomyMovementAndConstructionDuration()
        {
            var balance=new MatchBalance{StartingMinerals=999,StartingGas=777,MineralDeposit=111,GasDeposit=333};
            balance.Worker.Speed=2;balance.Buildings[0].BuildSeconds=12;balance.Buildings[0].Minerals=37;
            var match=new PrototypeMatch(balance);Assert.That(match.Gas,Is.EqualTo(777));Assert.That(match.Map.ResourceAt(5,5).Remaining,Is.EqualTo(111));
            Assert.That(match.Build(15,14,0),Is.True);Assert.That(match.Minerals,Is.EqualTo(962));
            match.Tick(6);Assert.That(match.Buildings[1].Ready,Is.EqualTo(.5f));
            match.Tick(5.9f);Assert.That(match.Buildings[1].Ready,Is.LessThan(1));
            match.Tick(.1f);Assert.That(match.Buildings[1].Ready,Is.EqualTo(1).Within(.0001f));
            Assert.That(match.Move(18.5f,13.5f),Is.True);match.Tick(.5f);Assert.That(match.X,Is.EqualTo(14.5f).Within(.0001f));
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
            var match=new PrototypeMatch();Assert.That(match.CanStep(19,16,20,16),Is.False);
            Assert.That(match.CanStep(19,10,20,10),Is.True);
            foreach(var destination in new[]{new[]{50,13},new[]{50,50},new[]{13,50},new[]{13,13}})
            {
                Assert.That(match.Move(destination[0],destination[1]),Is.True);
                for(int i=0;i<500;i++)match.Tick(.1f);
                Assert.That(match.X,Is.EqualTo(destination[0]+.5f).Within(.01f));
                Assert.That(match.Z,Is.EqualTo(destination[1]+.5f).Within(.01f));
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
            var match=new PrototypeMatch();match.X=28.5f;match.Z=11.5f;match.UpdateVision();
            Assert.That(match.Visible[19,11],Is.False);Assert.That(match.Visible[28,11],Is.True);
            match.X=45.5f;match.Z=30.5f;match.UpdateVision();
            Assert.That(match.Visible[28,11],Is.False);Assert.That(match.Explored[28,11],Is.True);
        }
        [Test] public void ResourceOrderHarvestsOnlyAfterArrivalAndDepletesDeposit()
        {
            var match=new PrototypeMatch();Assert.That(match.Move(9,5),Is.True);
            int before=match.Minerals;match.Tick(.01f);Assert.That(match.Minerals,Is.EqualTo(before));
            for(int i=0;i<100;i++)match.Tick(.1f);
            Assert.That(match.Minerals,Is.GreaterThan(before));Assert.That(match.Map.ResourceAt(9,5).Remaining,Is.LessThan(1500));
        }
        [Test] public void GasCannotBeHarvestedPastRemainingAmount()
        {
            var match=new PrototypeMatch();var gas=match.Map.ResourceAt(5,10);gas.Remaining=3;
            Assert.That(match.Move(5,10),Is.True);for(int i=0;i<100;i++)match.Tick(.1f);
            Assert.That(match.Gas,Is.EqualTo(103));Assert.That(gas.Remaining,Is.Zero);
        }
        [Test] public void FactoryChargesBothResourcesAndBoundaryPlacementFails()
        {
            var match=new PrototypeMatch();Assert.That(match.Build(15,14,1),Is.True);
            Assert.That(match.Minerals,Is.EqualTo(350));Assert.That(match.Gas,Is.EqualTo(50));
            Assert.That(match.Build(63,63,1),Is.False);Assert.That(match.Build(-1,12,0),Is.False);
            Assert.That(match.Minerals,Is.EqualTo(350));Assert.That(match.Move(-1,0),Is.False);
        }
    }
}
