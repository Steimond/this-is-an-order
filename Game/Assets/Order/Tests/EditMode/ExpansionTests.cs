using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // Second bases: main buildings built by workers, their place by the deposits, the bot expanding.
    public sealed class ExpansionTests
    {
        const int Main = FactionBalance.Main, Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeMatch Game(FactionId player){var match=new PrototypeMatch(null,player,FactionId.Expedition);match.ToggleReveal();match.Metal=2000;return match;}

        [Test] public void EveryBaseSpotTakesAMainBuildingButNotRightByTheDeposits()
        {
            var match=new PrototypeMatch();
            Assert.That(match.Map.Bases.Count,Is.EqualTo(8));Assert.That(match.Map.Bases[0],Is.EqualTo((10,10)),"The player's start");
            var game=Game(FactionId.Expedition);
            foreach(var (x,z) in game.Map.Bases)if((x,z)!=(10,10))Assert.That(game.CanBuild(x,z,Main,out var reason),Is.True,$"{x},{z}: {reason}");
            Assert.That(game.CanBuild(7,7,Main,out var close),Is.False);Assert.That(close,Is.EqualTo("Слишком близко к залежам"));
        }
        [Test] public void HumansTakeASecondBaseAndHaulToTheNearestCommandNode()
        {
            var match=Game(FactionId.Expedition);var stats=match.Roster(PrototypeMatch.Player).Buildings[Main];
            int metal=match.Metal;
            var hq=Sites.Place(match,24,14,Main,finish:true);
            Assert.That(match.Metal,Is.EqualTo(metal-stats.Metal));Assert.That(match.SupplyLimit,Is.EqualTo(20));
            var miner=match.Spawn(UnitKind.Worker,27.5f,12);int before=match.MetalHarvested;
            Assert.That(match.Move(miner,27.5f,9.5f),Is.True);Assert.That(miner.HarvestTarget,Is.Not.Null);
            Run(match,20);
            Assert.That(match.MetalHarvested-before,Is.GreaterThanOrEqualTo(15),"Short trips to the new node, not up the ramp to the first one");
            Assert.That(miner.X,Is.GreaterThan(20),"It never climbed to the plateau");
            Assert.That(match.Train(hq,UnitKind.Worker),Is.True,"The new node trains workers");
        }
        [Test] public void MachineCoreStartsANetworkOfItsOwn()
        {
            var match=Game(FactionId.Resonance);
            Assert.That(match.CanBuild(26,16,0,out var reason),Is.False);Assert.That(reason,Is.EqualTo("Вне покрытия сети"));
            Assert.That(match.CanBuild(24,14,Main,out _),Is.True,"A core needs no coverage");
            int relays=match.RelaysLeft(PrototypeMatch.Player);
            Sites.Place(match,24,14,Main,finish:true);
            Assert.That(match.Covered(PrototypeMatch.Player,27,17),Is.True);Assert.That(match.CanBuild(26,18,0,out _),Is.True);
            Assert.That(match.RelaysLeft(PrototypeMatch.Player),Is.EqualTo(relays+3),"Each core allows more relays");
        }
        [Test] public void BotOfEitherFactionTakesASecondBaseFromTheSixthMinute([Values(FactionId.Expedition,FactionId.Resonance)] FactionId faction)
        {
            // The player is left alone and cannot be hurt, so the first wave does not end the match.
            var match=new PrototypeMatch(null,FactionId.Expedition,faction);match.AddEnemyBase();match.ToggleInvulnerable();match.StartBot();
            Run(match,340);
            Assert.That(match.Buildings.FindAll(b=>b.Owner==Enemy&&b.Kind==Main).Count,Is.EqualTo(1),"Not before the sixth minute");
            Run(match,120);
            var bases=match.Buildings.FindAll(b=>b.Owner==Enemy&&b.Kind==Main);
            Assert.That(bases.Count,Is.EqualTo(2));
            var second=bases.Find(b=>b.X!=51);Assert.That((second.X,second.Z),Is.EqualTo((37,47)),"The spot below its ramp");
            if(second.Ready>=1)Assert.That(match.Units.Exists(u=>u.Owner==Enemy&&u.HarvestTarget!=null&&u.HarvestTarget.X<40),Is.True,"Workers go to the new deposits");
        }
    }
}
