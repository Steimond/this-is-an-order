using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // The humans' extractor: plasma is mined only through one, it stands only on a rift, and the rift is its owner's.
    public sealed class ExtractorTests
    {
        const int Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static PrototypeMatch Humans(FactionId enemy=FactionId.Expedition){var match=new PrototypeMatch(null,FactionId.Expedition,enemy);match.ToggleReveal();match.Metal=2000;return match;}
        static int Extractor(PrototypeMatch match)=>match.Roster(PrototypeMatch.Player).Extractor;

        [Test] public void HumansMinePlasmaOnlyThroughTheirFinishedExtractor()
        {
            var match=Humans();var rift=match.Map.ResourceAt(5,10);int kind=Extractor(match);
            var miner=match.Spawn(UnitKind.Worker,8.5f,13.5f);
            Assert.That(match.Move(miner,5.5f,10.5f),Is.True);
            Assert.That(miner.HarvestTarget,Is.Null,"Without an extractor the worker only walks up to the rift");Assert.That(match.Message,Does.Contain("экстрактор"));
            Assert.That(match.CanBuild(15,14,kind,out var reason),Is.False);Assert.That(reason,Is.EqualTo("Ставится только на разлом плазмы"));
            Assert.That(match.CanBuild(5,5,kind,out reason),Is.False,"Not on a metal vein either");
            Assert.That(match.CanBuild(5,10,kind,out reason),Is.True,reason);
            var extractor=Sites.Place(match,5,10,kind);
            Assert.That(extractor.Stats.Extracts,Is.True);
            Assert.That(match.Move(miner,5.5f,10.5f),Is.True);Assert.That(miner.HarvestTarget,Is.Null);Assert.That(match.Message,Is.EqualTo("Экстрактор ещё строится."));
            Assert.That(match.CanBuild(5,10,kind,out reason),Is.False);Assert.That(reason,Is.EqualTo("На разломе уже стоит экстрактор"));
            Run(match,(float)extractor.Stats.BuildSeconds+1);Assert.That((float)extractor.Ready,Is.EqualTo(1));
            int plasma=match.Plasma;
            Assert.That(match.Move(miner,5.5f,10.5f),Is.True);Assert.That(miner.HarvestTarget,Is.SameAs(rift));
            Run(match,20);
            Assert.That(match.Plasma,Is.GreaterThan(plasma),"Plasma comes through the extractor");
            Assert.That(match.PlasmaHarvested,Is.GreaterThan(0));
        }
        [Test] public void LosingTheExtractorStopsItsMiners()
        {
            var match=Humans();var extractor=Sites.Place(match,5,10,Extractor(match),finish:true);
            var miner=match.Spawn(UnitKind.Worker,8.5f,13.5f);
            Assert.That(match.Move(miner,5.5f,10.5f),Is.True);Run(match,6);
            extractor.Health=0;Run(match,8);
            Assert.That(miner.HarvestTarget,Is.Null,"No extractor, no plasma");
            int plasma=match.Plasma;Run(match,10);Assert.That(match.Plasma,Is.EqualTo(plasma));
        }
        [Test] public void TheRiftUnderAnExtractorIsItsOwnersAloneAndAWrappedRiftTakesNoExtractor()
        {
            var match=Humans(FactionId.Resonance);var rift=match.Map.ResourceAt(5,10);int kind=Extractor(match);
            var extractor=Sites.Place(match,5,10,kind,finish:true);
            var weaver=match.Spawn(UnitKind.Worker,8.5f,11.5f,Enemy);
            Assert.That(match.MiningBlocked(Enemy,rift),Is.EqualTo("Разлом занят экстрактором противника."));
            Assert.That(match.Move(weaver,5.5f,10.5f),Is.True);Assert.That(weaver.HarvestTarget,Is.Null,"A machine cannot wrap a rift under a human extractor");
            extractor.Health=0;Run(match,.1f);
            Assert.That(match.Move(weaver,5.5f,10.5f),Is.True);Assert.That(weaver.HarvestTarget,Is.SameAs(rift),"Machines wrap a free rift without any extractor");
            Run(match,3);
            Assert.That(match.CanBuild(5,10,kind,out var reason),Is.False);Assert.That(reason,Is.EqualTo("Разлом занят противником"));
        }
        [Test] public void MachinesHaveNoExtractorAndWrapRiftsAsTheyAre()
        {
            var match=new PrototypeMatch(null,FactionId.Resonance,FactionId.Expedition);
            Assert.That(match.Roster(PrototypeMatch.Player).Extractor,Is.EqualTo(-1));
            var weaver=match.Units.Find(u=>u.Kind==UnitKind.Worker);
            Assert.That(match.Move(weaver,5.5f,10.5f),Is.True);Assert.That(weaver.HarvestTarget,Is.SameAs(match.Map.ResourceAt(5,10)));
        }
        [Test] public void BotPutsAnExtractorOnItsRiftAndMinesPlasmaThroughIt()
        {
            var match=new PrototypeMatch();match.AddTestSquad();match.AddEnemyBase();match.StartBot();
            Run(match,150);
            var extractor=match.Buildings.Find(b=>b.Owner==Enemy&&b.Stats.Extracts);
            Assert.That(extractor,Is.Not.Null,"The bot builds an extractor");Assert.That((float)extractor.Ready,Is.EqualTo(1));
            Assert.That((extractor.X,extractor.Z),Is.EqualTo((58,53)),"On the rift of its base");
            var onPlasma=match.Units.FindAll(u=>u.Owner==Enemy&&u.HarvestTarget!=null&&u.HarvestTarget.Plasma);
            Assert.That(onPlasma.Count,Is.EqualTo(2));
            foreach(var w in onPlasma)Assert.That(match.ExtractorOn(w.HarvestTarget),Is.SameAs(extractor));
            Assert.That(match.PlasmaOf(Enemy),Is.GreaterThan(0));
        }
    }
}
