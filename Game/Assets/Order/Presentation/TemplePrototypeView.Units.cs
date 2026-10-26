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
        sealed class UnitView
        {
            public GameObject Root;
            public Transform Body, Shadow, Gun;
            public Transform[] Legs, Rotors;
            public UnitModel Model;
            public LineRenderer Beam, Nanites;
            // A chunk of ore or a plasma capsule carried home.
            public GameObject Cargo, Cocoon;
            public int CocoonSeen;
            // Kick runs from 1 to 0 after a strike: a melee lunge or a recoil; Sparks counts down to the next welding spark.
            public float Phase, Height, Kick, Sparks;
            // Machines: the next mending glint, and the renderers cut while the unit is taken apart.
            public float Mend;
            public MeshRenderer[] Renderers;
            public bool Dissolving;
            // Which puppets it may be swapped with in the pool: faction, kind and whether it wears the player's colours.
            public (FactionId faction,UnitKind kind,bool own) Look;
        }
        // Puppets of units that are gone, hidden and kept for new units of the same look: a puppet is a dozen GameObjects,
        // and a battle loses and trains units every second.
        readonly Dictionary<(FactionId,UnitKind,bool),Stack<UnitView>> idlePuppets=new Dictionary<(FactionId,UnitKind,bool),Stack<UnitView>>();
        UnitView CreateUnitView(PrototypeUnit unit)
        {
            var key=(Match.FactionOf(unit.Owner),unit.Kind);
            if(!unitModels.TryGetValue(key,out var model))
            {
                model=key.Item1==FactionId.Resonance?MachineModels.Unit(unit.Kind):MatchModels.Unit(unit.Kind);unitModels[key]=model;
                scenery.Own(model.Body);if(model.Leg!=null)scenery.Own(model.Leg);if(model.Rotor!=null)scenery.Own(model.Rotor);if(model.Gun!=null)scenery.Own(model.Gun);
            }
            var look=(key.Item1,unit.Kind,unit.Owner==PrototypeMatch.Player);
            float height=(float)Match.HeightAt((float)unit.X,(float)unit.Z);
            // Enemies start facing south-west, toward the player's base.
            var facing=Quaternion.Euler(0,unit.Owner==PrototypeMatch.Player?180:225,0);
            if(idlePuppets.TryGetValue(look,out var idle)&&idle.Count>0)
            {
                var old=idle.Pop();
                var reused=new UnitView{Model=model,Root=old.Root,Body=old.Body,Legs=old.Legs,Rotors=old.Rotors,Gun=old.Gun,Shadow=old.Shadow,Height=height,Look=look};
                reused.Root.name=unit.Stats.Name+" #"+unit.Id;
                reused.Root.transform.SetPositionAndRotation(new Vector3((float)unit.X,height,(float)unit.Z),facing);
                reused.Body.localPosition=Vector3.up*model.Hover;reused.Body.localRotation=Quaternion.identity;
                foreach(var leg in reused.Legs)leg.localRotation=Quaternion.identity;
                if(reused.Gun!=null)reused.Gun.localRotation=Quaternion.identity;
                reused.Root.SetActive(true);if(reused.Shadow!=null)reused.Shadow.gameObject.SetActive(true);
                return reused;
            }
            var view=new UnitView{Model=model,Root=new GameObject(unit.Stats.Name+" #"+unit.Id),Height=height,Look=look};
            view.Root.transform.SetParent(worldRoot.transform);view.Root.transform.localScale=Vector3.one*model.Scale;
            view.Root.transform.SetPositionAndRotation(new Vector3((float)unit.X,height,(float)unit.Z),facing);
            var material=unit.Owner==PrototypeMatch.Player?unitMaterial:enemyMaterial;
            view.Body=scenery.Model("Body",model.Body,material,view.Root.transform).transform;view.Body.localPosition=Vector3.up*model.Hover;
            view.Legs=new Transform[model.Hips.Length];
            for(int i=0;i<view.Legs.Length;i++){view.Legs[i]=scenery.Model("Leg",model.Leg,material,view.Body).transform;view.Legs[i].localPosition=model.Hips[i];}
            view.Rotors=new Transform[model.Rotors.Length];
            for(int i=0;i<view.Rotors.Length;i++){view.Rotors[i]=scenery.Model("Rotor",model.Rotor,material,view.Body).transform;view.Rotors[i].localPosition=model.Rotors[i];}
            if(model.Gun!=null){view.Gun=scenery.Model("Gun",model.Gun,material,view.Body).transform;view.Gun.localPosition=model.GunPivot;}
            if(unit.Stats.Flying){view.Shadow=scenery.Model("Shadow",shadowMesh,shadowMaterial,worldRoot.transform,false).transform;view.Shadow.localScale=Vector3.one*(float)unit.Stats.Radius*3.2f;}
            return view;
        }
        // Walkers swing their legs with the distance covered, air units hover and bank with spinning rotors, the drone floats.
        // Units turn toward their movement or their target; a harvesting worker faces its deposit and draws a beam.
        // Enemies out of sight are hidden. Damaged, selected and hovered units show a health bar.
        void AnimateUnits()
        {
            float dt=Time.deltaTime,time=Time.time;
            foreach(var unit in Match.Units)
            {
                if(!unitViews.TryGetValue(unit.Id,out var view))unitViews[unit.Id]=view=CreateUnitView(unit);
                // A machine worker gathering back out of a cancelled site shows only as the liquid until it has formed.
                bool seen=Match.SeenBy(PrototypeMatch.Player,unit)&&!Gathering(unit);
                if(view.Root.activeSelf!=seen){view.Root.SetActive(seen);if(view.Shadow!=null)view.Shadow.gameObject.SetActive(seen);}
                var at=Place(unit);
                float ground=(float)Match.HeightAt(at.x,at.y);
                if(unit.Stats.Flying)
                {
                    // Climb early and fast, sink late and slowly: the flight level follows the highest ground around
                    // the unit and ahead along its course, so it clears a cliff before reaching the edge.
                    float level=FlightGround(unit);
                    view.Height=Mathf.Lerp(view.Height,level,1-Mathf.Exp(-dt*(level>view.Height?8:2.2f)));
                }
                // A leaper flies its jump in an arc.
                else view.Height=ground+(unit.Leaping?Mathf.Sin((float)unit.LeapShare*Mathf.PI)*.9f:0);
                var position=new Vector3(at.x,view.Height,at.y);
                var root=view.Root.transform;
                var direction=position-root.position;direction.y=0;
                float step=direction.magnitude;
                var target=unit.HarvestTarget;bool nanite=unit.Kind==UnitKind.Worker&&Match.Roster(unit.Owner).NaniteMining;
                // A hauling worker mines with a beam; a nanite worker wraps its deposit (facing it from the start of the wrap).
                bool mining=target!=null&&target.Remaining>0&&(nanite?unit.Wrap>0:unit.Mining);
                var deposit=mining?new Vector3((float)target.X+.5f,(float)Match.Map.Height(target.X,target.Z),(float)target.Z+.5f):Vector3.zero;
                if(mining){direction=deposit-position;direction.y=0;}
                // A building worker faces its building and welds it with the same beam: into the footprint, at the top of
                // the part that has risen so far, the end wandering along it like a welding torch.
                var site=unit.Constructing;bool welding=!mining&&site!=null&&site.Powered&&PrototypeMatch.Gap(unit,site)<=PrototypeMatch.BuildReach;
                var weld=Vector3.zero;
                if(welding)
                {
                    float top=(float)site.Stats.Height*(float)site.Ready,cx=(float)site.X+site.Width*.5f,cz=(float)site.Z+site.Width*.5f;
                    var edge=new Vector2(Mathf.Clamp((float)unit.X,(float)site.X,(float)site.X+site.Width),Mathf.Clamp((float)unit.Z,(float)site.Z,(float)site.Z+site.Width));
                    var into=Vector2.Lerp(edge,new Vector2(cx,cz),.3f+.12f*Mathf.Sin(time*2.3f+unit.Id));
                    var along=new Vector2(-(cz-edge.y),cx-edge.x).normalized*.35f*Mathf.Sin(time*1.7f+unit.Id*.7f);
                    weld=new Vector3(into.x+along.x,(float)Match.Map.Height(site.X,site.Z)+Mathf.Max(.18f,top*.9f),into.y+along.y);
                    direction=new Vector3(cx-(float)unit.X,0,cz-(float)unit.Z);
                }
                // A repairing worker welds its target the same way: a building along its side, machinery at its middle.
                else if(!mining&&unit.Welding)
                {
                    welding=true;var mend=unit.Repairing;
                    if(mend!=null)
                    {
                        float cx=mend.X+mend.Width*.5f,cz=mend.Z+mend.Width*.5f;
                        var edge=new Vector2(Mathf.Clamp((float)unit.X,(float)mend.X,(float)mend.X+mend.Width),Mathf.Clamp((float)unit.Z,(float)mend.Z,(float)mend.Z+mend.Width));
                        var along=new Vector2(-(cz-edge.y),cx-edge.x).normalized*.4f*Mathf.Sin(time*1.7f+unit.Id*.7f);
                        weld=new Vector3(edge.x+along.x,(float)Match.Map.Height(mend.X,mend.Z)+(float)mend.Stats.Height*(.35f+.2f*Mathf.Sin(time*2.3f+unit.Id)),edge.y+along.y);
                        direction=new Vector3(cx-(float)unit.X,0,cz-(float)unit.Z);
                    }
                    else if(unit.RepairingUnit!=null)
                    {
                        var other=unit.RepairingUnit;
                        weld=unitViews.TryGetValue(other.Id,out var otherView)?Center(other,otherView):new Vector3((float)other.X,(float)Match.HeightAt((float)other.X,(float)other.Z)+.4f,(float)other.Z);
                        direction=new Vector3((float)other.X-(float)unit.X,0,(float)other.Z-(float)unit.Z);
                    }
                }
                // A moving unit faces its next waypoint, so shoves do not twist it; a shoved idle unit keeps its facing.
                if(unit.Leaping){/* In flight a leaper faces the way it flies: this frame's movement. */}
                else if(unit.Moving&&!mining)foreach(var next in unit.Route){direction=new Vector3((float)next.X-(float)unit.X,0,(float)next.Z-(float)unit.Z);break;}
                else if(unit.Target!=null)direction=new Vector3((float)unit.Target.X-(float)unit.X,0,(float)unit.Target.Z-(float)unit.Z);
                else if(unit.TargetBuilding!=null){var b=unit.TargetBuilding;direction=new Vector3(Mathf.Clamp((float)unit.X,(float)b.X,(float)b.X+b.Width)-(float)unit.X,0,Mathf.Clamp((float)unit.Z,(float)b.Z,(float)b.Z+b.Width)-(float)unit.Z);}
                else if(!mining&&!welding)direction=Vector3.zero;
                if(direction.sqrMagnitude>.0001f)root.rotation=Quaternion.Slerp(root.rotation,Quaternion.LookRotation(direction),dt*10);
                root.position=position;
                // A shoved idle unit shuffles its legs too.
                bool moving=unit.Moving||step>dt*.6f;
                if(view.Legs.Length>0)
                {
                    // About one stride per 0.75 units travelled.
                    view.Phase+=step/view.Model.Scale*8.4f;
                    float swing=moving?Mathf.Sin(view.Phase)*28:0;
                    for(int i=0;i<view.Legs.Length;i++)
                        view.Legs[i].localRotation=Quaternion.Slerp(view.Legs[i].localRotation,Quaternion.Euler(i%2==0?swing:-swing,0,0),moving?1:dt*10);
                    view.Body.localPosition=Vector3.up*(moving?Mathf.Abs(Mathf.Cos(view.Phase))*.035f:0);
                    view.Body.localRotation=Quaternion.Slerp(view.Body.localRotation,Quaternion.Euler(moving?5:0,0,0),dt*8);
                }
                else
                {
                    float bob=unit.Stats.Flying?Mathf.Sin(time*1.7f+unit.Id)*.07f:Mathf.Sin(time*3.1f+unit.Id)*.03f;
                    view.Body.localPosition=Vector3.up*(view.Model.Hover+bob);
                    view.Body.localRotation=Quaternion.Slerp(view.Body.localRotation,Quaternion.Euler(moving?(unit.Stats.Flying?9:12):0,0,0),dt*6);
                }
                if(view.Kick>0)
                {
                    // Melee lunges into the blow, ranged weapons recoil.
                    float kick=unit.Stats.AttackRange<1?Mathf.Sin(view.Kick*Mathf.PI)*.24f:-view.Kick*.07f;
                    view.Body.localPosition+=Vector3.forward*kick/view.Model.Scale;
                    view.Kick=Mathf.Max(0,view.Kick-dt*(unit.Stats.AttackRange<1?5:9));
                }
                for(int i=0;i<view.Rotors.Length;i++)view.Rotors[i].localRotation=Quaternion.Euler(0,time*1300*(i%2==0?1:-1)+i*40,0);
                // A howitzer lifts its barrel as it deploys and lowers it to travel.
                if(view.Gun!=null)view.Gun.localRotation=Quaternion.Euler(-view.Model.GunLift*(float)unit.DeployShare,0,0);
                if(view.Shadow!=null)view.Shadow.position=new Vector3(at.x,ground+.06f,at.y);
                bool beam=(mining&&!nanite)||welding;
                if(beam&&view.Beam==null)
                {
                    view.Beam=new GameObject("Mining beam",typeof(LineRenderer)).GetComponent<LineRenderer>();view.Beam.transform.SetParent(view.Root.transform);
                    view.Beam.sharedMaterial=beamMaterial;view.Beam.positionCount=2;view.Beam.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                if(view.Beam!=null)view.Beam.enabled=beam;
                if(beam)
                {
                    var jitter=new Vector3(Mathf.Sin(time*23),Mathf.Sin(time*31),Mathf.Cos(time*19))*(mining?.03f:.09f);
                    view.Beam.SetPosition(0,view.Body.TransformPoint(new Vector3(0,.45f,.33f)));
                    view.Beam.SetPosition(1,mining?deposit+Vector3.up*(target.Plasma?.25f:.55f)+jitter:weld+jitter);
                    view.Beam.widthMultiplier=.05f+.015f*Mathf.Sin(time*40);
                }
                Weave(view,unit,nanite&&mining,deposit,time);
                // Welding throws short sparks where the beam meets the building.
                if(welding&&seen&&(view.Sparks-=dt)<=0){view.Sparks=.11f+.06f*Mathf.PerlinNoise(time*3,unit.Id);combat.Flash(weld,new Color(3,2.3f,1.2f),.2f,.09f,1.6f);}
                bool carrying=unit.Carrying>0;
                if(carrying&&view.Cargo==null)
                {
                    view.Cargo=scenery.Model("Cargo",cargoMesh,oreMaterial,view.Body,false);
                    view.Cargo.transform.localPosition=new Vector3(0,.06f,.3f);view.Cargo.transform.localScale=Vector3.one*.22f;
                }
                if(view.Cargo!=null)
                {
                    if(view.Cargo.activeSelf!=carrying)view.Cargo.SetActive(carrying);
                    if(carrying)view.Cargo.GetComponent<MeshRenderer>().sharedMaterial=unit.CarriesPlasma?plasmaCargoMaterial:oreMaterial;
                }
                if(seen&&(unit.Health<unit.Stats.Health||selection.Contains(unit)||hovered.Contains(unit)))
                    combat.Bar(Top(unit,view)+Vector3.up*.22f,Mathf.Max(.62f,(float)unit.Stats.Radius*2.3f),unit.Health/(float)unit.Stats.Health);
                Machine(unit,view,seen);
            }
            foreach(var (b,view) in buildingViews)
            {
                var stats=b.Stats;
                if(!view.Model.activeSelf||(b.Owner!=PrototypeMatch.Player&&!Match.SeenBy(PrototypeMatch.Player,b)))continue;
                if(b.Health<stats.Health||selectedBuilding==b)
                    combat.Bar(new Vector3((float)b.X+b.Width*.5f,(float)Match.Map.Height(b.X,b.Z)+(float)stats.Height+.45f,(float)b.Z+b.Width*.5f),b.Width*.8f,b.Health/(float)stats.Health);
                // A machine building cut off from the network blinks amber on its grace, then a dark one gives a faint grey pulse.
                if(!b.Linked&&b.Ready>=1&&UnityEngine.Random.value<Time.deltaTime*(b.Powered?2.5f:.7f))
                    combat.Flash(new Vector3((float)b.X+b.Width*.5f,(float)Match.Map.Height(b.X,b.Z)+(float)stats.Height+.15f,(float)b.Z+b.Width*.5f),b.Powered?new Color(1,.7f,.28f)*2:new Color(.5f,.52f,.58f),.22f*b.Width,.35f,1.6f);
                // A machine building mending in the network glints here and there over its body.
                if(b.Mending&&UnityEngine.Random.value<Time.deltaTime*3*b.Width)
                {
                    var r=UnityEngine.Random.insideUnitCircle*b.Width*.45f;
                    combat.Flash(new Vector3((float)b.X+b.Width*.5f+r.x,(float)Match.Map.Height(b.X,b.Z)+(float)stats.Height*UnityEngine.Random.Range(.2f,.95f),(float)b.Z+b.Width*.5f+r.y),NaniteGlow*1.8f,.14f,.5f,2);
                }
            }
            combat.EndBars();
            if(unitViews.Count>Match.Units.Count)
            {
                alive.Clear();foreach(var u in Match.Units)alive.Add(u.Id);
                gone.Clear();foreach(var id in unitViews.Keys)if(!alive.Contains(id))gone.Add(id);
                foreach(var id in gone){ReleasePuppet(unitViews[id]);unitViews.Remove(id);}
                selection.Prune(Match.Units);
            }
        }
        readonly HashSet<int> alive=new HashSet<int>();
        readonly List<int> gone=new List<int>();
        // Back to the pool: what the unit grew while alive (beam, threads, cargo, cocoon) goes, the dissolve cut is lifted.
        void ReleasePuppet(UnitView view)
        {
            if(view.Beam!=null)Destroy(view.Beam.gameObject);
            if(view.Nanites!=null)Destroy(view.Nanites.gameObject);
            if(view.Cargo!=null)Destroy(view.Cargo);
            if(view.Cocoon!=null)Destroy(view.Cocoon);
            if(view.Renderers!=null)foreach(var r in view.Renderers)if(r!=null)r.SetPropertyBlock(null);
            view.Root.SetActive(false);if(view.Shadow!=null)view.Shadow.gameObject.SetActive(false);
            if(!idlePuppets.TryGetValue(view.Look,out var idle))idlePuppets[view.Look]=idle=new Stack<UnitView>();
            idle.Push(view);
        }
        // A machine being taken apart dissolves from the feet up under a cold glowing edge (the same cut as a rising machine
        // building); one mending inside the network glints now and then as its nanites close the damage.
        static readonly Color NaniteGlow=new Color(.62f,.86f,1);
        void Machine(PrototypeUnit unit,UnitView view,bool seen)
        {
            if(unit.Disassembling||view.Dissolving)
            {
                view.Renderers??=view.Root.GetComponentsInChildren<MeshRenderer>(true);
                float bottom=view.Root.transform.position.y-.05f,top=bottom+(view.Model.Height+view.Model.Hover)*view.Model.Scale+.25f;
                if(unit.Disassembling){revealBlock.SetFloat("_RevealBelow",Mathf.Lerp(bottom,top,(float)unit.Disassembly));revealBlock.SetColor("_RevealColor",NaniteGlow*2.2f);}
                else revealBlock.Clear();
                foreach(var r in view.Renderers)if(r!=null)r.SetPropertyBlock(revealBlock);
                revealBlock.Clear();view.Dissolving=unit.Disassembling;
            }
            if(!seen||!unit.Mending||(view.Mend-=Time.deltaTime)>0)return;
            view.Mend=.35f+.3f*Mathf.PerlinNoise(Time.time*2,unit.Id);
            var at=Center(unit,view)+new Vector3(Mathf.Sin(Time.time*7+unit.Id),Mathf.Cos(Time.time*5),Mathf.Cos(Time.time*6+unit.Id))*(float)unit.Stats.Radius*.7f;
            combat.Flash(at,NaniteGlow*1.8f,.1f,.45f,2);
        }
        // A nanite worker wraps its deposit: glowing threads spiral from its arms round the deposit and climb it as the
        // wrap grows, then keep slowly turning. Its cocoon is a faint shell round the body that flares when it takes a
        // blow and thins out as it wears down.
        void Weave(UnitView view,PrototypeUnit unit,bool wrapping,Vector3 deposit,float time)
        {
            if(wrapping&&view.Nanites==null)
            {
                view.Nanites=new GameObject("Nanite threads",typeof(LineRenderer)).GetComponent<LineRenderer>();view.Nanites.transform.SetParent(view.Root.transform);
                view.Nanites.sharedMaterial=naniteMaterial;view.Nanites.positionCount=NaniteThread;view.Nanites.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                view.Nanites.widthMultiplier=.035f;
            }
            if(view.Nanites!=null)view.Nanites.enabled=wrapping;
            if(wrapping)
            {
                var from=view.Body.TransformPoint(new Vector3(0,.3f,.3f));float climb=.08f+.62f*(float)unit.Wrap;
                for(int i=0;i<NaniteThread;i++)
                {
                    float t=i/(NaniteThread-1f),a=t*Mathf.PI*5+time*1.3f+unit.Id,r=.42f-.12f*t;
                    var round=deposit+new Vector3(Mathf.Cos(a)*r,.04f+climb*t,Mathf.Sin(a)*r);
                    // The first stretch leaves the worker's arms and joins the spiral.
                    view.Nanites.SetPosition(i,t<.12f?Vector3.Lerp(from,round,t/.12f):round);
                }
            }
            bool shell=unit.Cocoon>0&&view.Root.activeSelf;
            if(shell&&view.Cocoon==null)view.Cocoon=scenery.Model("Nanite cocoon",cocoonMesh,cocoonMaterial,view.Body,false);
            if(view.Cocoon==null)return;
            if(view.Cocoon.activeSelf!=shell)view.Cocoon.SetActive(shell);
            if(!shell){view.CocoonSeen=0;return;}
            float share=unit.Cocoon/(float)Mathf.Max(1,unit.Stats.Cocoon);
            float flare=unit.Cocoon<view.CocoonSeen?2.5f:1;view.CocoonSeen=unit.Cocoon;
            cocoonColor.SetColor("_Color",new Color(.14f,.27f,.38f,1)*(.35f+.65f*share)*flare*(1+.12f*Mathf.Sin(time*3+unit.Id)));
            view.Cocoon.GetComponent<MeshRenderer>().SetPropertyBlock(cocoonColor);
            view.Cocoon.transform.localScale=Vector3.one*(.92f+.08f*share);
        }
        // Highest terrain within a body's width of the unit and up to two and a half cells ahead on its route.
        float FlightGround(PrototypeUnit unit)
        {
            float H(float x,float z)=>(float)Match.HeightAt(Mathf.Clamp(x,0,MapSize-.01f),Mathf.Clamp(z,0,MapSize-.01f));
            float r=(float)unit.Stats.Radius+.3f,level=H((float)unit.X,(float)unit.Z);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;level=Mathf.Max(level,H((float)unit.X+Mathf.Cos(a)*r,(float)unit.Z+Mathf.Sin(a)*r));}
            foreach(var next in unit.Route)
            {
                var ahead=new Vector2((float)next.X-(float)unit.X,(float)next.Z-(float)unit.Z);float distance=Mathf.Min(2.5f,ahead.magnitude);
                if(distance<.01f)break;ahead/=ahead.magnitude;
                for(float d=.5f;d<=distance;d+=.5f)level=Mathf.Max(level,H((float)unit.X+ahead.x*d,(float)unit.Z+ahead.y*d));
                break;
            }
            return level;
        }
    }
}
