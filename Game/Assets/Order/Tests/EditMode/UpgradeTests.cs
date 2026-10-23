using NUnit.Framework;
using Order.Content;
using Order.Contracts;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // Weapon and armour upgrades: researched in the workshop (humans) or the firmware node (machines).
    public sealed class UpgradeTests
    {
        const int Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds){for(float t=0;t<seconds-1e-4f;t+=.05f)match.Tick(.05f);}
        static (PrototypeMatch match,PrototypeBuilding lab) Lab(FactionId faction=FactionId.Expedition)
        {
            var match=new PrototypeMatch(null,faction,FactionId.Expedition);match.Metal=3000;match.Plasma=3000;
            var lab=Sites.Place(match,15,14,match.Roster(PrototypeMatch.Player).Research,finish:true);
            Assert.That(lab.Stats.Researches,Is.True);
            return (match,lab);
        }

        [Test] public void ResearchIsPaidAtTheStartAndRaisesTheLevelWhenDone()
        {
            var (match,lab)=Lab();int metal=match.Metal,plasma=match.Plasma;var (cost,gas,seconds)=PrototypeMatch.UpgradeCost(1);
            Assert.That((cost,gas,seconds),Is.EqualTo((100,100,60f)));
            Assert.That(match.Research(lab,Upgrade.Weapons),Is.True);
            Assert.That(match.Metal,Is.EqualTo(metal-cost));Assert.That(match.Plasma,Is.EqualTo(plasma-gas));Assert.That(lab.Researching,Is.EqualTo(Upgrade.Weapons));
            Run(match,seconds-1);Assert.That(match.Level(PrototypeMatch.Player,Upgrade.Weapons),Is.Zero);
            Run(match,1.1f);
            Assert.That(match.Level(PrototypeMatch.Player,Upgrade.Weapons),Is.EqualTo(1));Assert.That(lab.Researching,Is.Null);
            Assert.That(match.Level(Enemy,Upgrade.Weapons),Is.Zero,"Each side has its own levels");
        }
        [Test] public void WeaponsAddATenthOfTheDamageArmourTakesAPointOff()
        {
            var (match,lab)=Lab();match.ToggleFastBuild();
            var rifleman=match.Spawn(UnitKind.Ranged,13.5f,17.5f);var enemy=match.Spawn(UnitKind.Melee,40.5f,40.5f,Enemy);
            Assert.That(match.Damage(rifleman),Is.EqualTo(7));
            for(int level=1;level<=2;level++){Assert.That(match.Research(lab,Upgrade.Weapons),Is.True);Run(match,PrototypeMatch.UpgradeCost(level).seconds/PrototypeMatch.FastFactor+.2f);}
            Assert.That(match.Damage(rifleman),Is.EqualTo(9),"+1 a level for a 7-damage rifle");
            Assert.That(match.Armor(enemy),Is.Zero);
            Assert.That(match.Research(lab,Upgrade.Armor),Is.True);Run(match,PrototypeMatch.UpgradeCost(1).seconds/PrototypeMatch.FastFactor+.2f);
            Assert.That(match.Armor(rifleman),Is.EqualTo(1));Assert.That(match.Armor(enemy),Is.Zero,"Only the researching side");
            // A blow from the enemy's assault unit loses a point on the upgraded rifleman.
            enemy.X=rifleman.X+.75f;enemy.Z=rifleman.Z;match.UpdateVision();Assert.That(match.Attack(enemy,rifleman),Is.True);
            int health=rifleman.Health;for(int i=0;i<60&&rifleman.Health==health;i++)match.Tick(.05f);
            Assert.That(health-rifleman.Health,Is.EqualTo(match.Damage(enemy)-1));
        }
        [Test] public void OneResearchAtATimeCancelRefundsAndThreeLevelsAtMost()
        {
            var (match,lab)=Lab();match.ToggleFastBuild();int metal=match.Metal;
            Assert.That(match.Research(lab,Upgrade.Armor),Is.True);
            Assert.That(match.Research(lab,Upgrade.Weapons),Is.False);Assert.That(match.Message,Does.Contain("Уже идёт"));
            Assert.That(match.CancelResearch(lab),Is.True);Assert.That(match.Metal,Is.EqualTo(metal));Assert.That(lab.Researching,Is.Null);
            for(int level=1;level<=PrototypeMatch.MaxUpgrade;level++){Assert.That(match.Research(lab,Upgrade.Armor),Is.True,match.Message);Run(match,PrototypeMatch.UpgradeCost(level).seconds/PrototypeMatch.FastFactor+.2f);}
            Assert.That(match.Level(PrototypeMatch.Player,Upgrade.Armor),Is.EqualTo(3));
            Assert.That(match.ResearchBlocked(lab,Upgrade.Armor),Does.Contain("высший уровень"));
        }
        [Test] public void FirmwareNodeResearchesOnlyInsideTheNetwork()
        {
            var (match,lab)=Lab(FactionId.Resonance);
            Assert.That(lab.Stats.Name,Is.EqualTo("Узел прошивки"));
            Assert.That(match.Research(lab,Upgrade.Weapons),Is.True);
            match.Buildings.Find(b=>b.Kind==FactionBalance.Main).Health=0;Run(match,PrototypeMatch.NetworkGrace+.2f);
            float time=lab.ResearchTime;Run(match,2);
            Assert.That(lab.ResearchTime,Is.EqualTo(time),"No research without the network");
            Assert.That(match.ResearchBlocked(lab,Upgrade.Armor),Is.EqualTo("Нет сигнала сети."));
        }
    }
}
