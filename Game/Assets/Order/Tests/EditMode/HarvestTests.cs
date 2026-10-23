using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class HarvestTests
    {
        static ResourceDeposit Mineral(PrototypeMatch match,int x)=>match.Map.Resources.Find(r=>!r.Gas&&r.X==x&&r.Z==5);

        [Test] public void WorkerCarriesEachLoadToTheCommandNode()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];var deposit=Mineral(match,11);int start=match.Minerals;
            Assert.That(match.Move(worker,deposit.X+.5f,deposit.Z+.5f),Is.True);
            bool carried=false,mined=false;int deliveries=0,last=start;
            for(float t=0;t<20;t+=.05f)
            {
                match.Tick(.05f);
                mined|=worker.Mining;
                if(worker.Carrying>0){carried=true;Assert.That(match.Minerals,Is.EqualTo(last),"The load counts only when delivered");}
                if(match.Minerals!=last){Assert.That(match.Minerals-last,Is.EqualTo(worker.Stats.HarvestAmount));deliveries++;last=match.Minerals;
                    var hq=match.Buildings[0];Assert.That(PrototypeMatch.Gap(worker,hq),Is.LessThan(.8f),"Delivered at the command node");}
            }
            Assert.That(mined,Is.True);Assert.That(carried,Is.True);
            Assert.That(deliveries,Is.InRange(3,6),"Trips of about four seconds");
            Assert.That(deposit.Remaining,Is.EqualTo(1500-(match.Minerals-start)-worker.Carrying));
        }
        [Test] public void OneWorkerMinesADepositAtATimeAndOthersMoveToFreeNeighbours()
        {
            var match=new PrototypeMatch();var hq=match.Buildings[0];match.Minerals=500;
            for(int i=0;i<2;i++)Assert.That(match.Train(hq,UnitKind.Worker),Is.True);
            for(float t=0;t<25;t+=.05f)match.Tick(.05f);
            Assert.That(match.Units.Count,Is.EqualTo(3));
            var deposit=Mineral(match,11);
            foreach(var w in match.Units)Assert.That(match.Move(w,deposit.X+.5f,deposit.Z+.5f),Is.True);
            int maxMining=0;
            for(float t=0;t<20;t+=.05f)
            {
                match.Tick(.05f);int mining=0;
                foreach(var w in match.Units)if(w.Mining&&w.HarvestTarget==deposit)mining++;
                maxMining=System.Math.Max(maxMining,mining);
            }
            Assert.That(maxMining,Is.EqualTo(1),"Never two workers mining the same deposit");
            var targets=new System.Collections.Generic.HashSet<ResourceDeposit>();foreach(var w in match.Units)targets.Add(w.HarvestTarget);
            Assert.That(targets.Count,Is.GreaterThan(1),"Busy workers spread to the neighbouring crystals");
        }
        [Test] public void DepletedDepositSendsTheWorkerToTheNextOne()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];var deposit=Mineral(match,11);deposit.Remaining=5;
            Assert.That(match.Move(worker,deposit.X+.5f,deposit.Z+.5f),Is.True);
            for(float t=0;t<12;t+=.05f)match.Tick(.05f);
            Assert.That(deposit.Remaining,Is.Zero);Assert.That(match.Map.ResourceAt(11,5),Is.Null);
            Assert.That(worker.HarvestTarget,Is.Not.Null);Assert.That(worker.HarvestTarget,Is.Not.SameAs(deposit));Assert.That(worker.HarvestTarget.Gas,Is.False);
        }
        [Test] public void WithoutACommandNodeTheWorkerKeepsItsLoad()
        {
            var match=new PrototypeMatch();var worker=match.Units[0];var deposit=Mineral(match,11);
            match.Buildings[0].Health=0;
            Assert.That(match.Move(worker,deposit.X+.5f,deposit.Z+.5f),Is.True);
            for(float t=0;t<10;t+=.05f)match.Tick(.05f);
            Assert.That(worker.Carrying,Is.EqualTo(worker.Stats.HarvestAmount));Assert.That(worker.HarvestTarget,Is.Null);Assert.That(match.Minerals,Is.EqualTo(500));
        }
    }
}
