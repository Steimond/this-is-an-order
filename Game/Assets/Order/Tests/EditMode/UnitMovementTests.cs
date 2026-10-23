using System.Collections.Generic;
using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class UnitMovementTests
    {
        static void Run(PrototypeMatch match,float seconds,System.Action check=null){for(float t=0;t<seconds;t+=.05f){match.Tick(.05f);check?.Invoke();}}
        static float Distance(PrototypeUnit a,PrototypeUnit b)=>(float)System.Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
        static void AssertApart(IList<PrototypeUnit> units)
        {
            for(int i=0;i<units.Count;i++)for(int j=i+1;j<units.Count;j++)
                if(units[i].Stats.Flying==units[j].Stats.Flying)
                    Assert.That(Distance(units[i],units[j]),Is.GreaterThanOrEqualTo(units[i].Stats.Radius+units[j].Stats.Radius-.03f),$"{units[i].Id} overlaps {units[j].Id}");
        }
        static void AssertOnFreeGround(PrototypeMatch match,PrototypeUnit unit)=>
            Assert.That(match.CanTravel(unit.X,unit.Z,unit.X,unit.Z,unit.Stats.Radius),Is.True,$"{unit.Stats.Name} #{unit.Id} at {unit.X:0.00},{unit.Z:0.00} is inside an obstacle");

        [Test] public void ScatteredGroupGathersAroundThePointWithoutOverlap()
        {
            var match=new PrototypeMatch();var group=new List<PrototypeUnit>();
            for(int i=0;i<7;i++)group.Add(match.Spawn(i%2==0?UnitKind.Melee:UnitKind.Ranged,20.5f+i*4,27.5f));
            Assert.That(match.MoveGroup(group,32,24),Is.True);
            Run(match,10);
            AssertApart(group);
            foreach(var u in group)
            {
                Assert.That(u.Moving,Is.False);AssertOnFreeGround(match,u);
                Assert.That((u.X-32)*(u.X-32)+(u.Z-24)*(u.Z-24),Is.LessThan(2.2f*2.2f),$"#{u.Id} stopped too far from the point");
            }
        }
        [Test] public void CompactGroupSentAwayKeepsItsFormation()
        {
            var match=new PrototypeMatch();var group=new List<PrototypeUnit>();
            for(int i=0;i<3;i++)for(int j=0;j<2;j++)group.Add(match.Spawn(UnitKind.Ranged,30+i*1.1f,26+j*1.1f));
            var offsets=new List<(float,float)>();foreach(var u in group)offsets.Add((u.X-31.1f,u.Z-26.55f));
            Assert.That(match.MoveGroup(group,40,36),Is.True);
            Run(match,8);
            AssertApart(group);
            for(int i=0;i<group.Count;i++)
            {
                Assert.That(group[i].X,Is.EqualTo(40+offsets[i].Item1).Within(.05f));Assert.That(group[i].Z,Is.EqualTo(36+offsets[i].Item2).Within(.05f));
            }
        }
        [Test] public void MovingUnitShovesAnIdleAllyAsideAndArrivesExactly()
        {
            var match=new PrototypeMatch();
            var idle=match.Spawn(UnitKind.Melee,26,30);var mover=match.Spawn(UnitKind.Ranged,21,30);
            Assert.That(match.Move(mover,31f,30f),Is.True);
            Run(match,4,()=>{AssertOnFreeGround(match,idle);AssertOnFreeGround(match,mover);});
            Assert.That(mover.X,Is.EqualTo(31).Within(.01f));Assert.That(mover.Z,Is.EqualTo(30).Within(.01f));
            Assert.That(System.Math.Abs(idle.Z-30),Is.GreaterThan(.3f),"The idle unit should have stepped aside");
            Assert.That(idle.Moving,Is.False);
        }
        [Test] public void UnreachablePointLeadsAsCloseAsPossible()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];
            Assert.That(match.Move(worker,11.5f,11.5f),Is.True);Assert.That(match.Message,Does.Contain("ближе"));
            Run(match,5);
            Assert.That(worker.Moving,Is.False);AssertOnFreeGround(match,worker);
            Assert.That((worker.X-11.5f)*(worker.X-11.5f)+(worker.Z-11.5f)*(worker.Z-11.5f),Is.LessThan(2.1f*2.1f));
        }
        [Test] public void SquadCrossesTheRampWithoutEnteringWallsOrEachOther()
        {
            var match=new PrototypeMatch();match.AddTestSquad();
            var squad=match.Units.FindAll(u=>u.Kind!=UnitKind.Worker);
            Assert.That(match.MoveGroup(squad,30.5f,17.5f),Is.True);
            Run(match,20,()=>{foreach(var u in squad)if(!u.Stats.Flying)AssertOnFreeGround(match,u);});
            AssertApart(match.Units);
            foreach(var u in squad){Assert.That(u.Moving,Is.False,$"#{u.Id} still moving");Assert.That(match.HeightAt(u.X,u.Z),Is.EqualTo(0),$"#{u.Id} did not come down");}
        }
        [Test] public void CrowdFlowsThroughTheRampWithoutSpinning()
        {
            var match=new PrototypeMatch();match.AddTestSquad();
            for(int i=0;i<12;i++)match.Spawn(i%2==0?UnitKind.Melee:UnitKind.Ranged,3.5f+(i%4)*1.1f,13+(i/4)*1.1f);
            var crowd=match.Units.FindAll(u=>u.Kind!=UnitKind.Worker);
            foreach(var target in new[]{(29.5f,18.5f),(8.5f,16.5f)})
            {
                Assert.That(match.MoveGroup(crowd,target.Item1,target.Item2),Is.True);
                var goals=new List<Waypoint>();foreach(var u in crowd){var last=new Waypoint(u.X,u.Z);foreach(var p in u.Route)last=p;goals.Add(last);}
                int steps=0,backwards=0;
                for(float t=0;t<15;t+=.05f)
                {
                    var before=new List<(float x,float z,float hx,float hz)>();
                    foreach(var u in crowd){float hx=0,hz=0;foreach(var p in u.Route){hx=p.X-u.X;hz=p.Z-u.Z;break;}before.Add((u.X,u.Z,hx,hz));}
                    match.Tick(.05f);
                    for(int i=0;i<crowd.Count;i++)
                    {
                        var u=crowd[i];if(!u.Stats.Flying)AssertOnFreeGround(match,u);
                        var b=before[i];if(b.hx==0&&b.hz==0)continue;
                        steps++;float l=(float)System.Math.Sqrt(b.hx*b.hx+b.hz*b.hz);
                        if(((u.X-b.x)*b.hx+(u.Z-b.z)*b.hz)/l<-.01f)backwards++;
                    }
                }
                AssertApart(crowd);
                for(int i=0;i<crowd.Count;i++)
                {
                    Assert.That(crowd[i].Moving,Is.False,$"#{crowd[i].Id} still moving");
                    Assert.That(System.Math.Abs(crowd[i].X-goals[i].X)+System.Math.Abs(crowd[i].Z-goals[i].Z),Is.LessThan(1),$"#{crowd[i].Id} stopped away from its place");
                }
                Assert.That(backwards,Is.LessThan(steps*.05f),"Too much jostling backwards");
            }
        }
        // A group order spreads the paths across the way: a big crowd fills the ramp's width instead of filing down one lane.
        [Test] public void BigCrowdFillsTheRampWidthInsteadOfQueueing()
        {
            var match=new PrototypeMatch();var crowd=new List<PrototypeUnit>();
            for(int i=0;i<24;i++)crowd.Add(match.Spawn(i%2==0?UnitKind.Melee:UnitKind.Ranged,8+(i%6),13.5f+i/6));
            Assert.That(match.MoveGroup(crowd,31.5f,10.5f),Is.True);
            int maxOnRamp=0,moving=0,slowed=0;float done=-1;
            for(float t=0;t<15&&done<0;t+=.05f)
            {
                var before=new List<(float,float)>();foreach(var u in crowd)before.Add((u.X,u.Z));
                match.Tick(.05f);int onRamp=0;
                for(int i=0;i<crowd.Count;i++)
                {
                    var u=crowd[i];if(match.Map.Ramp((int)u.X,(int)u.Z))onRamp++;
                    if(!u.Moving)continue;moving++;
                    float dx=u.X-before[i].Item1,dz=u.Z-before[i].Item2;if(dx*dx+dz*dz<(u.Stats.Speed*.05f*.35f)*(u.Stats.Speed*.05f*.35f))slowed++;
                }
                maxOnRamp=System.Math.Max(maxOnRamp,onRamp);
                if(!crowd.Exists(u=>u.Moving))done=t;
            }
            Assert.That(done,Is.InRange(0f,7.8f),"The crowd should be down in under 8 s");
            Assert.That(maxOnRamp,Is.GreaterThanOrEqualTo(13),"Many units on the ramp at once means it is used across its width");
            Assert.That(slowed,Is.LessThan(moving*.08f),"Few steps spent waiting behind others");
            foreach(var u in crowd)AssertOnFreeGround(match,u);
        }
        [Test] public void RampsAreEnteredOnlyAtTheirEnds()
        {
            var match=new PrototypeMatch();var unit=match.Spawn(UnitKind.Ranged,25.5f,13.6f);
            // Next to the bottom of the ramp the drop is under one step, yet the side is a wall: the way leads round the end.
            Assert.That(match.CanTravel(25.5f,13.6f,25.5f,12.5f,unit.Stats.Radius),Is.False);
            Assert.That(match.Move(unit,25.5f,12.5f),Is.True);Assert.That(unit.WaypointCount,Is.GreaterThan(1));
            Run(match,3,()=>AssertOnFreeGround(match,unit));
            Assert.That(unit.X,Is.EqualTo(25.5f).Within(.01f));Assert.That(unit.Z,Is.EqualTo(12.5f).Within(.01f));
        }
        [Test] public void IdleUnitsPiledOnOneSpotSpreadAndAirOnlyAvoidsAir()
        {
            var match=new PrototypeMatch();var pile=new List<PrototypeUnit>();
            for(int i=0;i<4;i++)pile.Add(match.Spawn(UnitKind.Melee,30,30));
            var air=new List<PrototypeUnit>{match.Spawn(UnitKind.Air,30,30),match.Spawn(UnitKind.Air,30,30)};
            Run(match,2);
            AssertApart(pile);AssertApart(air);
            foreach(var u in pile)AssertOnFreeGround(match,u);
            var lone=match.Spawn(UnitKind.Melee,40.5f,40.5f);match.Spawn(UnitKind.Air,40.5f,40.5f);
            Run(match,1);
            Assert.That(lone.X,Is.EqualTo(40.5f));Assert.That(lone.Z,Is.EqualTo(40.5f));
        }
    }
}
