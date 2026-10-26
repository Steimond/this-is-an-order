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
        // Enemy buildings stay on the map once explored, as the last thing seen of them (StarCraft's memory of structures).
        bool BuildingShown(PrototypeBuilding b)
        {
            if(b.Owner==PrototypeMatch.Player)return true;
            for(int z=b.Z;z<b.Z+b.Width;z++)for(int x=b.X;x<b.X+b.Width;x++)if(Match.Explored[x,z])return true;
            return false;
        }
        void AnimateBuildings()
        {
            foreach(var (b,view) in buildingViews)
            {
                float h=(float)b.Stats.Height,ground=(float)Match.Map.Height(b.X,b.Z);
                bool shown=BuildingShown(b);
                if(view.Model.activeSelf!=shown)view.Model.SetActive(shown);
                if(b.SelfBuilt&&(b.Ready<1||view.Revealing)){Liquid(b,view,shown,h,ground);continue;}
                // The full-sized model rises through the opaque terrain; it never scales up.
                view.Model.transform.position=new Vector3((float)b.X+b.Width*.5f,ground-h*(1-(float)b.Ready),(float)b.Z+b.Width*.5f);
                view.Ring.SetActive(b.Ready<1&&shown);
                // The ring pulses while a worker builds; a construction that waits for its worker keeps it still.
                bool rising=b.Builder!=null&&PrototypeMatch.Gap(b.Builder,b)<=PrototypeMatch.BuildReach;
                if(b.Ready<1){view.Ring.transform.position=new Vector3(b.X+b.Width*.5f,ground,b.Z+b.Width*.5f);view.Ring.transform.localScale=Vector3.one*b.Width*(rising?.74f+.04f*Mathf.Sin(Time.time*6):.7f);}
            }
        }
        readonly Dictionary<Mesh,Mesh> liquidMeshes=new Dictionary<Mesh,Mesh>();
        // Buildings split to 0.3, the small bodies of units finer, so both bend smoothly.
        Mesh LiquidMesh(Mesh mesh,float maxEdge=.3f){if(!liquidMeshes.TryGetValue(mesh,out var liquid))liquidMeshes[mesh]=liquid=scenery.Own(NaniteMeshes.Subdivided(mesh,maxEdge));return liquid;}
        Mesh PuddleMesh()=>puddleMesh??=scenery.Own(NaniteMeshes.Puddle());
        // Magnetic spikes over a model, cones about 0.085 apart, 0.075 in foot radius, up to 0.18 tall (NaniteMeshes.Spikes), and over the puddle.
        readonly Dictionary<Mesh,Mesh> spikeMeshes=new Dictionary<Mesh,Mesh>();
        Mesh SpikeMesh(Mesh mesh){if(!spikeMeshes.TryGetValue(mesh,out var spikes))spikeMeshes[mesh]=spikes=scenery.Own(NaniteMeshes.Spikes(mesh,.085f,.075f,.18f));return spikes;}
        Mesh PuddleSpikeMesh()=>puddleSpikes??=scenery.Own(NaniteMeshes.PuddleSpikes());
        // The liquid with its spikes, a child drawn with the same settings (LiquidBlock).
        GameObject WithSpikes(GameObject liquid,Mesh spikes){scenery.Model("Nanite spikes",spikes,spikeMaterial,liquid.transform);return liquid;}
        // The spikes draw with the liquid's own settings: in the editor they follow its material as it is tuned.
        void SyncSpikes()
        {
#if UNITY_EDITOR
            spikeMaterial.CopyPropertiesFromMaterial(liquidMaterial);spikeMaterial.EnableKeyword("NANITE_SPIKES");
#endif
        }
        // What a liquid object takes from its view, beside the tunable look in the material: shift is how far the liquid's
        // flow has gone (+ up, - down), integrated by the caller, so the pattern and the shape slow down and turn instead
        // of jumping; spikes and wobble scale the material's spikes and wobble for this object.
        void LiquidBlock(Renderer renderer,float front,float shift,float ground,float spikes=1,float wobble=1,Vector4 pool=default,float rough=LiquidRough)
        {
            revealBlock.SetFloat("_Front",front);revealBlock.SetFloat("_FrontNoise",rough);revealBlock.SetFloat("_Shift",shift);
            revealBlock.SetFloat("_Base",ground);revealBlock.SetFloat("_WobbleFade",wobble);revealBlock.SetFloat("_Spikes",spikes);
            if(pool.z>0)revealBlock.SetVector("_Pool",pool);
            renderer.SetPropertyBlock(revealBlock);
            // Its spikes, if it has them, stand on the same liquid.
            if(renderer.transform.childCount>0)renderer.transform.GetChild(0).GetComponent<MeshRenderer>().SetPropertyBlock(revealBlock);
            revealBlock.Clear();
        }
        // How far the building's shape reaches for the liquid above the ground, with the pooling on flat tops
        // (NaniteLevel): the lowest level the drain has to uncover (x), the highest it starts from (y), and the highest
        // the fill has to cover (z), where the pooling raises flat tops instead. Flat tops are sampled over their area,
        // leaving out what the shape itself covers from above (the floor under a tower) and what is below the ground.
        // Once per model: every building of a kind shares it.
        readonly Dictionary<Mesh,Vector3> liquidSpans=new Dictionary<Mesh,Vector3>();
        Vector3 LiquidSpan(Transform model,Mesh mesh,float radius,float pool)
        {
            if(liquidSpans.TryGetValue(mesh,out var known))return known;
            var vertices=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;var origin=model.position;
            int count=triangles.Length/3;var corners=new Vector3[count*3];var up=new float[count];var boxes=new Vector4[count];
            for(int i=0;i<count;i++)
            {
                for(int k=0;k<3;k++)
                {
                    int j=triangles[i*3+k];corners[i*3+k]=model.TransformPoint(vertices[j])-origin;
                    float s=Mathf.Clamp01((model.TransformDirection(normals[j]).y-.6f)/.35f);up[i]=Mathf.Max(up[i],s*s*(3-2*s));
                }
                Vector3 a=corners[i*3],b=corners[i*3+1],c=corners[i*3+2];
                boxes[i]=new Vector4(Mathf.Min(a.x,Mathf.Min(b.x,c.x)),Mathf.Max(a.x,Mathf.Max(b.x,c.x)),Mathf.Min(a.z,Mathf.Min(b.z,c.z)),Mathf.Max(a.z,Mathf.Max(b.z,c.z)));
            }
            // Whether the shape has a face right above this point.
            bool Covered(Vector3 p,int self)
            {
                for(int i=0;i<count;i++)
                {
                    if(i==self||p.x<boxes[i].x||p.x>boxes[i].y||p.z<boxes[i].z||p.z>boxes[i].w)continue;
                    Vector3 a=corners[i*3],b=corners[i*3+1],c=corners[i*3+2];
                    float d=(b.x-a.x)*(c.z-a.z)-(c.x-a.x)*(b.z-a.z);if(Mathf.Abs(d)<1e-6f)continue;
                    float u=((p.x-a.x)*(c.z-a.z)-(c.x-a.x)*(p.z-a.z))/d,v=((b.x-a.x)*(p.z-a.z)-(p.x-a.x)*(b.z-a.z))/d;
                    if(u<0||v<0||u+v>1)continue;
                    if(a.y+(b.y-a.y)*u+(c.y-a.y)*v>p.y+.02f)return true;
                }
                return false;
            }
            float low=float.MaxValue,high=float.MinValue,flooded=float.MinValue;const int Grid=24;
            for(int i=0;i<count;i++)
            {
                Vector3 a=corners[i*3],b=corners[i*3+1],c=corners[i*3+2];
                float lo=Mathf.Min(a.y,Mathf.Min(b.y,c.y)),hi=Mathf.Max(a.y,Mathf.Max(b.y,c.y));
                if(hi<0)continue;
                low=Mathf.Min(low,Mathf.Max(0,lo));high=Mathf.Max(high,hi);flooded=Mathf.Max(flooded,hi);
                if(up[i]<=0)continue;
                for(int p=0;p<=Grid;p++)for(int q=0;q<=Grid-p;q++)
                {
                    var s=a+(b-a)*(p/(float)Grid)+(c-a)*(q/(float)Grid);
                    if(s.y<0||Covered(s,i))continue;
                    float near=pool*up[i]*(1-Mathf.Clamp01(new Vector2(s.x,s.z).magnitude/radius));
                    low=Mathf.Min(low,s.y-near);flooded=Mathf.Max(flooded,s.y+near);
                }
            }
            return liquidSpans[mesh]=new Vector3(low,high,flooded);
        }
        void Liquid(PrototypeBuilding b,BuildingView view,bool shown,float h,float ground)
        {
            float w=b.Width,cx=b.X+w*.5f,cz=b.Z+w*.5f,pool=w*PoolDepth,time=Time.time;
            var drainPool=new Vector4(cx,cz,w*.5f,pool);
            view.Model.transform.position=new Vector3(cx,ground,cz);
            if(!view.Spanned){view.Span=LiquidSpan(view.Model.transform,view.Model.GetComponent<MeshFilter>().sharedMesh,w*.5f,pool);view.Spanned=true;}
            // The fill rises to just over the highest level it has to cover (full); the drain sets off from just over the
            // highest point it can uncover (top) and ends just under the lowest (bottom). The tongues smooth out as the
            // filling ends and as the draining ends, and grow back as the draining sets off, so the shape is whole just as
            // the filling ends, starts to show at once and is bare just as the building is ready.
            float top=ground+view.Span.y+.03f,bottom=ground+view.Span.x-.03f,full=ground+view.Span.z+.03f;
            view.Ring.SetActive(false);
            var renderer=view.Model.GetComponent<MeshRenderer>();
            float build=(float)b.Stats.BuildSeconds,fill=Mathf.Clamp(3/build,.2f,.45f),drain=(1-fill)*build;
            // The worker's melt comes first: while it lasts only the puddle grows under it.
            float melt=Mathf.Min(MeltSeconds/build,fill*.6f);
            // The film is as tall as the edge travels while it dries, so it reads the same on a quick node and a slow core.
            float film=Mathf.Clamp((top-bottom)/Mathf.Max(.1f,drain)*DryingSeconds,.25f,1.2f);
            if(b.Ready>=1)
            {
                if(view.Liquid!=null){Destroy(view.Liquid);view.Liquid=null;view.DriedAt=time+DryingSeconds;}
                if(view.Puddle!=null){Destroy(view.Puddle);view.Puddle=null;}
                float left=view.DriedAt-time;
                if(left<=0){revealBlock.Clear();renderer.SetPropertyBlock(revealBlock);view.Revealing=false;return;}
                revealBlock.SetFloat("_RevealBelow",bottom);revealBlock.SetFloat("_RevealNoise",0);revealBlock.SetVector("_Pool",drainPool);revealBlock.SetFloat("_Wet",film*left/DryingSeconds);
                renderer.SetPropertyBlock(revealBlock);revealBlock.Clear();return;
            }
            view.Revealing=true;
            if(view.Liquid==null){var shape=view.Model.GetComponent<MeshFilter>().sharedMesh;view.Liquid=WithSpikes(scenery.Model("Nanite form",LiquidMesh(shape),liquidMaterial,worldRoot.transform),SpikeMesh(shape));}
            if(view.Liquid.activeSelf!=shown)view.Liquid.SetActive(shown);
            view.Liquid.transform.SetPositionAndRotation(view.Model.transform.position,view.Model.transform.rotation);
            view.Liquid.transform.localScale=view.Model.transform.localScale;
            // The puddle at the middle of the site: it spreads while the worker melts, then sinks into the rising liquid.
            float sinking=Mathf.InverseLerp(melt,melt+(fill-melt)*.4f,(float)b.Ready),radius=w*.42f*Mathf.SmoothStep(0,1,(float)b.Ready/Mathf.Max(1e-4f,melt*.8f))*(1-Mathf.SmoothStep(0,1,sinking));
            if(radius>.02f)
            {
                if(view.Puddle==null)view.Puddle=WithSpikes(scenery.Model("Nanite puddle",PuddleMesh(),liquidMaterial,worldRoot.transform),PuddleSpikeMesh());
                if(view.Puddle.activeSelf!=shown)view.Puddle.SetActive(shown);
                view.Puddle.transform.SetPositionAndRotation(new Vector3(cx,ground-.02f-.08f*sinking,cz),Quaternion.Euler(0,b.X*37,0));
                view.Puddle.transform.localScale=new Vector3(radius,1,radius);
                // Its spikes grow as it spreads.
                LiquidBlock(view.Puddle.GetComponent<MeshRenderer>(),1000,0,ground-1,Mathf.Clamp01(radius));
            }
            else if(view.Puddle!=null){Destroy(view.Puddle);view.Puddle=null;}
            view.PuddleRadius=view.Puddle!=null?radius:0;
            float front,flow,rough;
            static float Ease(float from,float to,float x){float t=Mathf.Clamp01((x-from)/(to-from));return t*t*(3-2*t);}
            if(b.Ready<fill)
            {
                // Filling: the front sets off gently, rises fast and settles at the top; the building itself is not there yet.
                float progress=Mathf.InverseLerp(melt,fill,(float)b.Ready),s=Mathf.SmoothStep(0,1,progress);
                front=view.Front=Mathf.Lerp(ground-LiquidRough*.35f-.02f,full,1-(1-s)*(1-s));flow=1.1f;view.Pool=new Vector4(cx,cz,w*.5f,-pool);
                rough=LiquidRough*(1-Ease(.8f,1,progress));
                revealBlock.SetFloat("_RevealBelow",1000);
            }
            else
            {
                // Draining: one edge for both, so the building shows exactly where the liquid has left. It picks up speed
                // over the first tenth or so and keeps its pace to the end, so the last of the liquid leaves just as the
                // building is ready.
                float s=((float)b.Ready-fill)/(1-fill);const float Start=.06f;
                float travelled=(s-Start*(1-Mathf.Exp(-s/Start)))/(1-Start*(1-Mathf.Exp(-1/Start)));
                front=Mathf.Lerp(top,bottom,travelled);flow=-1.2f;view.Front=top;view.Pool=drainPool;
                rough=LiquidRough*Ease(0,.15f,s)*(1-Ease(.85f,1,s));
                revealBlock.SetFloat("_RevealBelow",front);revealBlock.SetFloat("_RevealNoise",rough);revealBlock.SetVector("_Pool",drainPool);
                revealBlock.SetColor("_RevealColor",NaniteGlow*1.6f);revealBlock.SetFloat("_Wet",film);
            }
            renderer.SetPropertyBlock(revealBlock);revealBlock.Clear();
            // When the liquid starts to drain its flow slows, stops and runs down, over about a second.
            view.Flow=Mathf.MoveTowards(view.Flow,flow,Time.deltaTime*2.5f);view.Shift+=view.Flow*Time.deltaTime;
            LiquidBlock(view.Liquid.GetComponent<MeshRenderer>(),front,view.Shift,ground,pool:view.Pool,rough:rough);
        }
        // A worker spent on a building loses its form where it stands, in the middle of the site: its body turns to the
        // same liquid, sags and spreads, and sinks into the puddle growing under it.
        sealed class Melt{public GameObject View;public Vector3 At,Scale;public Quaternion Turn;public float Start,Ground;}
        readonly List<Melt> melts=new List<Melt>();
        void StartMelt(PrototypeUnit unit,UnitView view)
        {
            var body=view.Body;
            var melt=scenery.Model("Melting "+unit.Stats.Name,LiquidMesh(view.Model.Body,.08f),liquidMaterial,worldRoot.transform);
            melts.Add(new Melt{View=melt,At=body.position,Scale=body.lossyScale,Turn=body.rotation,Start=Time.time,Ground=(float)Match.HeightAt((float)unit.X,(float)unit.Z)});
            AnimateMelts();
        }
        void AnimateMelts()
        {
            for(int i=melts.Count-1;i>=0;i--)
            {
                var m=melts[i];float s=(Time.time-m.Start)/MeltSeconds;
                if(s>=1){Destroy(m.View);melts.RemoveAt(i);continue;}
                float sag=Mathf.SmoothStep(0,1,Mathf.Min(1,s*1.6f)),sink=Mathf.SmoothStep(0,1,Mathf.Max(0,s*2.2f-1.2f));
                m.View.transform.SetPositionAndRotation(m.At+Vector3.down*sink*.25f,m.Turn);
                m.View.transform.localScale=Vector3.Scale(m.Scale,new Vector3(1+.55f*sag-.4f*sink,Mathf.Lerp(1,.12f,sag),1+.55f*sag-.4f*sink));
                // The shiver and the spikes come in as the body loses its form, so it does not twitch the moment it melts.
                LiquidBlock(m.View.GetComponent<MeshRenderer>(),1000,-1.5f*(Time.time-m.Start),m.Ground,3*sag,Mathf.SmoothStep(0,1,Mathf.Min(1,s*4)));
            }
        }
        // Cancelling a machine site plays it all back, quickly: the liquid shape slumps down into a puddle and the puddle
        // gathers up into the worker, which can be ordered from the start; its model stays hidden until it has formed.
        sealed class Gather{public GameObject Form,Puddle,Body;public PrototypeUnit Worker;public float Start,Top,Ground,Radius,Scale,Flow,Shift,PuddleFrom;public Vector3 Center;public Vector4 Pool;}
        readonly List<Gather> gathers=new List<Gather>();
        void OnCancelled(PrototypeBuilding b,PrototypeUnit worker)
        {
            // The site's own view goes at once, not with the next refresh: left standing, it would hide the start of the gathering.
            if(!b.SelfBuilt||!buildingViews.TryGetValue(b,out var view)||!view.Model.activeSelf){SyncBuildings();return;}
            float ground=(float)Match.Map.Height(b.X,b.Z);
            // It starts from where the liquid is: as high as it has filled, flowing as it flowed, the puddle as wide as it was.
            var g=new Gather{Worker=worker,Start=Time.time,Ground=ground,Top=view.Liquid!=null?view.Front:ground+(float)b.Stats.Height*1.08f+.15f,Radius=b.Width*.42f,
                Flow=view.Flow,Shift=view.Shift,PuddleFrom=view.PuddleRadius,Pool=view.Pool,Center=new Vector3(b.X+b.Width*.5f,ground,b.Z+b.Width*.5f)};
            var shape=view.Model.GetComponent<MeshFilter>().sharedMesh;
            g.Form=WithSpikes(scenery.Model("Nanite form",LiquidMesh(shape),liquidMaterial,worldRoot.transform),SpikeMesh(shape));
            g.Form.transform.SetPositionAndRotation(view.Model.transform.position,view.Model.transform.rotation);
            g.Puddle=WithSpikes(scenery.Model("Nanite puddle",PuddleMesh(),liquidMaterial,worldRoot.transform),PuddleSpikeMesh());
            if(worker!=null)
            {
                var key=(Match.FactionOf(worker.Owner),worker.Kind);
                if(!unitModels.TryGetValue(key,out var model))
                {
                    model=key.Item1==FactionId.Resonance?MachineModels.Unit(worker.Kind):MatchModels.Unit(worker.Kind);unitModels[key]=model;
                    scenery.Own(model.Body);if(model.Leg!=null)scenery.Own(model.Leg);if(model.Rotor!=null)scenery.Own(model.Rotor);if(model.Gun!=null)scenery.Own(model.Gun);
                }
                g.Body=scenery.Model("Gathering "+worker.Stats.Name,LiquidMesh(model.Body,.08f),liquidMaterial,worldRoot.transform);
                g.Scale=model.Scale;
            }
            gathers.Add(g);SyncBuildings();AnimateGathers();
        }
        bool Gathering(PrototypeUnit unit){foreach(var g in gathers)if(g.Worker==unit)return true;return false;}
        void AnimateGathers()
        {
            for(int i=gathers.Count-1;i>=0;i--)
            {
                var g=gathers[i];float s=(Time.time-g.Start)/GatherSeconds;
                if(s>=1){Destroy(g.Form);Destroy(g.Puddle);if(g.Body!=null)Destroy(g.Body);gathers.RemoveAt(i);continue;}
                // The shape slumps down to the ground in the first half.
                float slump=Mathf.SmoothStep(0,1,Mathf.Min(1,s*2));
                g.Form.SetActive(slump<1);
                g.Flow=Mathf.MoveTowards(g.Flow,-2.5f,Time.deltaTime*12);g.Shift+=g.Flow*Time.deltaTime;
                LiquidBlock(g.Form.GetComponent<MeshRenderer>(),Mathf.Lerp(g.Top,g.Ground-.4f,slump),g.Shift,g.Ground,1-slump,pool:g.Pool);
                // The puddle spreads as the shape slumps, then draws in under the worker.
                float spread=Mathf.SmoothStep(0,1,Mathf.Clamp01(s*2.5f-.3f)),draw=Mathf.SmoothStep(0,1,Mathf.Clamp01(s*2-1));
                var at=g.Worker!=null&&g.Worker.Alive?new Vector3((float)g.Worker.X,(float)Match.HeightAt((float)g.Worker.X,(float)g.Worker.Z),(float)g.Worker.Z):g.Center;
                g.Puddle.transform.position=Vector3.Lerp(g.Center,at,draw)+Vector3.down*.02f;
                float radius=Mathf.Lerp(Mathf.Max(g.PuddleFrom,g.Radius*spread),.12f,draw);g.Puddle.transform.localScale=new Vector3(radius,1,radius);
                LiquidBlock(g.Puddle.GetComponent<MeshRenderer>(),1000,0,g.Ground-1,Mathf.Clamp01(radius));
                // The worker rises out of it in the second half.
                if(g.Body==null)continue;
                float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(s*2-1));
                g.Body.SetActive(rise>0);
                g.Body.transform.position=at;
                g.Body.transform.localScale=new Vector3(g.Scale*(1.5f-.5f*rise),g.Scale*Mathf.Lerp(.1f,1,rise),g.Scale*(1.5f-.5f*rise));
                LiquidBlock(g.Body.GetComponent<MeshRenderer>(),1000,1.5f*(Time.time-g.Start),at.y,1+2*(1-rise));
            }
        }
        // The machines' network, as pylon fields in StarCraft: while the player places a building, has a worker selected or
        // looks at a core or relay, each of its nodes draws its coverage on the ground; a relay that is cut off is grey.
        void ShowCoverage()
        {
            int shown=0;
            bool worker=false;foreach(var u in selection.Orderable)if(u.Kind==UnitKind.Worker)worker=true;
            bool show=CoverageShown=Match.Roster(PrototypeMatch.Player).Network&&(Mode==MatchMode.Build||worker||(selectedBuilding!=null&&selectedBuilding.Owner==PrototypeMatch.Player&&selectedBuilding.Stats.Coverage>0));
            if(show)foreach(var b in Match.Buildings)
            {
                if(b.Owner!=PrototypeMatch.Player||b.Stats.Coverage<=0||b.Ready<1)continue;
                if(coverageRings.Count<=shown)
                {
                    var ring=new GameObject("Network coverage",typeof(LineRenderer)).GetComponent<LineRenderer>();ring.transform.SetParent(worldRoot.transform,false);
                    ring.positionCount=CoverageSegments;ring.loop=true;ring.widthMultiplier=.07f;ring.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    coverageRings.Add(ring);
                }
                var line=coverageRings[shown++];line.enabled=true;line.sharedMaterial=b.Linked?coverageMaterial:darkCoverageMaterial;
                float cx=(float)b.X+b.Width*.5f,cz=(float)b.Z+b.Width*.5f,r=(float)b.Stats.Coverage;
                for(int i=0;i<CoverageSegments;i++)
                {
                    float a=i*Mathf.PI*2/CoverageSegments,x=cx+Mathf.Cos(a)*r,z=cz+Mathf.Sin(a)*r;
                    line.SetPosition(i,new Vector3(x,(float)Match.HeightAt(Mathf.Clamp(x,0,MapSize-.01f),Mathf.Clamp(z,0,MapSize-.01f))+.07f,z));
                }
            }
            for(int i=shown;i<coverageRings.Count;i++)coverageRings[i].enabled=false;
        }
        // Sites the player's workers are walking to, and the ones queued with Shift, show as ghosts of their buildings until
        // the building is placed.
        void ShowPlannedSites()
        {
            int shown=0;
            void Ghost(FactionId faction,int kind,int x,int z)
            {
                if(siteGhosts.Count<=shown)siteGhosts.Add(scenery.Model("Planned site",BuildingMesh(faction,kind),ghostMaterial,worldRoot.transform,false));
                var ghost=siteGhosts[shown++];ghost.SetActive(true);ghost.GetComponent<MeshFilter>().sharedMesh=BuildingMesh(faction,kind);
                int w=Match.Balance.Of(faction).Buildings[kind].Width;
                ghost.transform.position=new Vector3(x+w*.5f,(float)Match.Map.Height(x,z)+.03f,z+w*.5f);
            }
            var own=Match.FactionOf(PrototypeMatch.Player);
            foreach(var u in Match.Units)
            {
                if(u.Owner!=PrototypeMatch.Player)continue;
                if(u.SiteKind>=0)Ghost(own,u.SiteKind,u.SiteX,u.SiteZ);
                foreach(var order in u.Queued)if(order.BuildKind>=0)Ghost(own,order.BuildKind,(int)order.X,(int)order.Z);
            }
            for(int i=shown;i<siteGhosts.Count;i++)siteGhosts[i].SetActive(false);
        }
        // Views appear for new buildings and disappear with destroyed ones.
        void SyncBuildings()
        {
            foreach(var b in Match.Buildings)
            {
                if(buildingViews.ContainsKey(b))continue;
                var material=b.Owner==PrototypeMatch.Player?buildingMaterial:enemyBuildingMaterial;
                buildingViews[b]=new BuildingView{Model=scenery.Model(b.Stats.Name,BuildingMesh(Match.FactionOf(b.Owner),b.Kind),material,worldRoot.transform),
                    Ring=scenery.Model("Construction",ringMesh,constructionMaterial,worldRoot.transform,false)};
            }
            if(buildingViews.Count==Match.Buildings.Count)return;
            foreach(var b in new List<PrototypeBuilding>(buildingViews.Keys))
                if(!Match.Buildings.Contains(b)){var view=buildingViews[b];Destroy(view.Model);Destroy(view.Ring);if(view.Liquid!=null)Destroy(view.Liquid);if(view.Puddle!=null)Destroy(view.Puddle);buildingViews.Remove(b);if(selectedBuilding==b)selectedBuilding=null;}
        }
        // The building whose model is under the cursor, nearest to the camera.
        PrototypeBuilding BuildingAt(Vector2 screen)
        {
            var ray=MapCamera.ScreenPointToRay(screen);PrototypeBuilding best=null;float bestDistance=float.MaxValue;
            foreach(var (b,view) in buildingViews)
            {
                if(!view.Model.activeSelf)continue;
                if(view.Model.GetComponent<MeshRenderer>().bounds.IntersectRay(ray,out float distance)&&distance<bestDistance){best=b;bestDistance=distance;}
            }
            return best;
        }
    }
}
