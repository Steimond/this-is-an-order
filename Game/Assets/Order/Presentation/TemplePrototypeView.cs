using System;
using System.Collections.Generic;
using Order.Content;
using Order.Balance;
using Order.Simulation;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    // Disposable view of a small ECS-driven test match. No scene-authored gameplay state.
    [DisallowMultipleComponent]
    public sealed class TemplePrototypeView : MonoBehaviour, IMatchHost
    {
        const float MinZoom = 6, MaxZoom = 22, StartZoom = 11;
        public PrototypeMatch Match { get; private set; }
        public Camera MapCamera { get; private set; }
        public MatchMode Mode { get; private set; }
        public int BuildKind => buildKind;
        public int SelectedBuilding => selectedBuilding;
        public ResourceDeposit SelectedDeposit => selectedDeposit;
        public IReadOnlyList<Vector2> Pins => pins;
        public Texture MinimapTexture => miniTexture;
        public string ModeHint { get; private set; } = "";
        public IReadOnlyList<PrototypeUnit> Selection => selection;
        VisualElement ui;
        MatchHud hud;
        MatchServices services;
        TempleScenery scenery;
        readonly List<Vector2> pins = new List<Vector2>();
        readonly List<GameObject> pinViews = new List<GameObject>();
        Texture2D miniTexture;
        Color[] miniBase;
        GameObject worldRoot, footprint, focusRing, ghostModel;
        int ghostKind=-1;
        readonly List<GameObject> buildingViews = new List<GameObject>(), constructionRings = new List<GameObject>(), selectionRings = new List<GameObject>();
        readonly List<GameObject> depositViews = new List<GameObject>();
        readonly List<PrototypeUnit> selection = new List<PrototypeUnit>();
        readonly Dictionary<int, UnitView> unitViews = new Dictionary<int, UnitView>();
        readonly Dictionary<UnitKind, UnitModel> unitModels = new Dictionary<UnitKind, UnitModel>();
        readonly Mesh[] buildingMeshes = new Mesh[3];
        Mesh ringMesh, beaconMesh, shadowMesh;
        Material buildingMaterial, ghostMaterial, footprintMaterial, selectedMaterial, neutralMaterial, constructionMaterial, beaconMaterial, unitMaterial, beamMaterial, shadowMaterial;
        Entity matchEntity;
        World ecsWorld;
        Vector3 focus = new Vector3(13,0,13);
        float zoom=StartZoom, targetZoom=StartZoom, focusHeight, refresh;
        int buildKind=-1, hoverX, hoverZ;
        int selectedBuilding=-1;
        ResourceDeposit selectedDeposit;
        bool overUI, hasHover;
        Action onExit;

        void Start()
        {
            if(Match==null)Initialize(GetComponent<UIDocument>().rootVisualElement,()=>UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"));
        }

        public void Initialize(VisualElement root, Action exit, MatchServices services=null)
        {
            if(Match!=null)return;
            this.services=services;
            enabled=true;Mode=MatchMode.None;buildKind=-1;selectedBuilding=-1;selectedDeposit=null;focus=new Vector3(13,0,13);zoom=targetZoom=StartZoom;refresh=0;
            var catalog=Resources.Load<BalanceCatalog>("PrototypeBalanceCatalog");
            if(catalog==null)throw new InvalidOperationException("Create balance assets using Order/Create Missing Balance Assets.");
            Match=new PrototypeMatch(catalog.Snapshot());ui=root;onExit=exit;
            ecsWorld=World.DefaultGameObjectInjectionWorld;
            if(ecsWorld==null)throw new InvalidOperationException("Default ECS world is unavailable.");
            matchEntity=ecsWorld.EntityManager.CreateEntity();
            ecsWorld.EntityManager.AddComponentObject(matchEntity,new PrototypeMatchState{Match=Match});
            worldRoot=new GameObject("Temple Basin • generated world");
            var cameraObject=new GameObject("RTS Camera",typeof(Camera),typeof(AudioListener));
            cameraObject.transform.SetParent(worldRoot.transform);
            MapCamera=cameraObject.GetComponent<Camera>();MapCamera.orthographic=false;MapCamera.fieldOfView=42;
            MapCamera.nearClipPlane=.3f;MapCamera.farClipPlane=180;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            scenery=new TempleScenery(Match.Map,worldRoot.transform,MapCamera);
            focusHeight=Match.HeightAt(focus.x,focus.z);
            // The HUD comes first: models take the team colour of its faction theme.
            BuildUI();Shader.SetGlobalColor("_OrderTeamColor",hud.OwnColor);
            buildingMaterial=scenery.Prop("Buildings",m=>{m.SetFloat("_Panels",1);m.SetFloat("_Gloss",.35f);});
            ghostMaterial=scenery.Prop("Placement ghost",m=>{m.SetFloat("_Opacity",.38f);m.SetFloat("_ZWrite",0);m.SetFloat("_Fog",0);m.renderQueue=3000;});
            unitMaterial=scenery.Prop("Units",m=>{m.SetFloat("_Fog",0);m.SetFloat("_Gloss",.45f);});
            var crystalMaterial=scenery.Prop("Crystals",m=>{m.SetFloat("_Rim",1.1f);m.SetFloat("_Gloss",.9f);m.SetFloat("_Emission",2.2f);});
            var ventMaterial=scenery.Prop("Gas vents");
            footprintMaterial=scenery.Glow("Placement footprint",Color.green,false,m=>m.SetFloat("_Grid",1));
            selectedMaterial=scenery.Glow("Selection",new Color(.3f,1,.42f)*1.5f);
            neutralMaterial=scenery.Glow("Selection neutral",new Color(1,.92f,.6f)*1.2f);
            constructionMaterial=scenery.Glow("Construction",new Color(1,.62f,.22f)*1.4f);
            beaconMaterial=scenery.Glow("Beacon",new Color(1,.42f,.24f)*2,true,m=>m.SetFloat("_FadeHeight",7));
            ringMesh=scenery.Own(MatchModels.Ring(.84f));beaconMesh=scenery.Own(MatchModels.Beacon());
            var minerals=new Mesh[4];for(int i=0;i<minerals.Length;i++)minerals[i]=scenery.Own(MatchModels.Minerals(17+i*31));
            var vent=scenery.Own(MatchModels.Vent(5));
            for(int i=0;i<Match.Map.Resources.Count;i++)
            {
                var r=Match.Map.Resources[i];
                var go=scenery.Model(r.Gas?"Gas vent":"Mineral crystals",r.Gas?vent:minerals[i%minerals.Length],r.Gas?ventMaterial:crystalMaterial,worldRoot.transform);
                go.transform.position=new Vector3(r.X+.5f,Match.Map.Height(r.X,r.Z),r.Z+.5f);go.transform.rotation=Quaternion.Euler(0,i*67%360,0);
                if(r.Gas)scenery.Steam(go.transform,new Vector3(0,.2f,0));
                depositViews.Add(go);
            }
            beamMaterial=scenery.Glow("Mining beam",new Color(.45f,.9f,1)*3);
            shadowMaterial=scenery.Glow("Air unit shadow",new Color(0,0,0,.6f),false,m=>m.SetFloat("_Soft",1));
            shadowMesh=scenery.Own(SoftQuad());
            focusRing=scenery.Model("Selection",ringMesh,selectedMaterial,worldRoot.transform,false);
            Match.AddTestSquad();SelectWorker();
            var square=new MeshBuilder();square.Quad(new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f));
            footprint=scenery.Model("Placement footprint",scenery.Own(square.ToMesh("Footprint")),footprintMaterial,worldRoot.transform,false);footprint.SetActive(false);
            UpdateCamera();RefreshWorld();
        }
        Mesh BuildingMesh(int kind)
        {
            if(buildingMeshes[kind]==null){var stats=Match.Balance.Buildings[kind];buildingMeshes[kind]=scenery.Own(MatchModels.Building(kind,stats.Width,stats.Height));}
            return buildingMeshes[kind];
        }
        void Update()
        {
            if(Match==null)return;
            var mouse=Mouse.current;var keys=Keyboard.current;
            hud?.HandleKeys(keys);
            if(keys!=null&&(hud==null||!hud.KeyboardCaptured))
            {
                // Arrows only: letter keys belong to the command card.
                float x=(keys.rightArrowKey.isPressed?1:0)-(keys.leftArrowKey.isPressed?1:0);
                float z=(keys.upArrowKey.isPressed?1:0)-(keys.downArrowKey.isPressed?1:0);
                focus+=new Vector3(x,0,z)*Time.deltaTime*zoom*CameraSpeed();
            }
            if(mouse!=null&&UnityEngine.Application.isFocused&&Option("controls.edgeScroll",1)>=.5f)
            {
                // Edge scrolling: only the outermost pixels of the window, while the pointer is inside it.
                Vector2 p=mouse.position.ReadValue();float edge=Mathf.Max(2,Screen.height*.004f);
                if(p.sqrMagnitude>0&&p.x>=0&&p.y>=0&&p.x<=Screen.width&&p.y<=Screen.height)
                {
                    float x=p.x<=edge?-1:p.x>=Screen.width-1-edge?1:0,z=p.y<=edge?-1:p.y>=Screen.height-1-edge?1:0;
                    focus+=new Vector3(x,0,z)*Time.deltaTime*zoom*CameraSpeed();
                }
            }
            if(mouse!=null)
            {
                Vector2 p=mouse.position.ReadValue();
                overUI=hud!=null&&hud.IsOverHud(p);
                if(!overUI)targetZoom=Mathf.Clamp(targetZoom-mouse.scroll.ReadValue().y*.012f,MinZoom,MaxZoom);
                hasHover=!overUI&&Physics.Raycast(MapCamera.ScreenPointToRay(p),out var hit,180);
                if(hasHover)
                {
                    // Repeat ray explicitly to keep the hit definitely assigned on all compiler versions.
                    Physics.Raycast(MapCamera.ScreenPointToRay(p),out hit,180);
                    hoverX=Mathf.FloorToInt(hit.point.x);hoverZ=Mathf.FloorToInt(hit.point.z);
                    var point=new Vector2(hit.point.x,hit.point.z);
                    if(mouse.rightButton.wasPressedThisFrame){if(Mode!=MatchMode.None)SetMode(MatchMode.None);else MoveSelection(point);}
                    if(mouse.leftButton.wasPressedThisFrame)
                    {
                        if(keys!=null&&keys.altKey.isPressed)AddPin(point);
                        else if(Mode==MatchMode.Build){if(Match.Build(hoverX,hoverZ,buildKind))SetMode(MatchMode.None);}
                        else if(Mode!=MatchMode.None)OrderTo(point);
                        else Pick(p,true);
                    }
                }
                // Air units hang above the ground, so a click may hit no terrain and still select one.
                else if(!overUI&&Mode==MatchMode.None&&mouse.leftButton.wasPressedThisFrame)Pick(p,false);
            }
            focus.x=Mathf.Clamp(focus.x,0,64);focus.z=Mathf.Clamp(focus.z,0,64);
            zoom=Mathf.Lerp(zoom,targetZoom,1-Mathf.Exp(-Time.deltaTime*12));
            focusHeight=Mathf.Lerp(focusHeight,Match.HeightAt(Mathf.Clamp(focus.x,0,63.9f),Mathf.Clamp(focus.z,0,63.9f)),1-Mathf.Exp(-Time.deltaTime*4));
            UpdateCamera();
            AnimateUnits();
            UpdateSelectionRings();
            UpdateFocusRing();
            footprint.SetActive(Mode==MatchMode.Build&&hasHover);
            if(footprint.activeSelf)
            {
                bool valid=Match.CanBuild(hoverX,hoverZ,buildKind,out var reason);int w=Match.Balance.Buildings[buildKind].Width;
                footprint.transform.position=new Vector3(hoverX+w*.5f,Match.Map.Height(hoverX,hoverZ)+.03f,hoverZ+w*.5f);
                footprint.transform.localScale=new Vector3(w,1,w);footprintMaterial.SetColor("_Color",valid?new Color(.2f,1,.4f,.5f):new Color(1,.15f,.15f,.55f));
                ModeHint=Match.Balance.Buildings[buildKind].Name+": "+reason+" · ПКМ / Esc — отмена";
                if(ghostKind!=buildKind)
                {
                    if(ghostModel!=null)Destroy(ghostModel);
                    ghostModel=scenery.Model("Placement ghost",BuildingMesh(buildKind),ghostMaterial,worldRoot.transform,false);ghostKind=buildKind;
                }
                ghostModel.SetActive(true);ghostModel.transform.position=footprint.transform.position;
            }
            else{if(ghostModel!=null)ghostModel.SetActive(false);ModeHint=Hint();}
            for(int i=0;i<pinViews.Count;i++)pinViews[i].transform.localScale=new Vector3(1,1+.08f*Mathf.Sin(Time.time*4+i),1);
            AnimateBuildings();
            scenery.Tick(Time.deltaTime);
            refresh+=Time.deltaTime;if(refresh>.15f){refresh=0;RefreshWorld();}
        }
        sealed class UnitView
        {
            public GameObject Root;
            public Transform Body, Shadow;
            public Transform[] Legs, Rotors;
            public UnitModel Model;
            public LineRenderer Beam;
            public float Phase, Height;
        }
        UnitView CreateUnitView(PrototypeUnit unit)
        {
            if(!unitModels.TryGetValue(unit.Kind,out var model))
            {
                model=MatchModels.Unit(unit.Kind);unitModels[unit.Kind]=model;
                scenery.Own(model.Body);if(model.Leg!=null)scenery.Own(model.Leg);if(model.Rotor!=null)scenery.Own(model.Rotor);
            }
            var view=new UnitView{Model=model,Root=new GameObject(unit.Stats.Name+" #"+unit.Id),Height=Match.HeightAt(unit.X,unit.Z)};
            view.Root.transform.SetParent(worldRoot.transform);view.Root.transform.localScale=Vector3.one*model.Scale;
            view.Root.transform.SetPositionAndRotation(new Vector3(unit.X,view.Height,unit.Z),Quaternion.Euler(0,180,0));
            view.Body=scenery.Model("Body",model.Body,unitMaterial,view.Root.transform).transform;view.Body.localPosition=Vector3.up*model.Hover;
            view.Legs=new Transform[model.Hips.Length];
            for(int i=0;i<view.Legs.Length;i++){view.Legs[i]=scenery.Model("Leg",model.Leg,unitMaterial,view.Body).transform;view.Legs[i].localPosition=model.Hips[i];}
            view.Rotors=new Transform[model.Rotors.Length];
            for(int i=0;i<view.Rotors.Length;i++){view.Rotors[i]=scenery.Model("Rotor",model.Rotor,unitMaterial,view.Body).transform;view.Rotors[i].localPosition=model.Rotors[i];}
            if(unit.Stats.Flying){view.Shadow=scenery.Model("Shadow",shadowMesh,shadowMaterial,worldRoot.transform,false).transform;view.Shadow.localScale=Vector3.one*unit.Stats.Radius*3.2f;}
            return view;
        }
        // Walkers swing their legs with the distance covered, air units hover and bank with spinning rotors, the drone floats.
        // Units turn toward their movement; a harvesting worker faces its deposit and draws a beam.
        void AnimateUnits()
        {
            float dt=Time.deltaTime,time=Time.time;
            foreach(var unit in Match.Units)
            {
                if(!unitViews.TryGetValue(unit.Id,out var view))unitViews[unit.Id]=view=CreateUnitView(unit);
                float ground=Match.HeightAt(unit.X,unit.Z);
                view.Height=unit.Stats.Flying?Mathf.Lerp(view.Height,ground,1-Mathf.Exp(-dt*2.5f)):ground;
                var position=new Vector3(unit.X,view.Height,unit.Z);
                var root=view.Root.transform;
                var direction=position-root.position;direction.y=0;
                float step=direction.magnitude;
                var target=unit.HarvestTarget;bool mining=!unit.Moving&&target!=null&&target.Remaining>0;
                var deposit=mining?new Vector3(target.X+.5f,Match.Map.Height(target.X,target.Z),target.Z+.5f):Vector3.zero;
                if(mining){direction=deposit-position;direction.y=0;}
                if(direction.sqrMagnitude>.0001f)root.rotation=Quaternion.Slerp(root.rotation,Quaternion.LookRotation(direction),dt*10);
                root.position=position;
                bool moving=unit.Moving;
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
                for(int i=0;i<view.Rotors.Length;i++)view.Rotors[i].localRotation=Quaternion.Euler(0,time*1300*(i%2==0?1:-1)+i*40,0);
                if(view.Shadow!=null)view.Shadow.position=new Vector3(unit.X,ground+.06f,unit.Z);
                if(mining&&view.Beam==null)
                {
                    view.Beam=new GameObject("Mining beam",typeof(LineRenderer)).GetComponent<LineRenderer>();view.Beam.transform.SetParent(view.Root.transform);
                    view.Beam.sharedMaterial=beamMaterial;view.Beam.positionCount=2;view.Beam.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                if(view.Beam!=null)view.Beam.enabled=mining;
                if(mining)
                {
                    var jitter=new Vector3(Mathf.Sin(time*23),Mathf.Sin(time*31),Mathf.Cos(time*19))*.03f;
                    view.Beam.SetPosition(0,view.Body.TransformPoint(new Vector3(0,.45f,.33f)));
                    view.Beam.SetPosition(1,deposit+Vector3.up*(target.Gas?.25f:.55f)+jitter);
                    view.Beam.widthMultiplier=.05f+.015f*Mathf.Sin(time*40);
                }
            }
            if(unitViews.Count>Match.Units.Count)
                foreach(var id in new List<int>(unitViews.Keys))
                    if(!Match.Units.Exists(u=>u.Id==id)){var view=unitViews[id];if(view.Shadow!=null)Destroy(view.Shadow.gameObject);Destroy(view.Root);unitViews.Remove(id);}
            selection.RemoveAll(u=>!Match.Units.Contains(u));
        }
        void UpdateSelectionRings()
        {
            while(selectionRings.Count<selection.Count)selectionRings.Add(scenery.Model("Selection",ringMesh,selectedMaterial,worldRoot.transform,false));
            for(int i=0;i<selectionRings.Count;i++)
            {
                bool active=i<selection.Count;selectionRings[i].SetActive(active);
                if(!active)continue;
                var unit=selection[i];
                // An air unit's ring hangs just under its body; on the ground it would sit far below on screen.
                float y=unit.Stats.Flying&&unitViews.TryGetValue(unit.Id,out var view)?view.Body.position.y-.3f:Match.HeightAt(unit.X,unit.Z)+.02f;
                selectionRings[i].transform.position=new Vector3(unit.X,y,unit.Z);
                selectionRings[i].transform.localScale=Vector3.one*(unit.Stats.Radius+.14f);
            }
        }
        // Units are picked on screen by their visible body; otherwise the ground cell picks a building or an explored deposit.
        void Pick(Vector2 screen,bool ground)
        {
            var unit=UnitAt(screen);
            selection.Clear();selectedBuilding=-1;selectedDeposit=null;
            if(unit!=null)selection.Add(unit);
            else if(ground)
            {
                selectedBuilding=Match.Buildings.FindIndex(b=>hoverX>=b.X&&hoverX<b.X+b.Width&&hoverZ>=b.Z&&hoverZ<b.Z+b.Width);
                selectedDeposit=selectedBuilding>=0||!Match.Map.Inside(hoverX,hoverZ)||!Match.Explored[hoverX,hoverZ]?null:Match.Map.ResourceAt(hoverX,hoverZ);
            }
            hud?.Refresh();
        }
        PrototypeUnit UnitAt(Vector2 screen)
        {
            PrototypeUnit best=null;float bestDistance=float.MaxValue;
            foreach(var unit in Match.Units)
            {
                if(!unitViews.TryGetValue(unit.Id,out var view))continue;
                // Distance on screen to the unit's vertical axis, from its feet (or the bottom of a hovering body) to the top.
                var model=view.Model;var foot=view.Root.transform.position;
                var low=model.Hover>0?view.Body.position+Vector3.down*.25f:foot+Vector3.up*.1f*model.Scale;
                var high=model.Hover>0?view.Body.position+Vector3.up*.25f:foot+Vector3.up*model.Height*model.Scale;
                Vector3 a=MapCamera.WorldToScreenPoint(low),b=MapCamera.WorldToScreenPoint(high);if(a.z<=0||b.z<=0)continue;
                float reach=Vector2.Distance(a,MapCamera.WorldToScreenPoint(low+MapCamera.transform.right*(unit.Stats.Radius*1.2f+.12f)));
                Vector2 ab=(Vector2)b-(Vector2)a;float t=ab.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(screen-(Vector2)a,ab)/ab.sqrMagnitude):0;
                float distance=Vector2.Distance(screen,(Vector2)a+ab*t);
                if(distance<reach&&distance<bestDistance){best=unit;bestDistance=distance;}
            }
            return best;
        }
        // Soft dark disc (uv 0–1) under air units.
        static Mesh SoftQuad()
        {
            var mesh=new Mesh{name="Soft quad"};
            mesh.SetVertices(new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)});
            mesh.SetUVs(0,new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});
            mesh.SetColors(new[]{Color.white,Color.white,Color.white,Color.white});
            mesh.SetTriangles(new[]{0,1,2,0,2,3},0);mesh.RecalculateBounds();
            return mesh;
        }
        void UpdateFocusRing()
        {
            Vector3 position=default;float radius=0;
            if(selectedBuilding>=0&&selectedBuilding<Match.Buildings.Count)
            {
                var b=Match.Buildings[selectedBuilding];position=new Vector3(b.X+b.Width*.5f,Match.Map.Height(b.X,b.Z),b.Z+b.Width*.5f);radius=b.Width*.76f;
                focusRing.GetComponent<MeshRenderer>().sharedMaterial=selectedMaterial;
            }
            else if(selectedDeposit!=null&&selectedDeposit.Remaining>0)
            {
                position=new Vector3(selectedDeposit.X+.5f,Match.Map.Height(selectedDeposit.X,selectedDeposit.Z),selectedDeposit.Z+.5f);radius=.68f;
                focusRing.GetComponent<MeshRenderer>().sharedMaterial=neutralMaterial;
            }
            focusRing.SetActive(radius>0);
            if(radius>0){focusRing.transform.position=position;focusRing.transform.localScale=Vector3.one*radius*(1+.025f*Mathf.Sin(Time.time*5));}
        }
        float Option(string id,float fallback){var settings=services?.App?.Settings;return settings.HasValue?MenuCatalog.Value(settings.Value,MenuCatalog.Find(id)):fallback;}
        // "Скорость камеры" 0–100 maps to 0.4–1.6 of the base speed; 50 keeps the original pace.
        float CameraSpeed()=>.4f+Option("controls.cameraSpeed",50)/100f*1.2f;
        string Hint()=>Mode==MatchMode.Build?"Укажите место на карте · ПКМ / Esc — отмена":Mode==MatchMode.Move?"Движение: ЛКМ — точка на карте или миникарте · ПКМ / Esc — отмена"
            :Mode==MatchMode.Harvest?"Добыча: ЛКМ — разведанная залежь · ПКМ / Esc — отмена":Mode==MatchMode.Ping?"Метка: ЛКМ — место на карте или миникарте · Alt+ЛКМ — всегда":"";
        // Zooming in lowers the camera toward the horizon, as in StarCraft; it follows the terrain height smoothly.
        // The focus sits in the middle of the view left free by the HUD deck, not in the middle of the screen.
        void UpdateCamera()
        {
            MapCamera.transform.rotation=Quaternion.Euler(Mathf.Lerp(46,57,Mathf.InverseLerp(MinZoom,MaxZoom,zoom)),0,0);
            var target=new Vector3(focus.x,focusHeight,focus.z);
            MapCamera.transform.position=target-MapCamera.transform.forward*(zoom/Mathf.Tan(MapCamera.fieldOfView*Mathf.Deg2Rad*.5f));
            if(new Plane(Vector3.up,target).Raycast(MapCamera.ViewportPointToRay(new Vector3(.5f,.59f,0)),out float d))
            {var seen=MapCamera.ViewportPointToRay(new Vector3(.5f,.59f,0)).GetPoint(d);MapCamera.transform.position+=new Vector3(target.x-seen.x,0,target.z-seen.z);}
        }
        void AnimateBuildings()
        {
            for(int i=0;i<buildingViews.Count;i++)
            {
                var b=Match.Buildings[i];float h=Match.Balance.Buildings[b.Kind].Height,ground=Match.Map.Height(b.X,b.Z);
                // The full-sized model rises through the opaque terrain; it never scales up.
                buildingViews[i].transform.position=new Vector3(b.X+b.Width*.5f,ground-h*(1-b.Ready),b.Z+b.Width*.5f);
                var ring=constructionRings[i];ring.SetActive(b.Ready<1);
                if(b.Ready<1){ring.transform.position=new Vector3(b.X+b.Width*.5f,ground,b.Z+b.Width*.5f);ring.transform.localScale=Vector3.one*b.Width*(.74f+.04f*Mathf.Sin(Time.time*6));}
            }
        }
        void RefreshWorld()
        {
            scenery.SetVision(Match.Visible,Match.Explored);
            if(hud!=null)Shader.SetGlobalColor("_OrderTeamColor",hud.OwnColor);
            for(int i=0;i<depositViews.Count;i++)
            {
                var r=Match.Map.Resources[i];depositViews[i].SetActive(Match.Explored[r.X,r.Z]&&r.Remaining>0);
                // Crystals shrink as they are mined out.
                if(!r.Gas)depositViews[i].transform.localScale=new Vector3(1,Mathf.Lerp(.4f,1,r.Remaining/(float)Mathf.Max(1,Match.Balance.MineralDeposit)),1);
            }
            while(buildingViews.Count<Match.Buildings.Count)
            {
                var b=Match.Buildings[buildingViews.Count];
                buildingViews.Add(scenery.Model(Match.Balance.Buildings[b.Kind].Name,BuildingMesh(b.Kind),buildingMaterial,worldRoot.transform));
                constructionRings.Add(scenery.Model("Construction",ringMesh,constructionMaterial,worldRoot.transform,false));
            }
            AnimateBuildings();
            RefreshMinimap();hud?.Refresh();
        }
        // Terrain, fog, deposits and buildings; the camera frame, worker, route and pins are vector overlays in the HUD.
        void RefreshMinimap()
        {
            const int t=TempleScenery.FogTexels;int n=TempleMap.Size*t;
            var pixels=new Color[n*n];var own=hud?.OwnColor??Color.white;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float lit=Match.Visible[x/t,z/t]?1:Match.Explored[x/t,z/t]?.32f:.05f;
                var c=miniBase[z*n+x]*lit;c.a=1;pixels[z*n+x]=c;
            }
            void Cell(int cx,int cz,Color color){for(int z=cz*t;z<cz*t+t;z++)for(int x=cx*t;x<cx*t+t;x++)pixels[z*n+x]=color;}
            foreach(var r in Match.Map.Resources)if(Match.Explored[r.X,r.Z]&&r.Remaining>0)Cell(r.X,r.Z,(r.Gas?new Color(.3f,.9f,.6f):new Color(.35f,.75f,1))*(Match.Visible[r.X,r.Z]?1:.4f));
            foreach(var b in Match.Buildings)for(int z=b.Z;z<b.Z+b.Width;z++)for(int x=b.X;x<b.X+b.Width;x++)Cell(x,z,own*(b.Ready>=1?1:.6f));
            miniTexture.SetPixels(pixels);miniTexture.Apply(false);
        }
        void BuildUI()
        {
            int n=TempleMap.Size*TempleScenery.FogTexels;
            miniTexture=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            miniBase=scenery.MinimapBase();
            hud=new MatchHud(ui,this,services);
        }
        public void SetMode(MatchMode mode,int kind=-1)
        {
            if(mode==MatchMode.Build&&(kind<0||kind>1))mode=MatchMode.None;
            if((mode==MatchMode.Move&&selection.Count==0)||(mode==MatchMode.Harvest&&!selection.Exists(u=>u.Stats.HarvestAmount>0)))SelectWorker();
            Mode=mode;buildKind=mode==MatchMode.Build?kind:-1;ModeHint=Hint();hud?.Refresh();
        }
        public void SelectWorker()
        {
            selection.Clear();var worker=Match.Units.Find(u=>u.Kind==UnitKind.Worker);if(worker!=null)selection.Add(worker);
            selectedBuilding=-1;selectedDeposit=null;hud?.Refresh();
        }
        public void Focus(Vector2 point)=>focus=new Vector3(Mathf.Clamp(point.x,0,64),0,Mathf.Clamp(point.y,0,64));
        public void FocusWorker(){var worker=Match.Units.Find(u=>u.Kind==UnitKind.Worker);if(worker!=null)focus=new Vector3(worker.X,0,worker.Z);}
        // Camera to the middle of the selected units; without a selection, to the worker.
        public void FocusSelection()
        {
            if(selection.Count==0){FocusWorker();return;}
            float x=0,z=0;foreach(var u in selection){x+=u.X;z+=u.Z;}
            focus=new Vector3(x/selection.Count,0,z/selection.Count);
        }
        // Every selected unit goes to the point; workers harvest a deposit there, the rest walk up to it.
        public bool MoveSelection(Vector2 point)
        {
            bool any=false;
            foreach(var unit in selection)any|=Match.Move(unit,point.x,point.y);
            return any;
        }
        public void Stop(){foreach(var unit in selection)Match.Stop(unit);SetMode(MatchMode.None);}
        public void FocusBase()
        {
            var hq=Match.Buildings.Find(b=>b.Kind==2);
            if(hq!=null)focus=new Vector3(hq.X+hq.Width*.5f,0,hq.Z+hq.Width*.5f);
        }
        // Order from the map or minimap in the current mode; without a mode it is a move/harvest order.
        public void OrderTo(Vector2 point)
        {
            if(Mode==MatchMode.Ping){AddPin(point);return;}
            if(Mode==MatchMode.Build){if(Match.Build((int)point.x,(int)point.y,buildKind))SetMode(MatchMode.None);return;}
            if(Mode==MatchMode.Harvest)
            {
                var deposit=Match.Map.Inside((int)point.x,(int)point.y)&&Match.Explored[(int)point.x,(int)point.y]?Match.Map.ResourceAt((int)point.x,(int)point.y):null;
                if(deposit==null){Match.Message="Укажите разведанную залежь.";return;}
                if(!selection.Exists(u=>u.Stats.HarvestAmount>0))SelectWorker();
                selection.RemoveAll(u=>u.Stats.HarvestAmount<=0);
            }
            if(selection.Count==0)SelectWorker();
            MoveSelection(point);SetMode(MatchMode.None);
        }
        public void AddPin(Vector2 point)
        {
            if(pins.Count>=9){pins.RemoveAt(0);Destroy(pinViews[0]);pinViews.RemoveAt(0);}
            pins.Add(point);
            var height=Match.HeightAt(Mathf.Clamp(point.x,0,63.9f),Mathf.Clamp(point.y,0,63.9f));
            var beacon=scenery.Model("Pin",beaconMesh,beaconMaterial,worldRoot.transform,false);
            beacon.transform.position=new Vector3(point.x,height,point.y);
            pinViews.Add(beacon);
            if(Mode==MatchMode.Ping)SetMode(MatchMode.None);
            Match.Message=$"Метка {pins.Count}: {MapGrid.Name(point.x,point.y,TempleMap.Size)}.";
            hud?.Refresh();
        }
        public void ClearPins()
        {
            foreach(var view in pinViews)if(view!=null)Destroy(view);
            pinViews.Clear();pins.Clear();hud?.Refresh();
        }
        // Camera view projected onto the ground plane, as (x, z) points for the minimap frame.
        public Vector2[] ViewportOnGround()
        {
            if(MapCamera==null)return null;
            var result=new Vector2[4];Vector2[] corners={new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
            // Project onto the terrain level under the camera focus, not y=0, so the frame matches what is on screen.
            var plane=new Plane(Vector3.up,new Vector3(0,Match.HeightAt(Mathf.Clamp(focus.x,0,63.9f),Mathf.Clamp(focus.z,0,63.9f)),0));
            for(int i=0;i<4;i++){var ray=MapCamera.ViewportPointToRay(corners[i]);if(!plane.Raycast(ray,out float d))return null;var p=ray.GetPoint(d);result[i]=new Vector2(p.x,p.z);}
            return result;
        }
        public void Exit()=>onExit?.Invoke();
        public void Shutdown()
        {
            enabled=false;
            hud?.Dispose();hud=null;
            if(ecsWorld!=null&&ecsWorld.IsCreated&&ecsWorld.EntityManager.Exists(matchEntity))ecsWorld.EntityManager.DestroyEntity(matchEntity);
            if(worldRoot!=null){worldRoot.SetActive(false);Destroy(worldRoot);worldRoot=null;}
            scenery?.Dispose();scenery=null;
            if(miniTexture!=null){Destroy(miniTexture);miniTexture=null;}
            buildingViews.Clear();constructionRings.Clear();depositViews.Clear();pinViews.Clear();pins.Clear();Match=null;
            unitViews.Clear();unitModels.Clear();selection.Clear();selectionRings.Clear();
            Array.Clear(buildingMeshes,0,buildingMeshes.Length);
            ghostModel=null;ghostKind=-1;Mode=MatchMode.None;
        }
        void OnDestroy()=>Shutdown();
    }
}
