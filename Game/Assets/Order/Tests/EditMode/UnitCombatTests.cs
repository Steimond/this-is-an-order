using System.Collections.Generic;
using NUnit.Framework;
using Order.Content;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    public sealed class UnitCombatTests
    {
        const int Enemy = PrototypeMatch.Enemy;
        static void Run(PrototypeMatch match,float seconds,System.Action check=null){match.UpdateVision();for(float t=0;t<seconds;t+=.05f){match.Tick(.05f);check?.Invoke();}}
        static PrototypeMatch Match()=>new PrototypeMatch();

        [Test] public void TargetsFollowTheWeaponRules()
        {
            var match=Match();
            var melee=match.Spawn(UnitKind.Melee,30,30);var ranged=match.Spawn(UnitKind.Ranged,31,30);var air=match.Spawn(UnitKind.Air,32,30);var worker=match.Units[0];
            var enemyGround=match.Spawn(UnitKind.Melee,30,33,Enemy);var enemyAir=match.Spawn(UnitKind.Air,31,33,Enemy);
            Assert.That(match.CanAttack(melee,enemyGround),Is.True);Assert.That(match.CanAttack(melee,enemyAir),Is.False);
            Assert.That(match.CanAttack(ranged,enemyGround),Is.True);Assert.That(match.CanAttack(ranged,enemyAir),Is.True);
            Assert.That(match.CanAttack(air,enemyGround),Is.True);Assert.That(match.CanAttack(air,enemyAir),Is.False);
            Assert.That(match.CanAttack(worker,enemyGround),Is.False);Assert.That(match.CanAttack(ranged,melee),Is.False,"No friendly fire");
        }
        [Test] public void IdleRiflemenShootAnEnemyInRangeAndItFightsBackUntilItDies()
        {
            var match=Match();var squad=new List<PrototypeUnit>();
            for(int i=0;i<3;i++)squad.Add(match.Spawn(UnitKind.Ranged,30+i*.8f,30));
            var enemy=match.Spawn(UnitKind.Melee,31,35.5f,Enemy);
            var shots=new List<(PrototypeUnit,PrototypeUnit)>();var dead=new List<PrototypeUnit>();
            match.Attacked+=(a,t)=>shots.Add((a,t));match.Died+=dead.Add;
            Run(match,12);
            Assert.That(dead,Has.Member(enemy));Assert.That(match.Units,Has.No.Member(enemy));
            Assert.That(shots.Exists(s=>s.Item1==enemy),Is.True,"The enemy should have answered the fire");
            foreach(var u in squad)if(u.Alive){Assert.That(u.Target,Is.Null);Assert.That(u.Order,Is.EqualTo(UnitOrder.Idle));}
        }
        [Test] public void ShotsFollowTheCooldown()
        {
            var match=Match();var rifleman=match.Spawn(UnitKind.Ranged,30,30);var dummy=match.Spawn(UnitKind.Air,30,34,Enemy);
            dummy.Health=10000;int shots=0;match.Attacked+=(a,t)=>{if(a==rifleman)shots++;};
            Run(match,4.25f);
            // A shot at once, then one every .85 s.
            Assert.That(shots,Is.EqualTo(5).Within(1));
            Assert.That(dummy.Health,Is.EqualTo(10000-shots*rifleman.Stats.Damage));
        }
        [Test] public void PlainMoveIgnoresEnemiesUntilArrival()
        {
            var match=Match();var rifleman=match.Spawn(UnitKind.Ranged,22,30);var enemy=match.Spawn(UnitKind.Ranged,34,33,Enemy);
            enemy.Health=10000;bool firedOnTheWay=false;float arrivedX=-1;
            match.Attacked+=(a,t)=>{if(a==rifleman&&rifleman.Moving)firedOnTheWay=true;};
            Assert.That(match.Move(rifleman,40f,30f),Is.True);Assert.That(rifleman.Order,Is.EqualTo(UnitOrder.Move));
            Run(match,6,()=>{if(arrivedX<0&&rifleman.Order==UnitOrder.Idle)arrivedX=(float)rifleman.X;});
            Assert.That(firedOnTheWay,Is.False);
            Assert.That(arrivedX,Is.EqualTo(40).Within(.05f),"The move should end on its point and leave the unit idle");
        }
        [Test] public void AttackMoveFightsOnTheWayAndThenGoesOn()
        {
            var match=Match();var group=new List<PrototypeUnit>();
            for(int i=0;i<3;i++)group.Add(match.Spawn(UnitKind.Ranged,20,28+i*.9f));
            var enemy=match.Spawn(UnitKind.Ranged,32,34,Enemy);bool fought=false;
            match.Attacked+=(a,t)=>{if(t==enemy)fought=true;};
            Assert.That(match.AttackMoveGroup(group,44,29),Is.True);
            Assert.That(group[0].Order,Is.EqualTo(UnitOrder.AttackMove));
            Run(match,16);
            Assert.That(fought,Is.True);Assert.That(enemy.Alive,Is.False);
            foreach(var u in group){Assert.That(u.Moving,Is.False);Assert.That((float)u.X,Is.GreaterThan(41.5f),$"#{u.Id} did not go on to the point");Assert.That(u.Order,Is.EqualTo(UnitOrder.Idle));}
        }
        [Test] public void AttackOrderPursuesTheChosenTarget()
        {
            var match=Match();var melee=match.Spawn(UnitKind.Melee,30,30);
            var near=match.Spawn(UnitKind.Ranged,30,32.5f,Enemy);var far=match.Spawn(UnitKind.Ranged,34,36,Enemy);
            near.Health=far.Health=10000;PrototypeUnit first=null;match.UpdateVision();
            match.Attacked+=(a,t)=>{if(a==melee&&first==null)first=t;};
            Assert.That(match.Attack(melee,far),Is.True);
            Run(match,3);
            Assert.That(first,Is.SameAs(far));Assert.That(melee.Order,Is.EqualTo(UnitOrder.Attack));
        }
        [Test] public void MeleeOrderedOntoAnAirTargetOnlyWalksUpToIt()
        {
            var match=Match();var melee=match.Spawn(UnitKind.Melee,30,30);var air=match.Spawn(UnitKind.Air,30,36,Enemy);match.UpdateVision();
            Assert.That(match.AttackGroup(new[]{melee},air),Is.True);
            Assert.That(melee.Order,Is.EqualTo(UnitOrder.Move));Assert.That(match.Message,Does.Contain("не бьют"));
        }
        [Test] public void HoldingUnitNeitherChasesNorGetsPushed()
        {
            var match=Match();var guard=match.Spawn(UnitKind.Melee,30,30);var enemy=match.Spawn(UnitKind.Ranged,30,34,Enemy);enemy.Health=10000;
            match.Hold(guard);
            var passer=match.Spawn(UnitKind.Melee,26,30);match.Move(passer,34f,30f);float passedX=-1;
            Run(match,4,()=>{if(passedX<0&&passer.Order==UnitOrder.Idle)passedX=(float)passer.X;});
            Assert.That((float)guard.X,Is.EqualTo(30));Assert.That((float)guard.Z,Is.EqualTo(30));Assert.That(guard.Order,Is.EqualTo(UnitOrder.Hold));
            Assert.That(guard.Health,Is.LessThan(guard.Stats.Health),"The enemy rifleman should be shooting the guard");
            // The ally walks round the guard to its point; then, idle beside a fight, it joins in.
            Assert.That(passedX,Is.EqualTo(34).Within(.05f));
        }
        [Test] public void EnemiesAreNotShovedAside()
        {
            var match=Match();var enemy=match.Spawn(UnitKind.Worker,30,30,Enemy);
            var worker=match.Units[0];worker.X=26;worker.Z=30;
            match.Move(worker,34,30);
            Run(match,3);
            Assert.That((float)enemy.X,Is.EqualTo(30).Within(.01f));Assert.That((float)enemy.Z,Is.EqualTo(30).Within(.01f));
        }
        [Test] public void HighGroundIsInvisibleFromBelow()
        {
            var match=Match();var rifleman=match.Spawn(UnitKind.Ranged,45.5f,41.5f);var sniper=match.Spawn(UnitKind.Ranged,45.5f,45.5f,Enemy);
            match.UpdateVision();
            Assert.That(match.SeenBy(PrototypeMatch.Player,sniper),Is.False);Assert.That(match.SeenBy(Enemy,rifleman),Is.True);
            Assert.That(match.Attack(rifleman,sniper),Is.False);
            Run(match,3);
            Assert.That(rifleman.Health,Is.LessThan(rifleman.Stats.Health));Assert.That(sniper.Health,Is.EqualTo(sniper.Stats.Health));
            // Air units are seen over a cliff.
            var gunship=match.Spawn(UnitKind.Air,45.5f,45.5f,Enemy);match.UpdateVision();
            Assert.That(match.SeenBy(PrototypeMatch.Player,gunship),Is.True);
        }
        [Test] public void EnemySquadStandsStillUntilProvoked()
        {
            var match=Match();match.AddTestSquad();match.AddEnemySquad();
            var enemies=match.Units.FindAll(u=>u.Owner==Enemy);
            Assert.That(enemies.Count,Is.EqualTo(6));
            var start=new List<(float,float)>();foreach(var e in enemies)start.Add(((float)e.X,(float)e.Z));
            int supply=0;foreach(var u in match.Units)if(u.Owner==PrototypeMatch.Player)supply+=u.Stats.Supply;
            Assert.That(match.SupplyUsed,Is.EqualTo(supply));
            Run(match,3);
            for(int i=0;i<enemies.Count;i++){Assert.That((float)enemies[i].X,Is.EqualTo(start[i].Item1).Within(.3f));Assert.That((float)enemies[i].Z,Is.EqualTo(start[i].Item2).Within(.3f));}
            foreach(var e in enemies)Assert.That(match.SeenBy(PrototypeMatch.Player,e),Is.False,"The enemy squad starts in the fog");
        }
    }
}
