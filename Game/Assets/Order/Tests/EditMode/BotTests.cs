using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class BotTests
    {
        const int Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeMatch Game(){var match=new PrototypeMatch();match.AddTestSquad();match.AddEnemySquad();match.AddEnemyBase();return match;}

        [Test] public void EachSideSpendsItsOwnResources()
        {
            var match=Game();var factory=match.Buildings.Find(b=>b.Owner==Enemy&&b.Kind==1);
            int playerMinerals=match.Minerals,enemyMinerals=match.MineralsOf(Enemy);
            Assert.That(match.Train(factory,UnitKind.Ranged),Is.True);
            Assert.That(match.Minerals,Is.EqualTo(playerMinerals));Assert.That(match.MineralsOf(Enemy),Is.EqualTo(enemyMinerals-match.Balance.Unit(UnitKind.Ranged).Minerals));
            Assert.That(match.Build(48,44,0,Enemy),Is.True,"The bot builds without the player's vision");
            Assert.That(match.Minerals,Is.EqualTo(playerMinerals));
        }
        [Test] public void IdleBotDoesNothing()
        {
            var match=Game();int minerals=match.MineralsOf(Enemy),units=match.Units.Count;
            Run(match,30);
            Assert.That(match.MineralsOf(Enemy),Is.EqualTo(minerals));Assert.That(match.Units.Count,Is.EqualTo(units));
        }
        [Test] public void BotMinesBuildsSupplyAndGrowsAnArmy()
        {
            var match=Game();match.StartBot();int army=match.Units.FindAll(u=>u.Owner==Enemy&&u.Stats.Damage>0).Count;
            Run(match,150);
            var workers=match.Units.FindAll(u=>u.Owner==Enemy&&u.Kind==UnitKind.Worker);
            Assert.That(workers.Count,Is.GreaterThanOrEqualTo(10));
            Assert.That(workers.FindAll(w=>w.HarvestTarget!=null).Count,Is.GreaterThanOrEqualTo(workers.Count-2),"Workers keep mining");
            Assert.That(workers.FindAll(w=>w.HarvestTarget!=null&&w.HarvestTarget.Gas).Count,Is.EqualTo(2),"Two workers on gas");
            Assert.That(match.Buildings.FindAll(b=>b.Owner==Enemy&&b.Kind==0).Count,Is.GreaterThanOrEqualTo(3),"Supply nodes before supply runs out");
            Assert.That(match.Units.FindAll(u=>u.Owner==Enemy&&u.Stats.Damage>0).Count,Is.GreaterThan(army+3));
            foreach(var b in match.Buildings)if(b.Owner==Enemy)
                for(int z=b.Z;z<b.Z+b.Width;z++)for(int x=b.X;x<b.X+b.Width;x++)Assert.That(match.Map.Ramp(x,z),Is.False,"No building on a ramp");
            Assert.That(match.Waves,Is.Zero,"No attack before the fifth minute");
        }
        [Test] public void BotMeetsPlayerUnitsAtItsBase()
        {
            var match=Game();match.StartBot();Run(match,20);
            var raid=match.SpawnSquad(UnitKind.Ranged,3,49,49);foreach(var u in raid)u.Health=10000;
            bool defended=false;
            for(float t=0;t<15&&!defended;t+=.05f){match.Tick(.05f);defended=match.Units.Exists(u=>u.Owner==Enemy&&raid.Contains(u.Target));}
            Assert.That(defended,Is.True);
        }
        [Test] public void BotAttacksThePlayerAfterTheFifthMinuteAndWarns()
        {
            var match=Game();match.StartBot();int alerts=0;float last=-100,shortest=float.MaxValue;
            match.UnderAttack+=(x,z,building)=>{alerts++;if(building){shortest=System.Math.Min(shortest,match.Clock-last);last=match.Clock;}};
            Run(match,299);Assert.That(match.Waves,Is.Zero);
            Run(match,90);
            Assert.That(match.Waves,Is.GreaterThanOrEqualTo(1));Assert.That(alerts,Is.GreaterThan(0),"The wave reached the player");
            Assert.That(shortest,Is.GreaterThanOrEqualTo(10).Or.EqualTo(float.MaxValue),"Alerts about the base come at most every 10 s");
        }
    }
}
