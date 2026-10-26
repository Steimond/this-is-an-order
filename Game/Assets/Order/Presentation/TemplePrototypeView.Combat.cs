using System;
using System.Collections.Generic;
using Order.Content;
using Order.Contracts;
using Order.Balance;
using Order.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    public sealed partial class TemplePrototypeView
    {
        // Points on a unit's model: the top (health bar), the middle (where hits land) and the muzzle of a ground unit.
        static Vector3 Top(PrototypeUnit unit,UnitView view)=>unit.Stats.Flying?view.Body.position+Vector3.up*.35f:view.Root.transform.position+Vector3.up*view.Model.Height*view.Model.Scale;
        static Vector3 Center(PrototypeUnit unit,UnitView view)=>unit.Stats.Flying?view.Body.position:view.Root.transform.position+Vector3.up*view.Model.Height*view.Model.Scale*.5f;
        static Vector3 Muzzle(PrototypeUnit unit,UnitView view)=>view.Root.transform.position+Vector3.up*(float)view.Model.Height*view.Model.Scale*.6f+view.Root.transform.forward*((float)unit.Stats.Radius+.12f);
        // Strikes and deaths from the last simulation steps, shown where the player can see them: a rifleman fires a tracer,
        // the gunship a pair from its wings, an assault unit slashes; a dead unit explodes and leaves a scorch mark.
        void ShowCombat()
        {
            foreach(var (attacker,target) in strikes)
            {
                if(!unitViews.TryGetValue(attacker.Id,out var a)||!unitViews.TryGetValue(target.Id,out var t))continue;
                if(!Match.SeenBy(PrototypeMatch.Player,attacker)&&!Match.SeenBy(PrototypeMatch.Player,target))continue;
                a.Kick=1;
                var hit=Center(target,t)+UnityEngine.Random.insideUnitSphere*(float)target.Stats.Radius*.4f;
                Shot(attacker,a,hit);
            }
            strikes.Clear();
            foreach(var (attacker,target) in buildingStrikes)
            {
                if(!unitViews.TryGetValue(attacker.Id,out var a)||!buildingViews.TryGetValue(target,out var view))continue;
                if(!Match.SeenBy(PrototypeMatch.Player,attacker)&&!Match.SeenBy(PrototypeMatch.Player,target))continue;
                a.Kick=1;
                // Hits land on the side facing the attacker, somewhere up the wall.
                var center=view.Model.GetComponent<MeshRenderer>().bounds.center;var toward=new Vector3((float)attacker.X-center.x,0,(float)attacker.Z-center.z).normalized;
                var hit=center+toward*target.Width*.42f+new Vector3(UnityEngine.Random.Range(-.4f,.4f),UnityEngine.Random.Range(-.3f,.4f),UnityEngine.Random.Range(-.4f,.4f));
                Shot(attacker,a,hit);
            }
            buildingStrikes.Clear();
            // A destroyed building goes up in a chain of blasts across its footprint.
            foreach(var b in ruins)
            {
                if(!Match.SeenBy(PrototypeMatch.Player,b)&&b.Owner!=PrototypeMatch.Player)continue;
                float ground=(float)Match.Map.Height(b.X,b.Z),h=(float)b.Stats.Height;
                for(int i=0;i<3;i++)
                {
                    var at=new Vector3(b.X+b.Width*(.3f+.2f*i),ground+h*.5f,b.Z+b.Width*(i==1?.7f:.35f));
                    combat.Explosion(at,ground,b.Width*.9f);
                }
            }
            if(ruins.Count>0){ruins.Clear();SyncBuildings();hud?.Refresh();}
            foreach(var unit in deaths)
                if(unitViews.TryGetValue(unit.Id,out var view)&&Match.SeenBy(PrototypeMatch.Player,unit))
                {
                    // A machine taken apart leaves no wreck: a last cold flash where its nanites flow away.
                    if(unit.Disassembling){combat.Flash(Center(unit,view),new Color(.62f,.86f,1)*2.6f,Mathf.Max(.5f,(float)unit.Stats.Radius*2),.5f,2.2f);continue;}
                    // A worker spent on a building melts into it. The building's view comes at once, not with the next refresh,
                    // so the puddle grows under the worker from its first frame.
                    if(unit.AbsorbedInto!=null){StartMelt(unit,view);SyncBuildings();continue;}
                    combat.Explosion(Center(unit,view),(float)Match.HeightAt((float)unit.X,(float)unit.Z),unit.Stats.Flying?1.3f:Mathf.Max(1,(float)unit.Stats.Radius*2.6f));
                }
            deaths.Clear();
        }
        // How a strike looks: melee slashes; the gunship fires a pair of tracers from its wings and the interceptor a pair of
        // missiles from its wingtips; the tank's gun flashes and throws a heavy tracer; the howitzer's barrel flashes and
        // the shell bursts on the target a moment later, as wide as its splash; other guns fire a tracer.
        void Shot(PrototypeUnit attacker,UnitView a,Vector3 hit)
        {
            var stats=attacker.Stats;
            if(stats.AttackRange<1||stats.Leap)combat.Slash(hit,hit-Center(attacker,a),new Color(.75f,.9f,1));
            else if(stats.Flying&&!stats.HitsGround)
                for(int side=-1;side<=1;side+=2)combat.Tracer(a.Body.TransformPoint(new Vector3(side*.56f,-.04f,.1f)),hit+a.Body.right*side*.08f,new Color(.75f,.9f,1),.05f);
            else if(stats.Flying)
                for(int side=-1;side<=1;side+=2)combat.Tracer(a.Body.TransformPoint(new Vector3(side*.34f,-.1f,.32f)),hit+a.Body.right*side*.12f,new Color(1,.6f,.28f),.06f);
            else if(stats.Siege)
            {
                var muzzle=a.Gun!=null?a.Gun.TransformPoint(new Vector3(0,0,a.Model.GunLength)):Muzzle(attacker,a);
                combat.Flash(muzzle,new Color(3.2f,1.8f,.7f),.55f,.25f,2.2f);
                float flight=Mathf.Clamp(Vector3.Distance(muzzle,hit)/22,.15f,.6f),burst=Mathf.Max(.8f,(float)stats.Splash*1.5f);
                combat.Flash(hit,new Color(3.2f,1.6f,.55f),burst*.6f,.45f,3.2f,flight);
                combat.Flash(hit+Vector3.up*.2f,new Color(.35f,.33f,.3f),burst*.7f,1.1f,2.2f,flight+.08f);
            }
            else if(stats.Ramp>1)
            {
                // The emitter's beam thickens and brightens as it builds up on one target.
                float focus=(float)Match.Focus(attacker);
                var lens=a.Body.TransformPoint(new Vector3(0,.8f,.22f));
                combat.Tracer(lens,hit,Color.Lerp(new Color(.62f,.86f,1),Color.white,focus*.6f),.035f+.075f*focus);
                if(focus>.6f)combat.Flash(hit,NaniteGlow*(1.5f+focus),.12f+.12f*focus,.12f,1.6f);
            }
            else if(attacker.Kind==UnitKind.Tank)
            {
                var muzzle=a.Body.TransformPoint(new Vector3(0,.445f,.78f));
                combat.Flash(muzzle,new Color(3,1.8f,.7f),.35f,.18f,2);
                combat.Tracer(muzzle,hit,new Color(1,.78f,.42f),.11f);
            }
            else combat.Tracer(Muzzle(attacker,a),hit,new Color(1,.86f,.5f));
        }
    }
}
