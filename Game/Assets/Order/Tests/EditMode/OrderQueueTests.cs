using System.Collections.Generic;
using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class OrderQueueTests
    {
        static void Run(PrototypeMatch match,float seconds,System.Action check=null){match.UpdateVision();for(float t=0;t<seconds;t+=.05f){match.Tick(.05f);check?.Invoke();}}

        [Test] public void QueuedMovesAreWalkedInTurn()
        {
            var match=new PrototypeMatch();var unit=match.Spawn(UnitKind.Ranged,24,30);var one=new List<PrototypeUnit>{unit};
            Assert.That(match.MoveGroup(one,30,30),Is.True);
            Assert.That(match.QueueMoveGroup(one,30,36),Is.True);Assert.That(match.QueueMoveGroup(one,36,36),Is.True);
            Assert.That(unit.Queued.Count,Is.EqualTo(2));
            bool passedFirst=false,passedSecond=false;
            Run(match,8,()=>{
                if(System.Math.Abs(unit.X-30)<.05f&&System.Math.Abs(unit.Z-30)<.05f)passedFirst=true;
                if(passedFirst&&System.Math.Abs(unit.X-30)<.05f&&System.Math.Abs(unit.Z-36)<.05f)passedSecond=true;});
            Assert.That(passedFirst,Is.True);Assert.That(passedSecond,Is.True);
            Assert.That(unit.X,Is.EqualTo(36).Within(.05f));Assert.That(unit.Z,Is.EqualTo(36).Within(.05f));Assert.That(unit.Queued,Is.Empty);
        }
        [Test] public void IdleUnitStartsAQueuedOrderAtOnceAndAPlainOrderClearsTheQueue()
        {
            var match=new PrototypeMatch();var unit=match.Spawn(UnitKind.Melee,24,30);var one=new List<PrototypeUnit>{unit};
            Assert.That(match.QueueMoveGroup(one,28,30),Is.True);
            Assert.That(unit.Moving,Is.True);Assert.That(unit.Queued,Is.Empty);
            match.QueueMoveGroup(one,28,34);match.QueueAttackMoveGroup(one,32,34);
            Assert.That(unit.Queued.Count,Is.EqualTo(2));
            match.MoveGroup(one,24,30);Assert.That(unit.Queued,Is.Empty);
            match.QueueMoveGroup(one,28,34);match.Stop(unit);Assert.That(unit.Queued,Is.Empty);
        }
        [Test] public void QueuedAttackStartsWhenTheMoveEndsAndDeadTargetsAreSkipped()
        {
            var match=new PrototypeMatch();var rifleman=match.Spawn(UnitKind.Ranged,24,30);var one=new List<PrototypeUnit>{rifleman};
            var first=match.Spawn(UnitKind.Ranged,32,33,PrototypeMatch.Enemy);var second=match.Spawn(UnitKind.Ranged,33,33,PrototypeMatch.Enemy);
            first.Health=second.Health=1000;match.UpdateVision();
            Assert.That(match.MoveGroup(one,28,30),Is.True);
            Assert.That(match.EnqueueGroup(one,first),Is.True);Assert.That(match.EnqueueGroup(one,second),Is.True);
            first.Health=0;
            Run(match,3);
            Assert.That(rifleman.Order,Is.EqualTo(UnitOrder.Attack));Assert.That(rifleman.Target,Is.SameAs(second),"The dead target is skipped");
        }
    }
}
