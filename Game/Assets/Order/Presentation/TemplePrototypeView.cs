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
        public PrototypeBuilding SelectedBuilding => selectedBuilding;
        public ResourceDeposit SelectedDeposit => selectedDeposit;
        public IReadOnlyList<Vector2> Pins => pins;
        public Texture MinimapTexture => miniTexture;
        public string ModeHint { get; private set; } = "";
        public UnitSelection Selection => selection;
        VisualElement ui;
        MatchHud hud;
        MatchServices services;
        TempleScenery scenery;
        readonly List<Vector2> pins = new List<Vector2>();
        readonly List<GameObject> pinViews = new List<GameObject>();
        Texture2D miniTexture;
        Color[] miniBase;
        GameObject worldRoot, footprint, focusRing, ghostModel, rallyFlag;
        LineRenderer rallyLine;
        int ghostKind=-1;
        sealed class BuildingView{public GameObject Model,Ring;}
        readonly Dictionary<PrototypeBuilding,BuildingView> buildingViews=new Dictionary<PrototypeBuilding,BuildingView>();
        readonly List<GameObject> selectionRings = new List<GameObject>(), hoverRings = new List<GameObject>();
        readonly List<GameObject> depositViews = new List<GameObject>();
        UnitSelection selection = new UnitSelection();
        readonly List<PrototypeUnit> hovered = new List<PrototypeUnit>();
        readonly List<Marker> markers = new List<Marker>();
        static readonly Key[] Digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
        Vector2 dragStart, lastClickAt;
        bool pressing, dragging;
        PrototypeUnit lastClickUnit;
        float lastClickTime, lastGroupTime;
        int lastGroup=-1;
        MaterialPropertyBlock markerColor;
        readonly Dictionary<int, UnitView> unitViews = new Dictionary<int, UnitView>();
        readonly Dictionary<UnitKind, UnitModel> unitModels = new Dictionary<UnitKind, UnitModel>();
        readonly Mesh[] buildingMeshes = new Mesh[3];
        Mesh ringMesh, beaconMesh, shadowMesh, selectionMesh, markerMesh, cargoMesh;
        Material buildingMaterial, enemyBuildingMaterial, ghostMaterial, footprintMaterial, selectedMaterial, neutralMaterial, constructionMaterial, beaconMaterial, unitMaterial, enemyMaterial, beamMaterial, shadowMaterial,
            unitRingMaterial, hoverRingMaterial, enemyRingMaterial, enemyHoverMaterial, markerMaterial, crystalMaterial, gasCargoMaterial;
        // Enemy team colour on models and the minimap.
        public static readonly Color EnemyColor = new Color(.92f, .16f, .12f);
        CombatEffects combat;
        // Strikes and deaths reported by the simulation, shown on the next frame.
        readonly List<(PrototypeUnit attacker, PrototypeUnit target)> strikes = new List<(PrototypeUnit, PrototypeUnit)>();
        readonly List<PrototypeUnit> deaths = new List<PrototypeUnit>();
        readonly List<(PrototypeUnit attacker, PrototypeBuilding target)> buildingStrikes = new List<(PrototypeUnit, PrototypeBuilding)>();
        readonly List<PrototypeBuilding> ruins = new List<PrototypeBuilding>();
        readonly List<LineRenderer> waypointLines = new List<LineRenderer>();
        readonly List<GameObject> waypointDots = new List<GameObject>();
        readonly List<Vector3> waypointPoints = new List<Vector3>();
        Entity matchEntity;
        World ecsWorld;
        Vector3 focus = new Vector3(13,0,13);
        float zoom=StartZoom, targetZoom=StartZoom, focusHeight, refresh;
        int buildKind=-1, hoverX, hoverZ;
        PrototypeBuilding selectedBuilding;
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
            enabled=true;Mode=MatchMode.None;buildKind=-1;selectedBuilding=null;selectedDeposit=null;focus=new Vector3(13,0,13);zoom=targetZoom=StartZoom;refresh=0;
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
            enemyBuildingMaterial=scenery.Prop("Enemy buildings",m=>{m.SetFloat("_Panels",1);m.SetFloat("_Gloss",.35f);m.SetColor("_TeamColor",EnemyColor);m.SetFloat("_TeamOverride",1);});
            ghostMaterial=scenery.Prop("Placement ghost",m=>{m.SetFloat("_Opacity",.38f);m.SetFloat("_ZWrite",0);m.SetFloat("_Fog",0);m.renderQueue=3000;});
            unitMaterial=scenery.Prop("Units",m=>{m.SetFloat("_Fog",0);m.SetFloat("_Gloss",.45f);});
            enemyMaterial=scenery.Prop("Enemy units",m=>{m.SetFloat("_Fog",0);m.SetFloat("_Gloss",.45f);m.SetColor("_TeamColor",EnemyColor);m.SetFloat("_TeamOverride",1);});
            crystalMaterial=scenery.Prop("Crystals",m=>{m.SetFloat("_Rim",1.1f);m.SetFloat("_Gloss",.9f);m.SetFloat("_Emission",2.2f);});
            gasCargoMaterial=scenery.Prop("Gas cargo",m=>{m.SetColor("_BaseColor",new Color(.5f,1,.55f));m.SetFloat("_Emission",3);m.SetFloat("_Fog",0);});
            var ventMaterial=scenery.Prop("Gas vents");
            footprintMaterial=scenery.Glow("Placement footprint",Color.green,false,m=>m.SetFloat("_Grid",1));
            selectedMaterial=scenery.Glow("Selection",new Color(.3f,1,.42f)*1.5f);
            neutralMaterial=scenery.Glow("Selection neutral",new Color(1,.92f,.6f)*1.2f);
            constructionMaterial=scenery.Glow("Construction",new Color(1,.62f,.22f)*1.4f);
            beaconMaterial=scenery.Glow("Beacon",new Color(1,.42f,.24f)*2,true,m=>m.SetFloat("_FadeHeight",7));
            ringMesh=scenery.Own(MatchModels.Ring(.84f));beaconMesh=scenery.Own(MatchModels.Beacon());
            var minerals=new Mesh[4];for(int i=0;i<minerals.Length;i++)minerals[i]=scenery.Own(MatchModels.Minerals(17+i*31));
            cargoMesh=minerals[0];
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
            // StarCraft-like green circles under selected units, a faint one under the hovered unit, and order markers.
            unitRingMaterial=scenery.Glow("Selected unit",new Color(.3f,1.5f,.45f,.95f),false);
            hoverRingMaterial=scenery.Glow("Hovered unit",new Color(.85f,1,.9f,.5f),false);
            enemyRingMaterial=scenery.Glow("Selected enemy",new Color(1.6f,.3f,.24f,.95f),false);
            enemyHoverMaterial=scenery.Glow("Hovered enemy",new Color(1,.55f,.5f,.55f),false);
            markerMaterial=scenery.Glow("Order marker",Color.white,false);
            selectionMesh=scenery.Own(MatchModels.SelectionRing());markerMesh=scenery.Own(MatchModels.MoveMarker());
            markerColor=new MaterialPropertyBlock();
            focusRing=scenery.Model("Selection",ringMesh,selectedMaterial,worldRoot.transform,false);
            rallyFlag=scenery.Model("Rally point",beaconMesh,selectedMaterial,worldRoot.transform,false);rallyFlag.transform.localScale=new Vector3(.35f,.3f,.35f);rallyFlag.SetActive(false);
            rallyLine=new GameObject("Rally line",typeof(LineRenderer)).GetComponent<LineRenderer>();rallyLine.transform.SetParent(worldRoot.transform,false);
            rallyLine.sharedMaterial=markerMaterial;rallyLine.widthMultiplier=.06f;rallyLine.startColor=rallyLine.endColor=new Color(.35f,1,.5f,.55f);
            rallyLine.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;rallyLine.enabled=false;
            combat=new CombatEffects(scenery,worldRoot.transform,MapCamera,shadowMesh,ringMesh);
            Match.Attacked+=(attacker,target)=>strikes.Add((attacker,target));Match.Died+=deaths.Add;
            Match.AttackedBuilding+=(attacker,target)=>buildingStrikes.Add((attacker,target));Match.Destroyed+=ruins.Add;
            Match.AddTestSquad();Match.AddEnemySquad();Match.AddEnemyBase();SelectWorker();
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
                SelectionKeys(keys);
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
                bool onGround=GroundPoint(p,out var point);hasHover=!overUI&&onGround;
                if(hasHover)
                {
                    hoverX=Mathf.FloorToInt(point.x);hoverZ=Mathf.FloorToInt(point.y);
                    // Right click on a visible enemy attacks it, anywhere else moves; in a targeting mode it cancels the mode.
                    if(mouse.rightButton.wasPressedThisFrame)
                    {
                        if(Mode!=MatchMode.None)SetMode(MatchMode.None);
                        else if(!selection.Own&&selectedBuilding!=null&&selectedBuilding.Owner==PrototypeMatch.Player)SetRallyPoint(point);
                        else{bool queue=Queueing();var enemy=EnemyAt(p);var structure=enemy==null?EnemyBuildingAt(p):null;if(enemy!=null&&selection.Own)AttackSelection(enemy,queue);else if(structure!=null&&selection.Own)AttackSelection(structure,queue);else MoveSelection(point,queue);}
                    }
                    if(mouse.leftButton.wasPressedThisFrame)
                    {
                        if(keys!=null&&keys.altKey.isPressed)AddPin(point);
                        else if(Mode==MatchMode.Build){if(Match.Build(hoverX,hoverZ,buildKind))SetMode(MatchMode.None);}
                        else if(Mode==MatchMode.Attack)
                        {
                            // With Shift the order is queued and the attack mode stays on for the next target, as in StarCraft.
                            bool queue=Queueing();var enemy=EnemyAt(p);var structure=enemy==null?EnemyBuildingAt(p):null;
                            if(enemy!=null)AttackSelection(enemy,queue);else if(structure!=null)AttackSelection(structure,queue);else AttackMoveSelection(point,queue);
                            if(!queue)SetMode(MatchMode.None);
                        }
                        else if(Mode!=MatchMode.None)OrderTo(point);
                        else{pressing=true;dragStart=p;}
                    }
                }
                // Air units hang above the ground, so a selection may start where no terrain is hit.
                else if(!overUI&&Mode==MatchMode.None&&mouse.leftButton.wasPressedThisFrame){pressing=true;dragStart=p;}
                // Selection happens on release: a click picks, a drag boxes; units under the box light up while dragging.
                hovered.Clear();
                if(pressing)
                {
                    if(!dragging&&(p-dragStart).magnitude>6)dragging=true;
                    var box=new Rect(Mathf.Min(dragStart.x,p.x),Mathf.Min(dragStart.y,p.y),Mathf.Abs(p.x-dragStart.x),Mathf.Abs(p.y-dragStart.y));
                    if(dragging){UnitsIn(box,hovered);hud?.SetSelectionBox(box);}
                    if(!mouse.leftButton.isPressed)
                    {
                        bool shift=keys!=null&&keys.shiftKey.isPressed,ctrl=keys!=null&&keys.ctrlKey.isPressed;
                        if(dragging)BoxSelect(hovered,shift);else Click(p,shift,ctrl,hasHover);
                        pressing=dragging=false;hovered.Clear();hud?.SetSelectionBox(null);
                    }
                }
                else if(!overUI&&Mode==MatchMode.None){var unit=UnitAt(p);if(unit!=null)hovered.Add(unit);}
            }
            focus.x=Mathf.Clamp(focus.x,0,64);focus.z=Mathf.Clamp(focus.z,0,64);
            zoom=Mathf.Lerp(zoom,targetZoom,1-Mathf.Exp(-Time.deltaTime*12));
            focusHeight=Mathf.Lerp(focusHeight,Match.HeightAt(Mathf.Clamp(focus.x,0,63.9f),Mathf.Clamp(focus.z,0,63.9f)),1-Mathf.Exp(-Time.deltaTime*4));
            UpdateCamera();
            ShowCombat();
            AnimateUnits();
            UpdateSelectionRings();
            UpdateWaypoints();
            UpdateFocusRing();
            AnimateMarkers();
            combat.Tick(Time.deltaTime);
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
            // A chunk of minerals or a gas capsule carried home.
            public GameObject Cargo;
            // Kick runs from 1 to 0 after a strike: a melee lunge or a recoil.
            public float Phase, Height, Kick;
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
            // Enemies start facing south-west, toward the player's base.
            view.Root.transform.SetPositionAndRotation(new Vector3(unit.X,view.Height,unit.Z),Quaternion.Euler(0,unit.Owner==PrototypeMatch.Player?180:225,0));
            var material=unit.Owner==PrototypeMatch.Player?unitMaterial:enemyMaterial;
            view.Body=scenery.Model("Body",model.Body,material,view.Root.transform).transform;view.Body.localPosition=Vector3.up*model.Hover;
            view.Legs=new Transform[model.Hips.Length];
            for(int i=0;i<view.Legs.Length;i++){view.Legs[i]=scenery.Model("Leg",model.Leg,material,view.Body).transform;view.Legs[i].localPosition=model.Hips[i];}
            view.Rotors=new Transform[model.Rotors.Length];
            for(int i=0;i<view.Rotors.Length;i++){view.Rotors[i]=scenery.Model("Rotor",model.Rotor,material,view.Body).transform;view.Rotors[i].localPosition=model.Rotors[i];}
            if(unit.Stats.Flying){view.Shadow=scenery.Model("Shadow",shadowMesh,shadowMaterial,worldRoot.transform,false).transform;view.Shadow.localScale=Vector3.one*unit.Stats.Radius*3.2f;}
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
                bool seen=Match.SeenBy(PrototypeMatch.Player,unit);
                if(view.Root.activeSelf!=seen){view.Root.SetActive(seen);if(view.Shadow!=null)view.Shadow.gameObject.SetActive(seen);}
                float ground=Match.HeightAt(unit.X,unit.Z);
                if(unit.Stats.Flying)
                {
                    // Climb early and fast, sink late and slowly: the flight level follows the highest ground around
                    // the unit and ahead along its course, so it clears a cliff before reaching the edge.
                    float level=FlightGround(unit);
                    view.Height=Mathf.Lerp(view.Height,level,1-Mathf.Exp(-dt*(level>view.Height?8:2.2f)));
                }
                else view.Height=ground;
                var position=new Vector3(unit.X,view.Height,unit.Z);
                var root=view.Root.transform;
                var direction=position-root.position;direction.y=0;
                float step=direction.magnitude;
                var target=unit.HarvestTarget;bool mining=unit.Mining&&target!=null&&target.Remaining>0;
                var deposit=mining?new Vector3(target.X+.5f,Match.Map.Height(target.X,target.Z),target.Z+.5f):Vector3.zero;
                if(mining){direction=deposit-position;direction.y=0;}
                // A moving unit faces its next waypoint, so shoves do not twist it; a shoved idle unit keeps its facing.
                if(unit.Moving&&!mining)foreach(var next in unit.Route){direction=new Vector3(next.X-unit.X,0,next.Z-unit.Z);break;}
                else if(unit.Target!=null)direction=new Vector3(unit.Target.X-unit.X,0,unit.Target.Z-unit.Z);
                else if(unit.TargetBuilding!=null){var b=unit.TargetBuilding;direction=new Vector3(Mathf.Clamp(unit.X,b.X,b.X+b.Width)-unit.X,0,Mathf.Clamp(unit.Z,b.Z,b.Z+b.Width)-unit.Z);}
                else if(!mining)direction=Vector3.zero;
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
                bool carrying=unit.Carrying>0;
                if(carrying&&view.Cargo==null)
                {
                    view.Cargo=scenery.Model("Cargo",cargoMesh,crystalMaterial,view.Body,false);
                    view.Cargo.transform.localPosition=new Vector3(0,.06f,.3f);view.Cargo.transform.localScale=Vector3.one*.22f;
                }
                if(view.Cargo!=null)
                {
                    if(view.Cargo.activeSelf!=carrying)view.Cargo.SetActive(carrying);
                    if(carrying)view.Cargo.GetComponent<MeshRenderer>().sharedMaterial=unit.CarriesGas?gasCargoMaterial:crystalMaterial;
                }
                if(seen&&(unit.Health<unit.Stats.Health||selection.Contains(unit)||hovered.Contains(unit)))
                    combat.Bar(Top(unit,view)+Vector3.up*.22f,Mathf.Max(.62f,unit.Stats.Radius*2.3f),unit.Health/(float)unit.Stats.Health);
            }
            foreach(var (b,view) in buildingViews)
            {
                var stats=Match.Balance.Buildings[b.Kind];
                if(!view.Model.activeSelf||(b.Owner!=PrototypeMatch.Player&&!Match.SeenBy(PrototypeMatch.Player,b)))continue;
                if(b.Health<stats.Health||selectedBuilding==b)
                    combat.Bar(new Vector3(b.X+b.Width*.5f,Match.Map.Height(b.X,b.Z)+stats.Height+.45f,b.Z+b.Width*.5f),b.Width*.8f,b.Health/(float)stats.Health);
            }
            combat.EndBars();
            if(unitViews.Count>Match.Units.Count)
            {
                foreach(var id in new List<int>(unitViews.Keys))
                    if(!Match.Units.Exists(u=>u.Id==id)){var view=unitViews[id];if(view.Shadow!=null)Destroy(view.Shadow.gameObject);Destroy(view.Root);unitViews.Remove(id);}
                selection.Prune(Match.Units);
            }
        }
        // Selected units get a green circle, the hovered unit (or units under the selection box) a faint one.
        // Circles follow the slope of a ramp; an air unit's circle hangs just under its body, so it stays next to it on screen.
        // Map point under the cursor. A click on a cliff face means the ground at its foot, as in StarCraft: the point moves
        // off the wall to the side it faces, so orders, markers and pins never land on the wall itself.
        bool GroundPoint(Vector2 screen,out Vector2 point)
        {
            point=default;
            if(!Physics.Raycast(MapCamera.ScreenPointToRay(screen),out var hit,180))return false;
            var at=hit.point;
            // Past the sloped foot and plinth of the wall (about 0.3), with room for a unit to stand.
            if(hit.normal.y<.5f){var away=new Vector3(hit.normal.x,0,hit.normal.z).normalized;at+=away*.55f;}
            point=new Vector2(at.x,at.z);
            return true;
        }
        // Highest terrain within a body's width of the unit and up to two and a half cells ahead on its route.
        float FlightGround(PrototypeUnit unit)
        {
            float H(float x,float z)=>Match.HeightAt(Mathf.Clamp(x,0,63.99f),Mathf.Clamp(z,0,63.99f));
            float r=unit.Stats.Radius+.3f,level=H(unit.X,unit.Z);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;level=Mathf.Max(level,H(unit.X+Mathf.Cos(a)*r,unit.Z+Mathf.Sin(a)*r));}
            foreach(var next in unit.Route)
            {
                var ahead=new Vector2(next.X-unit.X,next.Z-unit.Z);float distance=Mathf.Min(2.5f,ahead.magnitude);
                if(distance<.01f)break;ahead/=ahead.magnitude;
                for(float d=.5f;d<=distance;d+=.5f)level=Mathf.Max(level,H(unit.X+ahead.x*d,unit.Z+ahead.y*d));
                break;
            }
            return level;
        }
        void UpdateSelectionRings()
        {
            int shown=0,hints=0;
            foreach(var unit in selection.Units)Ring(selectionRings,shown++,unit,unit.Owner==PrototypeMatch.Player?unitRingMaterial:enemyRingMaterial,1);
            foreach(var unit in hovered)if(!selection.Contains(unit))Ring(hoverRings,hints++,unit,unit.Owner==PrototypeMatch.Player?hoverRingMaterial:enemyHoverMaterial,1.06f);
            for(int i=shown;i<selectionRings.Count;i++)selectionRings[i].SetActive(false);
            for(int i=hints;i<hoverRings.Count;i++)hoverRings[i].SetActive(false);
        }
        void Ring(List<GameObject> pool,int index,PrototypeUnit unit,Material material,float scale)
        {
            while(pool.Count<=index)pool.Add(scenery.Model("Selection circle",selectionMesh,material,worldRoot.transform,false));
            var ring=pool[index];ring.SetActive(true);ring.GetComponent<MeshRenderer>().sharedMaterial=material;
            float y;Quaternion tilt;
            if(unit.Stats.Flying&&unitViews.TryGetValue(unit.Id,out var view)){y=view.Body.position.y-.3f;tilt=Quaternion.identity;}
            else{y=Match.HeightAt(unit.X,unit.Z)+.03f;tilt=Quaternion.FromToRotation(Vector3.up,GroundNormal(unit.X,unit.Z));}
            ring.transform.SetPositionAndRotation(new Vector3(unit.X,y,unit.Z),tilt);
            ring.transform.localScale=Vector3.one*(unit.Stats.Radius+.16f)*scale;
        }
        // Slope under a point for circles and markers: ramps tilt them, a nearby cliff does not (samples across a drop count as level).
        Vector3 GroundNormal(float x,float z)
        {
            const float e=.3f;
            float H(float px,float pz)=>Match.HeightAt(Mathf.Clamp(px,0,63.99f),Mathf.Clamp(pz,0,63.99f));
            float h=H(x,z);
            float S(float px,float pz){float s=H(px,pz);return Mathf.Abs(s-h)>.51f?h:s;}
            return new Vector3(S(x-e,z)-S(x+e,z),2*e,S(x,z-e)-S(x,z+e)).normalized;
        }
        // Click: a unit (Shift toggles, Ctrl or a double click takes all of its kind on screen); otherwise the ground cell
        // picks a building or an explored deposit, and an empty click clears the selection.
        void Click(Vector2 screen,bool shift,bool ctrl,bool ground)
        {
            var unit=UnitAt(screen);
            if(unit!=null)
            {
                bool twice=lastClickUnit!=null&&lastClickUnit.Kind==unit.Kind&&Time.unscaledTime-lastClickTime<.35f&&(screen-lastClickAt).magnitude<14;
                selection.Click(unit,shift,ctrl||twice,UnitsOnScreen());
                lastClickUnit=twice?null:unit;lastClickTime=Time.unscaledTime;lastClickAt=screen;
                selectedBuilding=null;selectedDeposit=null;
            }
            else if(!shift)
            {
                selection.Clear();lastClickUnit=null;selectedBuilding=null;selectedDeposit=null;
                if(ground)
                {
                    selectedBuilding=BuildingAt(screen)??Match.Buildings.Find(b=>BuildingShown(b)&&hoverX>=b.X&&hoverX<b.X+b.Width&&hoverZ>=b.Z&&hoverZ<b.Z+b.Width);
                    selectedDeposit=selectedBuilding!=null||!Match.Map.Inside(hoverX,hoverZ)||!Match.Explored[hoverX,hoverZ]?null:Match.Map.ResourceAt(hoverX,hoverZ);
                }
            }
            hud?.Refresh();
        }
        void BoxSelect(List<PrototypeUnit> inside,bool shift)
        {
            selection.Box(inside,shift);
            if(selection.Count>0||!shift){selectedBuilding=null;selectedDeposit=null;}
            hud?.Refresh();
        }
        // On screen a unit is a segment along its body: from the feet (or below a hovering body) to the top of the model.
        bool ScreenAxis(PrototypeUnit unit,out Vector2 a,out Vector2 b,out float reach)
        {
            a=b=default;reach=0;
            // Enemies hidden in the fog cannot be picked.
            if(!unitViews.TryGetValue(unit.Id,out var view)||!view.Root.activeSelf)return false;
            var model=view.Model;var foot=view.Root.transform.position;
            var low=model.Hover>0?view.Body.position+Vector3.down*.25f:foot+Vector3.up*.1f*model.Scale;
            var high=model.Hover>0?view.Body.position+Vector3.up*.25f:foot+Vector3.up*model.Height*model.Scale;
            Vector3 sa=MapCamera.WorldToScreenPoint(low),sb=MapCamera.WorldToScreenPoint(high);if(sa.z<=0||sb.z<=0)return false;
            a=sa;b=sb;reach=Vector2.Distance(a,MapCamera.WorldToScreenPoint(low+MapCamera.transform.right*(unit.Stats.Radius*1.2f+.12f)));
            return true;
        }
        PrototypeUnit UnitAt(Vector2 screen)
        {
            PrototypeUnit best=null;float bestDistance=float.MaxValue;
            foreach(var unit in Match.Units)
            {
                if(!ScreenAxis(unit,out var a,out var b,out float reach))continue;
                Vector2 ab=b-a;float t=ab.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(screen-a,ab)/ab.sqrMagnitude):0;
                float distance=Vector2.Distance(screen,a+ab*t);
                if(distance<reach&&distance<bestDistance){best=unit;bestDistance=distance;}
            }
            return best;
        }
        static bool Queueing()=>Keyboard.current!=null&&Keyboard.current.shiftKey.isPressed;
        // With Shift held, the selected units' orders show as lines through their waypoints, as in StarCraft: green while all
        // are moves, orange when the queue holds an attack; queued points get a small circle of their own colour.
        void UpdateWaypoints()
        {
            int lines=0,dots=0;
            if(Queueing()&&selection.Own)
                foreach(var unit in selection.Units)
                {
                    if(unit.Queued.Count==0)continue;
                    Vector3 G(float x,float z)=>new Vector3(x,Match.HeightAt(Mathf.Clamp(x,0,63.99f),Mathf.Clamp(z,0,63.99f))+.1f,z);
                    waypointPoints.Clear();waypointPoints.Add(G(unit.X,unit.Z));
                    bool any=false;float lx=0,lz=0;foreach(var p in unit.Route){lx=p.X;lz=p.Z;any=true;}
                    if(any)waypointPoints.Add(G(lx,lz));
                    bool attack=unit.Order==UnitOrder.AttackMove||unit.Order==UnitOrder.Attack;
                    foreach(var order in unit.Queued)
                    {
                        float x=order.Unit!=null?order.Unit.X:order.X,z=order.Unit!=null?order.Unit.Z:order.Z;
                        var at=G(x,z);waypointPoints.Add(at);
                        bool hit=order.Order!=UnitOrder.Move;attack|=hit;
                        while(waypointDots.Count<=dots)waypointDots.Add(scenery.Model("Waypoint",selectionMesh,markerMaterial,worldRoot.transform,false));
                        var dot=waypointDots[dots++];dot.SetActive(true);dot.transform.SetPositionAndRotation(at,Quaternion.identity);dot.transform.localScale=Vector3.one*.28f;
                        markerColor.SetColor("_Color",hit?AttackMark:MoveMark);dot.GetComponent<MeshRenderer>().SetPropertyBlock(markerColor);
                    }
                    while(waypointLines.Count<=lines)
                    {
                        var line=new GameObject("Waypoint line",typeof(LineRenderer)).GetComponent<LineRenderer>();line.transform.SetParent(worldRoot.transform,false);
                        line.sharedMaterial=markerMaterial;line.widthMultiplier=.045f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                        waypointLines.Add(line);
                    }
                    var path=waypointLines[lines++];path.enabled=true;path.positionCount=waypointPoints.Count;path.SetPositions(waypointPoints.ToArray());
                    path.startColor=path.endColor=attack?new Color(1,.45f,.2f,.7f):new Color(.35f,1,.5f,.6f);
                }
            for(int i=lines;i<waypointLines.Count;i++)waypointLines[i].enabled=false;
            for(int i=dots;i<waypointDots.Count;i++)waypointDots[i].SetActive(false);
        }
        PrototypeUnit EnemyAt(Vector2 screen){var unit=UnitAt(screen);return unit!=null&&unit.Owner!=PrototypeMatch.Player?unit:null;}
        PrototypeBuilding EnemyBuildingAt(Vector2 screen){var b=BuildingAt(screen);return b!=null&&b.Owner!=PrototypeMatch.Player&&Match.SeenBy(PrototypeMatch.Player,b)?b:null;}
        // Points on a unit's model: the top (health bar), the middle (where hits land) and the muzzle of a ground unit.
        static Vector3 Top(PrototypeUnit unit,UnitView view)=>unit.Stats.Flying?view.Body.position+Vector3.up*.35f:view.Root.transform.position+Vector3.up*view.Model.Height*view.Model.Scale;
        static Vector3 Center(PrototypeUnit unit,UnitView view)=>unit.Stats.Flying?view.Body.position:view.Root.transform.position+Vector3.up*view.Model.Height*view.Model.Scale*.5f;
        static Vector3 Muzzle(PrototypeUnit unit,UnitView view)=>view.Root.transform.position+Vector3.up*view.Model.Height*view.Model.Scale*.6f+view.Root.transform.forward*(unit.Stats.Radius+.12f);
        // Strikes and deaths from the last simulation steps, shown where the player can see them: a rifleman fires a tracer,
        // the gunship a pair from its wings, an assault unit slashes; a dead unit explodes and leaves a scorch mark.
        void ShowCombat()
        {
            foreach(var (attacker,target) in strikes)
            {
                if(!unitViews.TryGetValue(attacker.Id,out var a)||!unitViews.TryGetValue(target.Id,out var t))continue;
                if(!Match.SeenBy(PrototypeMatch.Player,attacker)&&!Match.SeenBy(PrototypeMatch.Player,target))continue;
                a.Kick=1;
                var hit=Center(target,t)+UnityEngine.Random.insideUnitSphere*target.Stats.Radius*.4f;
                if(attacker.Stats.AttackRange<1)combat.Slash(hit,hit-Center(attacker,a),new Color(.75f,.9f,1));
                else if(attacker.Stats.Flying)
                    for(int side=-1;side<=1;side+=2)combat.Tracer(a.Body.TransformPoint(new Vector3(side*.34f,-.1f,.32f)),hit+a.Body.right*side*.12f,new Color(1,.6f,.28f),.06f);
                else combat.Tracer(Muzzle(attacker,a),hit,new Color(1,.86f,.5f));
            }
            strikes.Clear();
            foreach(var (attacker,target) in buildingStrikes)
            {
                if(!unitViews.TryGetValue(attacker.Id,out var a)||!buildingViews.TryGetValue(target,out var view))continue;
                if(!Match.SeenBy(PrototypeMatch.Player,attacker)&&!Match.SeenBy(PrototypeMatch.Player,target))continue;
                a.Kick=1;
                // Hits land on the side facing the attacker, somewhere up the wall.
                var center=view.Model.GetComponent<MeshRenderer>().bounds.center;var toward=new Vector3(attacker.X-center.x,0,attacker.Z-center.z).normalized;
                var hit=center+toward*target.Width*.42f+new Vector3(UnityEngine.Random.Range(-.4f,.4f),UnityEngine.Random.Range(-.3f,.4f),UnityEngine.Random.Range(-.4f,.4f));
                if(attacker.Stats.AttackRange<1)combat.Slash(hit,hit-Center(attacker,a),new Color(.75f,.9f,1));
                else if(attacker.Stats.Flying)
                    for(int side=-1;side<=1;side+=2)combat.Tracer(a.Body.TransformPoint(new Vector3(side*.34f,-.1f,.32f)),hit+a.Body.right*side*.12f,new Color(1,.6f,.28f),.06f);
                else combat.Tracer(Muzzle(attacker,a),hit,new Color(1,.86f,.5f));
            }
            buildingStrikes.Clear();
            // A destroyed building goes up in a chain of blasts across its footprint.
            foreach(var b in ruins)
            {
                if(!Match.SeenBy(PrototypeMatch.Player,b)&&b.Owner!=PrototypeMatch.Player)continue;
                float ground=Match.Map.Height(b.X,b.Z),h=Match.Balance.Buildings[b.Kind].Height;
                for(int i=0;i<3;i++)
                {
                    var at=new Vector3(b.X+b.Width*(.3f+.2f*i),ground+h*.5f,b.Z+b.Width*(i==1?.7f:.35f));
                    combat.Explosion(at,ground,b.Width*.9f);
                }
            }
            if(ruins.Count>0){ruins.Clear();SyncBuildings();hud?.Refresh();}
            foreach(var unit in deaths)
                if(unitViews.TryGetValue(unit.Id,out var view)&&Match.SeenBy(PrototypeMatch.Player,unit))
                    combat.Explosion(Center(unit,view),Match.HeightAt(unit.X,unit.Z),unit.Stats.Flying?1.3f:Mathf.Max(1,unit.Stats.Radius*2.6f));
            deaths.Clear();
        }
        // A unit is inside the box when any of its feet, middle or top is.
        void UnitsIn(Rect box,List<PrototypeUnit> result)
        {
            foreach(var unit in Match.Units)
                if(ScreenAxis(unit,out var a,out var b,out _)&&(box.Contains(a)||box.Contains(b)||box.Contains((a+b)*.5f)))result.Add(unit);
        }
        List<PrototypeUnit> UnitsOnScreen()
        {
            var result=new List<PrototypeUnit>();UnitsIn(new Rect(0,0,Screen.width,Screen.height),result);return result;
        }
        // 1–0 recall groups (twice in a row: camera to the group), Ctrl+digit assigns, Shift+digit adds;
        // F1 picks the next idle worker, F2 the whole army, as in StarCraft.
        void SelectionKeys(Keyboard keys)
        {
            bool ctrl=keys.ctrlKey.isPressed,shift=keys.shiftKey.isPressed;
            for(int i=0;i<Digits.Length;i++)
            {
                if(!keys[Digits[i]].wasPressedThisFrame)continue;
                string label=i==9?"0":(i+1).ToString();
                if(ctrl){selection.Assign(i);Match.Message=selection.Count>0?$"Группа {label}: {selection.Count}.":$"Группа {label} очищена.";hud?.Refresh();}
                else if(shift){selection.AddToGroup(i);Match.Message=$"Группа {label}: {selection.GroupSize(i,Match.Units)}.";hud?.Refresh();}
                else RecallGroup(i);
            }
            if(keys.f1Key.wasPressedThisFrame)SelectIdleWorker();
            if(keys.f2Key.wasPressedThisFrame)SelectArmy();
        }
        public void RecallGroup(int index)
        {
            if(!selection.Recall(index,Match.Units))return;
            selectedBuilding=null;selectedDeposit=null;
            if(lastGroup==index&&Time.unscaledTime-lastGroupTime<.4f)FocusSelection();
            lastGroup=index;lastGroupTime=Time.unscaledTime;
            hud?.Refresh();
        }
        public void SelectArmy()=>SelectUnits(u=>u.Kind!=UnitKind.Worker);
        public void SelectKind(UnitKind kind)=>SelectUnits(u=>u.Kind==kind);
        void SelectUnits(Predicate<PrototypeUnit> filter)
        {
            var units=Match.Units.FindAll(u=>u.Owner==PrototypeMatch.Player&&filter(u));if(units.Count==0)return;
            selection.Set(units);selectedBuilding=null;selectedDeposit=null;hud?.Refresh();
        }
        // The next worker with no order after the one selected now; the camera follows it.
        void SelectIdleWorker()
        {
            var idle=Match.Units.FindAll(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker&&!u.Moving&&u.HarvestTarget==null);
            if(idle.Count==0){Match.Message="Нет свободных рабочих.";hud?.Refresh();return;}
            int current=selection.Count==1?idle.IndexOf(selection.Units[0]):-1;
            selection.Set(new[]{idle[(current+1)%idle.Count]});selectedBuilding=null;selectedDeposit=null;
            FocusSelection();hud?.Refresh();
        }
        // Order marker on the ground: arrows converge on the point and fade; orange for an attack-move, red when no unit
        // could take the order. An attacked unit flashes a red circle that closes in on it.
        sealed class Marker{public GameObject View;public float Start,Size;public Color Color;public PrototypeUnit Unit;}
        static readonly Color MoveMark=new Color(.35f,1.6f,.5f,1),AttackMark=new Color(1.9f,.55f,.2f,1),FailMark=new Color(1.6f,.3f,.25f,1);
        void Mark(Vector2 point,bool ok,bool attack=false)
        {
            var view=scenery.Model("Order marker",markerMesh,markerMaterial,worldRoot.transform,false);
            float x=Mathf.Clamp(point.x,0,63.99f),z=Mathf.Clamp(point.y,0,63.99f);
            view.transform.SetPositionAndRotation(new Vector3(point.x,Match.HeightAt(x,z)+.04f,point.y),Quaternion.FromToRotation(Vector3.up,GroundNormal(x,z)));
            markers.Add(new Marker{View=view,Start=Time.time,Color=!ok?FailMark:attack?AttackMark:MoveMark});
        }
        void MarkTarget(PrototypeUnit unit)
        {
            var view=scenery.Model("Target marker",selectionMesh,markerMaterial,worldRoot.transform,false);
            markers.Add(new Marker{View=view,Start=Time.time,Color=FailMark,Unit=unit});
        }
        void AnimateMarkers()
        {
            for(int i=markers.Count-1;i>=0;i--)
            {
                var marker=markers[i];float t=(Time.time-marker.Start)/.6f;
                if(t>=1||(marker.Unit!=null&&!marker.Unit.Alive)){Destroy(marker.View);markers.RemoveAt(i);continue;}
                if(marker.Unit!=null)
                {
                    var unit=marker.Unit;float y=unit.Stats.Flying&&unitViews.TryGetValue(unit.Id,out var view)?view.Body.position.y-.3f:Match.HeightAt(unit.X,unit.Z)+.05f;
                    marker.View.transform.position=new Vector3(unit.X,y,unit.Z);
                    marker.View.transform.localScale=Vector3.one*(unit.Stats.Radius+.16f)*Mathf.Lerp(1.9f,1.05f,Mathf.Sqrt(t));
                }
                else if(marker.Size>0)marker.View.transform.localScale=Vector3.one*marker.Size*Mathf.Lerp(1.35f,1,Mathf.Sqrt(t));
                else marker.View.transform.localScale=Vector3.one*Mathf.Lerp(1.3f,.55f,Mathf.Sqrt(t));
                var color=marker.Color;color.a=1-Mathf.SmoothStep(.35f,1,t);
                markerColor.SetColor("_Color",color);marker.View.GetComponent<MeshRenderer>().SetPropertyBlock(markerColor);
            }
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
            if(selectedBuilding!=null)
            {
                var b=selectedBuilding;position=new Vector3(b.X+b.Width*.5f,Match.Map.Height(b.X,b.Z),b.Z+b.Width*.5f);radius=b.Width*.76f;
                focusRing.GetComponent<MeshRenderer>().sharedMaterial=b.Owner==PrototypeMatch.Player?selectedMaterial:enemyRingMaterial;
            }
            else if(selectedDeposit!=null&&selectedDeposit.Remaining>0)
            {
                position=new Vector3(selectedDeposit.X+.5f,Match.Map.Height(selectedDeposit.X,selectedDeposit.Z),selectedDeposit.Z+.5f);radius=.68f;
                focusRing.GetComponent<MeshRenderer>().sharedMaterial=neutralMaterial;
            }
            focusRing.SetActive(radius>0);
            UpdateRally();
            if(radius>0){focusRing.transform.position=position;focusRing.transform.localScale=Vector3.one*radius*(1+.025f*Mathf.Sin(Time.time*5));}
        }
        // The selected building's rally point: a small flag and a line from the building along the ground.
        void UpdateRally()
        {
            var b=selectedBuilding;bool show=b!=null&&b.HasRally&&b.Owner==PrototypeMatch.Player;
            rallyFlag.SetActive(show);rallyLine.enabled=show;
            if(!show)return;
            float H(float x,float z)=>Match.HeightAt(Mathf.Clamp(x,0,63.99f),Mathf.Clamp(z,0,63.99f))+.12f;
            var from=new Vector2(b.X+b.Width*.5f,b.Z+b.Width*.5f);var to=new Vector2(b.RallyX,b.RallyZ);
            int count=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from,to)/.5f)+1,2,160);
            rallyLine.positionCount=count;
            for(int i=0;i<count;i++){var p=Vector2.Lerp(from,to,i/(count-1f));rallyLine.SetPosition(i,new Vector3(p.x,H(p.x,p.y),p.y));}
            rallyFlag.transform.position=new Vector3(to.x,H(to.x,to.y)-.12f,to.y);
        }
        float Option(string id,float fallback){var settings=services?.App?.Settings;return settings.HasValue?MenuCatalog.Value(settings.Value,MenuCatalog.Find(id)):fallback;}
        // "Скорость камеры" 0–100 maps to 0.4–1.6 of the base speed; 50 keeps the original pace.
        float CameraSpeed()=>.4f+Option("controls.cameraSpeed",50)/100f*1.2f;
        string Hint()=>Mode==MatchMode.Build?"Укажите место на карте · ПКМ / Esc — отмена":Mode==MatchMode.Move?"Движение: ЛКМ — точка на карте или миникарте · ПКМ / Esc — отмена"
            :Mode==MatchMode.Harvest?"Добыча: ЛКМ — разведанная залежь · ПКМ / Esc — отмена"
            :Mode==MatchMode.Attack?"Атака: ЛКМ — враг или точка (атака с движением) · ПКМ / Esc — отмена"
            :Mode==MatchMode.Rally?"Точка сбора: ЛКМ — место или залежь на карте или миникарте · ПКМ / Esc — отмена"
            :Mode==MatchMode.Ping?"Метка: ЛКМ — место на карте или миникарте · Alt+ЛКМ — всегда":"";
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
                float h=Match.Balance.Buildings[b.Kind].Height,ground=Match.Map.Height(b.X,b.Z);
                bool shown=BuildingShown(b);
                if(view.Model.activeSelf!=shown)view.Model.SetActive(shown);
                // The full-sized model rises through the opaque terrain; it never scales up.
                view.Model.transform.position=new Vector3(b.X+b.Width*.5f,ground-h*(1-b.Ready),b.Z+b.Width*.5f);
                view.Ring.SetActive(b.Ready<1&&shown);
                if(b.Ready<1){view.Ring.transform.position=new Vector3(b.X+b.Width*.5f,ground,b.Z+b.Width*.5f);view.Ring.transform.localScale=Vector3.one*b.Width*(.74f+.04f*Mathf.Sin(Time.time*6));}
            }
        }
        // Views appear for new buildings and disappear with destroyed ones.
        void SyncBuildings()
        {
            foreach(var b in Match.Buildings)
            {
                if(buildingViews.ContainsKey(b))continue;
                var material=b.Owner==PrototypeMatch.Player?buildingMaterial:enemyBuildingMaterial;
                buildingViews[b]=new BuildingView{Model=scenery.Model(Match.Balance.Buildings[b.Kind].Name,BuildingMesh(b.Kind),material,worldRoot.transform),
                    Ring=scenery.Model("Construction",ringMesh,constructionMaterial,worldRoot.transform,false)};
            }
            if(buildingViews.Count==Match.Buildings.Count)return;
            foreach(var b in new List<PrototypeBuilding>(buildingViews.Keys))
                if(!Match.Buildings.Contains(b)){var view=buildingViews[b];Destroy(view.Model);Destroy(view.Ring);buildingViews.Remove(b);if(selectedBuilding==b)selectedBuilding=null;}
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
            SyncBuildings();
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
            foreach(var b in Match.Buildings)
            {
                if(!BuildingShown(b))continue;
                var color=(b.Owner==PrototypeMatch.Player?own:EnemyColor)*(b.Ready>=1?1:.6f);color.a=1;
                for(int z=b.Z;z<b.Z+b.Width;z++)for(int x=b.X;x<b.X+b.Width;x++)Cell(x,z,color);
            }
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
            if(mode==MatchMode.Attack&&!HasFighter()){Match.Message="Выбранные юниты не атакуют.";mode=MatchMode.None;}
            if(mode==MatchMode.Rally&&(selectedBuilding==null||Match.Balance.Buildings[selectedBuilding.Kind].Trains.Length==0))mode=MatchMode.None;
            if((mode==MatchMode.Move&&!selection.Own)||(mode==MatchMode.Harvest&&!HasHarvester()))SelectWorker();
            Mode=mode;buildKind=mode==MatchMode.Build?kind:-1;ModeHint=Hint();hud?.Refresh();
        }
        bool HasHarvester(){foreach(var u in selection.Orderable)if(u.Stats.HarvestAmount>0)return true;return false;}
        bool HasFighter(){foreach(var u in selection.Orderable)if(u.Stats.Damage>0)return true;return false;}
        public void SelectWorker()
        {
            var worker=Match.Units.Find(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker);
            if(worker!=null)selection.Set(new[]{worker});else selection.Clear();
            selectedBuilding=null;selectedDeposit=null;hud?.Refresh();
        }
        public void Focus(Vector2 point)=>focus=new Vector3(Mathf.Clamp(point.x,0,64),0,Mathf.Clamp(point.y,0,64));
        public Vector2 CameraFocus=>new Vector2(focus.x,focus.z);
        public void FocusWorker(){var worker=Match.Units.Find(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker);if(worker!=null)focus=new Vector3(worker.X,0,worker.Z);}
        // Camera to the middle of the selected units; without a selection, to the worker.
        public void FocusSelection()
        {
            if(selection.Count==0){FocusWorker();return;}
            float x=0,z=0;foreach(var u in selection.Units){x+=u.X;z+=u.Z;}
            focus=new Vector3(x/selection.Count,0,z/selection.Count);
        }
        // A group order (formation or gathering on the point, see PrototypeMatch.MoveGroup); workers harvest a deposit there,
        // the rest walk up to it. The marker sits on the point, or on the deposit when the order targets one.
        public bool MoveSelection(Vector2 point)=>MoveSelection(point,false);
        bool MoveSelection(Vector2 point,bool queue)
        {
            var units=selection.Orderable;if(units.Count==0)return false;
            bool any=queue?Match.QueueMoveGroup(units,point.x,point.y):Match.MoveGroup(units,point.x,point.y);
            int cx=(int)point.x,cz=(int)point.y;
            var deposit=Match.Map.Inside(cx,cz)&&Match.Explored[cx,cz]?Match.Map.ResourceAt(cx,cz):null;
            Mark(deposit!=null?new Vector2(deposit.X+.5f,deposit.Z+.5f):point,any);
            return any;
        }
        // Attack-move (A + click): the group goes to the point in formation and fights everything on the way.
        public bool AttackMoveSelection(Vector2 point)=>AttackMoveSelection(point,false);
        bool AttackMoveSelection(Vector2 point,bool queue)
        {
            var units=selection.Orderable;if(units.Count==0)return false;
            bool any=queue?Match.QueueAttackMoveGroup(units,point.x,point.y):Match.AttackMoveGroup(units,point.x,point.y);
            Mark(point,any,true);
            return any;
        }
        // Units that cannot hit the target (an assault unit against air) walk up to it instead.
        public bool AttackSelection(PrototypeUnit target,bool queue=false)
        {
            var units=selection.Orderable;if(units.Count==0)return false;
            bool any=queue?Match.EnqueueGroup(units,target):Match.AttackGroup(units,target);
            if(any)MarkTarget(target);
            hud?.Refresh();
            return any;
        }
        // Production in the selected building; the queue and rally point live in the simulation.
        public bool Train(UnitKind kind){bool ok=selectedBuilding!=null&&Match.Train(selectedBuilding,kind);hud?.Refresh();return ok;}
        public void CancelTraining(int index){if(selectedBuilding!=null)Match.CancelTraining(selectedBuilding,index);hud?.Refresh();}
        // A rally point on a deposit is drawn at its centre: workers will harvest it.
        public void SetRallyPoint(Vector2 point)
        {
            if(selectedBuilding==null||selectedBuilding.Owner!=PrototypeMatch.Player||Match.Balance.Buildings[selectedBuilding.Kind].Trains.Length==0)return;
            int cx=(int)point.x,cz=(int)point.y;
            var deposit=Match.Map.Inside(cx,cz)&&Match.Explored[cx,cz]?Match.Map.ResourceAt(cx,cz):null;
            if(deposit!=null)point=new Vector2(deposit.X+.5f,deposit.Z+.5f);
            Match.SetRally(selectedBuilding,point.x,point.y);Mark(point,true);hud?.Refresh();
        }
        public bool AttackSelection(PrototypeBuilding target,bool queue=false)
        {
            var units=selection.Orderable;if(units.Count==0)return false;
            bool any=queue?Match.EnqueueGroup(units,target):Match.AttackGroup(units,target);
            if(any)
            {
                var view=scenery.Model("Target marker",selectionMesh,markerMaterial,worldRoot.transform,false);
                view.transform.position=new Vector3(target.X+target.Width*.5f,Match.Map.Height(target.X,target.Z)+.05f,target.Z+target.Width*.5f);
                markers.Add(new Marker{View=view,Start=Time.time,Color=FailMark,Size=target.Width*.78f});
            }
            hud?.Refresh();
            return any;
        }
        public void HoldSelection(){foreach(var unit in selection.Orderable)Match.Hold(unit);SetMode(MatchMode.None);}
        public void Stop(){foreach(var unit in selection.Orderable)Match.Stop(unit);SetMode(MatchMode.None);}
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
            if(Mode==MatchMode.Attack){bool queue=Queueing();AttackMoveSelection(point,queue);if(!queue)SetMode(MatchMode.None);return;}
            // With a building selected, a plain order from the minimap sets its rally point.
            if(Mode==MatchMode.Rally||(Mode==MatchMode.None&&!selection.Own&&selectedBuilding!=null&&selectedBuilding.Owner==PrototypeMatch.Player)){SetRallyPoint(point);SetMode(MatchMode.None);return;}
            if(Mode==MatchMode.Harvest)
            {
                var deposit=Match.Map.Inside((int)point.x,(int)point.y)&&Match.Explored[(int)point.x,(int)point.y]?Match.Map.ResourceAt((int)point.x,(int)point.y):null;
                if(deposit==null){Match.Message="Укажите разведанную залежь.";return;}
                if(!HasHarvester())SelectWorker();
                // Only workers go: the rest of the selection stays where it is.
                selection.Set(new List<PrototypeUnit>(selection.Units).FindAll(u=>u.Stats.HarvestAmount>0));
            }
            if(!selection.Own)SelectWorker();
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
            buildingViews.Clear();depositViews.Clear();pinViews.Clear();pins.Clear();Match=null;
            combat?.Dispose();combat=null;strikes.Clear();deaths.Clear();buildingStrikes.Clear();ruins.Clear();waypointLines.Clear();waypointDots.Clear();
            unitViews.Clear();unitModels.Clear();selectionRings.Clear();hoverRings.Clear();hovered.Clear();markers.Clear();
            selection=new UnitSelection();lastClickUnit=null;lastGroup=-1;pressing=dragging=false;
            Array.Clear(buildingMeshes,0,buildingMeshes.Length);
            ghostModel=null;ghostKind=-1;Mode=MatchMode.None;
        }
        void OnDestroy()=>Shutdown();
    }
}
