using System;
using System.Collections.Generic;
using Order.Content;
using Order.Contracts;
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
        public IReadOnlyList<(Vector2 point, float time)> Alerts => alerts;
        readonly List<(Vector2 point, float time)> alerts = new List<(Vector2, float)>();
        public const float AlertSeconds = 4;
        public Texture MinimapTexture => miniTexture;
        public string ModeHint { get; private set; } = "";
        public UnitSelection Selection => selection;
        VisualElement ui;
        MatchHud hud;
        MatchServices services;
        TempleScenery scenery;
        readonly List<Vector2> pins = new List<Vector2>();
        readonly List<GameObject> pinViews = new List<GameObject>();
        readonly List<GameObject> siteGhosts = new List<GameObject>();
        readonly List<LineRenderer> coverageRings = new List<LineRenderer>();
        Texture2D miniTexture;
        Color[] miniBase;
        GameObject worldRoot, footprint, focusRing, ghostModel, rallyFlag;
        LineRenderer rallyLine;
        int ghostKind=-1;
        // A rising machine building adds its nanite liquid form, under Liquid.
        sealed class BuildingView{public GameObject Model,Ring,Liquid,Puddle;public bool Revealing,Spanned;public float DriedAt,Flow,Shift,Front,PuddleRadius;public Vector4 Pool;public Vector3 Span;}
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
        readonly Dictionary<(FactionId, UnitKind), UnitModel> unitModels = new Dictionary<(FactionId, UnitKind), UnitModel>();
        readonly Mesh[,] buildingMeshes = new Mesh[2,5];
        Mesh ringMesh, beaconMesh, shadowMesh, selectionMesh, markerMesh, cargoMesh;
        Material buildingMaterial, enemyBuildingMaterial, ghostMaterial, footprintMaterial, selectedMaterial, neutralMaterial, constructionMaterial, beaconMaterial, unitMaterial, enemyMaterial, beamMaterial, shadowMaterial,
            unitRingMaterial, hoverRingMaterial, enemyRingMaterial, enemyHoverMaterial, markerMaterial, oreMaterial, plasmaCargoMaterial, naniteMaterial, liquidMaterial, cocoonMaterial, coverageMaterial, darkCoverageMaterial;
        Mesh cocoonMesh;
        MaterialPropertyBlock revealBlock;
        MaterialPropertyBlock cocoonColor;
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
            // Factions: those chosen for the match in the menu (by default humans against the machines), or a restart's.
            var (player,enemy)=restartAs??(services?.App?.PlayerFaction??FactionId.Expedition,services?.App?.EnemyFaction??FactionId.Resonance);restartAs=null;
            Match=new PrototypeMatch(catalog.Snapshot(),player,enemy);ui=root;onExit=exit;
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
            // Polished ore: glossy with a strong rim, glowing only along its seams.
            oreMaterial=scenery.Prop("Metal ore",m=>{m.SetFloat("_Rim",.8f);m.SetFloat("_Gloss",1);m.SetFloat("_Emission",1.6f);});
            plasmaCargoMaterial=scenery.Prop("Plasma cargo",m=>{m.SetColor("_BaseColor",new Color(1,.4f,.92f));m.SetFloat("_Emission",3);m.SetFloat("_Fog",0);});
            var riftMaterial=scenery.Prop("Plasma rifts");
            footprintMaterial=scenery.Glow("Placement footprint",Color.green,false,m=>m.SetFloat("_Grid",1));
            selectedMaterial=scenery.Glow("Selection",new Color(.3f,1,.42f)*1.5f);
            neutralMaterial=scenery.Glow("Selection neutral",new Color(1,.92f,.6f)*1.2f);
            constructionMaterial=scenery.Glow("Construction",new Color(1,.62f,.22f)*1.4f);
            beaconMaterial=scenery.Glow("Beacon",new Color(1,.42f,.24f)*2,true,m=>m.SetFloat("_FadeHeight",7));
            ringMesh=scenery.Own(MatchModels.Ring(.84f));beaconMesh=scenery.Own(MatchModels.Beacon());
            var veins=new Mesh[4];for(int i=0;i<veins.Length;i++)veins[i]=scenery.Own(MatchModels.MetalVein(17+i*31));
            cargoMesh=veins[0];
            var rift=scenery.Own(MatchModels.PlasmaRift(5));
            for(int i=0;i<Match.Map.Resources.Count;i++)
            {
                var r=Match.Map.Resources[i];
                var go=scenery.Model(r.Plasma?"Plasma rift":"Metal vein",r.Plasma?rift:veins[i%veins.Length],r.Plasma?riftMaterial:oreMaterial,worldRoot.transform);
                go.transform.position=new Vector3(r.X+.5f,Match.Map.Height(r.X,r.Z),r.Z+.5f);go.transform.rotation=Quaternion.Euler(0,i*67%360,0);
                if(r.Plasma)scenery.PlasmaWisps(go.transform,new Vector3(0,.2f,0));
                depositViews.Add(go);
            }
            beamMaterial=scenery.Glow("Mining beam",new Color(.45f,.9f,1)*3);
            naniteMaterial=scenery.Glow("Nanite threads",new Color(.62f,.86f,1)*2.4f);
            liquidMaterial=scenery.NaniteLiquid("Nanite liquid");
            coverageMaterial=scenery.Glow("Network coverage",new Color(.5f,.8f,1)*1.1f);darkCoverageMaterial=scenery.Glow("Network coverage lost",new Color(.45f,.45f,.5f)*.5f);
            cocoonMaterial=scenery.Glow("Nanite cocoon",new Color(.14f,.27f,.38f,1));cocoonMesh=scenery.Own(MachineModels.Cocoon());cocoonColor=new MaterialPropertyBlock();
            revealBlock=new MaterialPropertyBlock();
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
            Match.UnderAttack+=OnUnderAttack;Match.Cancelled+=OnCancelled;
            Match.AddTestSquad();Match.AddEnemySquad();Match.AddEnemyBase();Match.StartBot();Match.PrepareNavigation();SelectWorker();
            var square=new MeshBuilder();square.Quad(new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f));
            footprint=scenery.Model("Placement footprint",scenery.Own(square.ToMesh("Footprint")),footprintMaterial,worldRoot.transform,false);footprint.SetActive(false);
            UpdateCamera();RefreshWorld();
        }
        Mesh BuildingMesh(FactionId faction,int kind)
        {
            int f=(int)faction;
            if(buildingMeshes[f,kind]==null){var stats=Match.Balance.Of(faction).Buildings[kind];buildingMeshes[f,kind]=scenery.Own(faction==FactionId.Resonance?MachineModels.Building(kind,stats.Width,stats.Height):MatchModels.Building(kind,stats.Width,stats.Height));}
            return buildingMeshes[f,kind];
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
                if(!overUI)
                {
                    // Wheel notches: Input System reports ±1 per notch by default, ±120 with platform-specific range.
                    float notches=mouse.scroll.ReadValue().y;
                    if(InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)notches/=120;
                    targetZoom=Mathf.Clamp(targetZoom*Mathf.Pow(.85f,Mathf.Clamp(notches,-3,3)),MinZoom,MaxZoom);
                }
                bool onGround=GroundPoint(p,out var point);hasHover=!overUI&&onGround;
                if(hasHover)
                {
                    hoverX=Mathf.FloorToInt(point.x);hoverZ=Mathf.FloorToInt(point.y);
                    // An extractor placed near a rift snaps onto it.
                    if(Mode==MatchMode.Build&&Match.Roster(PrototypeMatch.Player).Buildings[buildKind].Extracts)
                    {
                        float best=1.6f*1.6f;
                        foreach(var r in Match.Map.Resources)
                        {
                            float dx=r.X+.5f-point.x,dz=r.Z+.5f-point.y;
                            if(r.Plasma&&r.Remaining>0&&dx*dx+dz*dz<best){best=dx*dx+dz*dz;hoverX=r.X;hoverZ=r.Z;}
                        }
                    }
                    // Right click on a visible enemy attacks it, anywhere else moves; in a targeting mode it cancels the mode.
                    if(mouse.rightButton.wasPressedThisFrame)
                    {
                        if(Mode!=MatchMode.None)SetMode(MatchMode.None);
                        else if(!selection.Own&&selectedBuilding!=null&&selectedBuilding.Owner==PrototypeMatch.Player)SetRallyPoint(point);
                        else
                        {
                            bool queue=Queueing();var enemy=EnemyAt(p);var structure=enemy==null?EnemyBuildingAt(p):null;var site=enemy==null&&structure==null&&!queue?SiteAt(p):null;
                            if(enemy!=null&&selection.Own)AttackSelection(enemy,queue);else if(structure!=null&&selection.Own)AttackSelection(structure,queue);
                            else if(site!=null&&ResumeSelection(site)){}
                            // Workers on own damaged buildings and machinery repair them, as SCVs do.
                            else if(Repairers(null).Count>0&&RepairTargetAt(p,true,out var mend,out var machine)&&(queue?QueueRepairSelection(mend,machine):RepairSelection(mend,machine))){}
                            else MoveSelection(ExtractorPoint(p,point),queue);
                        }
                    }
                    if(mouse.leftButton.wasPressedThisFrame)
                    {
                        if(keys!=null&&keys.altKey.isPressed)AddPin(point);
                        // With Shift the building joins the worker's queue and the placement stays on for the next one, as in StarCraft.
                        else if(Mode==MatchMode.Build){bool queue=Queueing();if(Build(hoverX,hoverZ,buildKind,queue)&&!queue)SetMode(MatchMode.None);}
                        else if(Mode==MatchMode.Attack)
                        {
                            // With Shift the order is queued and the attack mode stays on for the next target, as in StarCraft.
                            bool queue=Queueing();var enemy=EnemyAt(p);var structure=enemy==null?EnemyBuildingAt(p):null;
                            if(enemy!=null)AttackSelection(enemy,queue);else if(structure!=null)AttackSelection(structure,queue);else AttackMoveSelection(point,queue);
                            if(!queue)SetMode(MatchMode.None);
                        }
                        else if(Mode==MatchMode.Repair)
                        {
                            // A wrong target keeps the mode on, as in StarCraft.
                            // With Shift the repair is queued and the mode stays on for the next target.
                            bool queue=Queueing();
                            if(!RepairTargetAt(p,false,out var mend,out var machine)){Match.Message="Укажите своё здание или технику.";hud?.Refresh();}
                            else if(queue)QueueRepairSelection(mend,machine);
                            else if(RepairSelection(mend,machine))SetMode(MatchMode.None);
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
                bool valid=Match.CanBuild(hoverX,hoverZ,buildKind,out var reason);int w=Match.Roster(PrototypeMatch.Player).Buildings[buildKind].Width;
                footprint.transform.position=new Vector3(hoverX+w*.5f,Match.Map.Height(hoverX,hoverZ)+.03f,hoverZ+w*.5f);
                footprint.transform.localScale=new Vector3(w,1,w);footprintMaterial.SetColor("_Color",valid?new Color(.2f,1,.4f,.5f):new Color(1,.15f,.15f,.55f));
                ModeHint=Match.Roster(PrototypeMatch.Player).Buildings[buildKind].Name+": "+reason+" · ПКМ / Esc — отмена";
                if(ghostKind!=buildKind)
                {
                    if(ghostModel!=null)Destroy(ghostModel);
                    ghostModel=scenery.Model("Placement ghost",BuildingMesh(Match.FactionOf(PrototypeMatch.Player),buildKind),ghostMaterial,worldRoot.transform,false);ghostKind=buildKind;
                }
                ghostModel.SetActive(true);ghostModel.transform.position=footprint.transform.position;
            }
            else{if(ghostModel!=null)ghostModel.SetActive(false);ModeHint=Hint();}
            ShowPlannedSites();
            ShowCoverage();
            for(int i=0;i<pinViews.Count;i++)pinViews[i].transform.localScale=new Vector3(1,1+.08f*Mathf.Sin(Time.time*4+i),1);
            AnimateBuildings();
            AnimateMelts();
            AnimateGathers();
            scenery.Tick(Time.deltaTime);
            refresh+=Time.deltaTime;if(refresh>.15f){refresh=0;RefreshWorld();}
        }
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
        }
        UnitView CreateUnitView(PrototypeUnit unit)
        {
            var key=(Match.FactionOf(unit.Owner),unit.Kind);
            if(!unitModels.TryGetValue(key,out var model))
            {
                model=key.Item1==FactionId.Resonance?MachineModels.Unit(unit.Kind):MatchModels.Unit(unit.Kind);unitModels[key]=model;
                scenery.Own(model.Body);if(model.Leg!=null)scenery.Own(model.Leg);if(model.Rotor!=null)scenery.Own(model.Rotor);if(model.Gun!=null)scenery.Own(model.Gun);
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
            if(model.Gun!=null){view.Gun=scenery.Model("Gun",model.Gun,material,view.Body).transform;view.Gun.localPosition=model.GunPivot;}
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
                // A machine worker gathering back out of a cancelled site shows only as the liquid until it has formed.
                bool seen=Match.SeenBy(PrototypeMatch.Player,unit)&&!Gathering(unit);
                if(view.Root.activeSelf!=seen){view.Root.SetActive(seen);if(view.Shadow!=null)view.Shadow.gameObject.SetActive(seen);}
                float ground=Match.HeightAt(unit.X,unit.Z);
                if(unit.Stats.Flying)
                {
                    // Climb early and fast, sink late and slowly: the flight level follows the highest ground around
                    // the unit and ahead along its course, so it clears a cliff before reaching the edge.
                    float level=FlightGround(unit);
                    view.Height=Mathf.Lerp(view.Height,level,1-Mathf.Exp(-dt*(level>view.Height?8:2.2f)));
                }
                // A leaper flies its jump in an arc.
                else view.Height=ground+(unit.Leaping?Mathf.Sin(unit.LeapShare*Mathf.PI)*.9f:0);
                var position=new Vector3(unit.X,view.Height,unit.Z);
                var root=view.Root.transform;
                var direction=position-root.position;direction.y=0;
                float step=direction.magnitude;
                var target=unit.HarvestTarget;bool nanite=unit.Kind==UnitKind.Worker&&Match.Roster(unit.Owner).NaniteMining;
                // A hauling worker mines with a beam; a nanite worker wraps its deposit (facing it from the start of the wrap).
                bool mining=target!=null&&target.Remaining>0&&(nanite?unit.Wrap>0:unit.Mining);
                var deposit=mining?new Vector3(target.X+.5f,Match.Map.Height(target.X,target.Z),target.Z+.5f):Vector3.zero;
                if(mining){direction=deposit-position;direction.y=0;}
                // A building worker faces its building and welds it with the same beam: into the footprint, at the top of
                // the part that has risen so far, the end wandering along it like a welding torch.
                var site=unit.Constructing;bool welding=!mining&&site!=null&&site.Powered&&PrototypeMatch.Gap(unit,site)<=PrototypeMatch.BuildReach;
                var weld=Vector3.zero;
                if(welding)
                {
                    float top=site.Stats.Height*site.Ready,cx=site.X+site.Width*.5f,cz=site.Z+site.Width*.5f;
                    var edge=new Vector2(Mathf.Clamp(unit.X,site.X,site.X+site.Width),Mathf.Clamp(unit.Z,site.Z,site.Z+site.Width));
                    var into=Vector2.Lerp(edge,new Vector2(cx,cz),.3f+.12f*Mathf.Sin(time*2.3f+unit.Id));
                    var along=new Vector2(-(cz-edge.y),cx-edge.x).normalized*.35f*Mathf.Sin(time*1.7f+unit.Id*.7f);
                    weld=new Vector3(into.x+along.x,Match.Map.Height(site.X,site.Z)+Mathf.Max(.18f,top*.9f),into.y+along.y);
                    direction=new Vector3(cx-unit.X,0,cz-unit.Z);
                }
                // A repairing worker welds its target the same way: a building along its side, machinery at its middle.
                else if(!mining&&unit.Welding)
                {
                    welding=true;var mend=unit.Repairing;
                    if(mend!=null)
                    {
                        float cx=mend.X+mend.Width*.5f,cz=mend.Z+mend.Width*.5f;
                        var edge=new Vector2(Mathf.Clamp(unit.X,mend.X,mend.X+mend.Width),Mathf.Clamp(unit.Z,mend.Z,mend.Z+mend.Width));
                        var along=new Vector2(-(cz-edge.y),cx-edge.x).normalized*.4f*Mathf.Sin(time*1.7f+unit.Id*.7f);
                        weld=new Vector3(edge.x+along.x,Match.Map.Height(mend.X,mend.Z)+mend.Stats.Height*(.35f+.2f*Mathf.Sin(time*2.3f+unit.Id)),edge.y+along.y);
                        direction=new Vector3(cx-unit.X,0,cz-unit.Z);
                    }
                    else if(unit.RepairingUnit!=null)
                    {
                        var other=unit.RepairingUnit;
                        weld=unitViews.TryGetValue(other.Id,out var otherView)?Center(other,otherView):new Vector3(other.X,Match.HeightAt(other.X,other.Z)+.4f,other.Z);
                        direction=new Vector3(other.X-unit.X,0,other.Z-unit.Z);
                    }
                }
                // A moving unit faces its next waypoint, so shoves do not twist it; a shoved idle unit keeps its facing.
                if(unit.Leaping){/* In flight a leaper faces the way it flies: this frame's movement. */}
                else if(unit.Moving&&!mining)foreach(var next in unit.Route){direction=new Vector3(next.X-unit.X,0,next.Z-unit.Z);break;}
                else if(unit.Target!=null)direction=new Vector3(unit.Target.X-unit.X,0,unit.Target.Z-unit.Z);
                else if(unit.TargetBuilding!=null){var b=unit.TargetBuilding;direction=new Vector3(Mathf.Clamp(unit.X,b.X,b.X+b.Width)-unit.X,0,Mathf.Clamp(unit.Z,b.Z,b.Z+b.Width)-unit.Z);}
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
                if(view.Gun!=null)view.Gun.localRotation=Quaternion.Euler(-view.Model.GunLift*unit.DeployShare,0,0);
                if(view.Shadow!=null)view.Shadow.position=new Vector3(unit.X,ground+.06f,unit.Z);
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
                    combat.Bar(Top(unit,view)+Vector3.up*.22f,Mathf.Max(.62f,unit.Stats.Radius*2.3f),unit.Health/(float)unit.Stats.Health);
                Machine(unit,view,seen);
            }
            foreach(var (b,view) in buildingViews)
            {
                var stats=b.Stats;
                if(!view.Model.activeSelf||(b.Owner!=PrototypeMatch.Player&&!Match.SeenBy(PrototypeMatch.Player,b)))continue;
                if(b.Health<stats.Health||selectedBuilding==b)
                    combat.Bar(new Vector3(b.X+b.Width*.5f,Match.Map.Height(b.X,b.Z)+stats.Height+.45f,b.Z+b.Width*.5f),b.Width*.8f,b.Health/(float)stats.Health);
                // A machine building cut off from the network blinks amber on its grace, then a dark one gives a faint grey pulse.
                if(!b.Linked&&b.Ready>=1&&UnityEngine.Random.value<Time.deltaTime*(b.Powered?2.5f:.7f))
                    combat.Flash(new Vector3(b.X+b.Width*.5f,Match.Map.Height(b.X,b.Z)+stats.Height+.15f,b.Z+b.Width*.5f),b.Powered?new Color(1,.7f,.28f)*2:new Color(.5f,.52f,.58f),.22f*b.Width,.35f,1.6f);
                // A machine building mending in the network glints here and there over its body.
                if(b.Mending&&UnityEngine.Random.value<Time.deltaTime*3*b.Width)
                {
                    var r=UnityEngine.Random.insideUnitCircle*b.Width*.45f;
                    combat.Flash(new Vector3(b.X+b.Width*.5f+r.x,Match.Map.Height(b.X,b.Z)+stats.Height*UnityEngine.Random.Range(.2f,.95f),b.Z+b.Width*.5f+r.y),NaniteGlow*1.8f,.14f,.5f,2);
                }
            }
            combat.EndBars();
            if(unitViews.Count>Match.Units.Count)
            {
                foreach(var id in new List<int>(unitViews.Keys))
                    if(!Match.Units.Exists(u=>u.Id==id)){var view=unitViews[id];if(view.Shadow!=null)Destroy(view.Shadow.gameObject);Destroy(view.Root);unitViews.Remove(id);}
                selection.Prune(Match.Units);
            }
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
                if(unit.Disassembling){revealBlock.SetFloat("_RevealBelow",Mathf.Lerp(bottom,top,unit.Disassembly));revealBlock.SetColor("_RevealColor",NaniteGlow*2.2f);}
                else revealBlock.Clear();
                foreach(var r in view.Renderers)if(r!=null)r.SetPropertyBlock(revealBlock);
                revealBlock.Clear();view.Dissolving=unit.Disassembling;
            }
            if(!seen||!unit.Mending||(view.Mend-=Time.deltaTime)>0)return;
            view.Mend=.35f+.3f*Mathf.PerlinNoise(Time.time*2,unit.Id);
            var at=Center(unit,view)+new Vector3(Mathf.Sin(Time.time*7+unit.Id),Mathf.Cos(Time.time*5),Mathf.Cos(Time.time*6+unit.Id))*unit.Stats.Radius*.7f;
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
                var from=view.Body.TransformPoint(new Vector3(0,.3f,.3f));float climb=.08f+.62f*unit.Wrap;
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
        const int NaniteThread=28;
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
            // The selected building turns white on the minimap at once, not at the next world refresh.
            RefreshMinimap();hud?.Refresh();
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
        // An own building under construction under the cursor: its model, or its footprint while it is still low.
        PrototypeBuilding SiteAt(Vector2 screen)
        {
            var b=BuildingAt(screen)??Match.Buildings.Find(s=>hoverX>=s.X&&hoverX<s.X+s.Width&&hoverZ>=s.Z&&hoverZ<s.Z+s.Width);
            return b!=null&&b.Owner==PrototypeMatch.Player&&b.Ready<1?b:null;
        }
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
                Shot(attacker,a,hit);
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
                Shot(attacker,a,hit);
            }
            buildingStrikes.Clear();
            // A destroyed building goes up in a chain of blasts across its footprint.
            foreach(var b in ruins)
            {
                if(!Match.SeenBy(PrototypeMatch.Player,b)&&b.Owner!=PrototypeMatch.Player)continue;
                float ground=Match.Map.Height(b.X,b.Z),h=b.Stats.Height;
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
                    if(unit.Disassembling){combat.Flash(Center(unit,view),new Color(.62f,.86f,1)*2.6f,Mathf.Max(.5f,unit.Stats.Radius*2),.5f,2.2f);continue;}
                    // A worker spent on a building melts into it. The building's view comes at once, not with the next refresh,
                    // so the puddle grows under the worker from its first frame.
                    if(unit.AbsorbedInto!=null){StartMelt(unit,view);SyncBuildings();continue;}
                    combat.Explosion(Center(unit,view),Match.HeightAt(unit.X,unit.Z),unit.Stats.Flying?1.3f:Mathf.Max(1,unit.Stats.Radius*2.6f));
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
                float flight=Mathf.Clamp(Vector3.Distance(muzzle,hit)/22,.15f,.6f),burst=Mathf.Max(.8f,stats.Splash*1.5f);
                combat.Flash(hit,new Color(3.2f,1.6f,.55f),burst*.6f,.45f,3.2f,flight);
                combat.Flash(hit+Vector3.up*.2f,new Color(.35f,.33f,.3f),burst*.7f,1.1f,2.2f,flight+.08f);
            }
            else if(stats.Ramp>1)
            {
                // The emitter's beam thickens and brightens as it builds up on one target.
                float focus=Match.Focus(attacker);
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
            var idle=Match.Units.FindAll(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker&&!u.Moving&&u.HarvestTarget==null&&u.Constructing==null&&u.SiteKind<0);
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
            :Mode==MatchMode.Repair?"Ремонт: ЛКМ — своё повреждённое здание или техника · ПКМ / Esc — отмена"
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
                float h=b.Stats.Height,ground=Match.Map.Height(b.X,b.Z);
                bool shown=BuildingShown(b);
                if(view.Model.activeSelf!=shown)view.Model.SetActive(shown);
                if(b.SelfBuilt&&(b.Ready<1||view.Revealing)){Liquid(b,view,shown,h,ground);continue;}
                // The full-sized model rises through the opaque terrain; it never scales up.
                view.Model.transform.position=new Vector3(b.X+b.Width*.5f,ground-h*(1-b.Ready),b.Z+b.Width*.5f);
                view.Ring.SetActive(b.Ready<1&&shown);
                // The ring pulses while a worker builds; a construction that waits for its worker keeps it still.
                bool rising=b.Builder!=null&&PrototypeMatch.Gap(b.Builder,b)<=PrototypeMatch.BuildReach;
                if(b.Ready<1){view.Ring.transform.position=new Vector3(b.X+b.Width*.5f,ground,b.Z+b.Width*.5f);view.Ring.transform.localScale=Vector3.one*b.Width*(rising?.74f+.04f*Mathf.Sin(Time.time*6):.7f);}
            }
        }
        // A machine building rises out of nanite liquid (SETTING.md, «Наниты — суть расы»):
        // - the worker walks to the middle of the site and melts down there into a puddle (StartMelt);
        // - the puddle flows into the building: the liquid fills the building's whole shape from the ground up, flowing
        //   upwards with a cold meniscus on top, while the puddle sinks into it;
        // - then the liquid drains off from the top down in tongues, and the real building shows above its edge, wet and
        //   mirror-dark at first, drying off as the edge moves on; once it stands the last of the film dries.
        // The liquid's own shape swells and shivers all the while (the vertex wobble of Order/NaniteLiquid, on a finely
        // split copy of the model). Driven by the progress, so a cancelled site plays its own way back (OnCancelled) and a
        // destroyed one simply disappears with its view.
        // A flat top keeps its liquid longer towards the middle of the building, as deep as this share of the width
        // (NaniteLevel in OrderNanite.hlsl): it drains off such a face as a shrinking puddle and floods it from the rim,
        // instead of the whole face turning at once.
        const float LiquidRough=.3f,DryingSeconds=1.6f,MeltSeconds=.9f,LiquidWobble=.06f,PoolDepth=.25f;
        readonly Dictionary<Mesh,Mesh> liquidMeshes=new Dictionary<Mesh,Mesh>();
        Mesh puddleMesh;
        // Buildings split to 0.3, the small bodies of units finer, so both bend smoothly.
        Mesh LiquidMesh(Mesh mesh,float maxEdge=.3f){if(!liquidMeshes.TryGetValue(mesh,out var liquid))liquidMeshes[mesh]=liquid=scenery.Own(NaniteMeshes.Subdivided(mesh,maxEdge));return liquid;}
        Mesh PuddleMesh()=>puddleMesh??=scenery.Own(NaniteMeshes.Puddle());
        // shift is how far the liquid's flow has gone (+ up, - down): a flow that turns round is integrated by the caller, so
        // the pattern and the shape slow down and turn instead of jumping.
        void LiquidBlock(Renderer renderer,float front,float shift,float ground,float spikes=1,float wobble=LiquidWobble,Vector4 pool=default,float rough=LiquidRough)
        {
            revealBlock.SetFloat("_Front",front);revealBlock.SetFloat("_FrontNoise",rough);revealBlock.SetFloat("_Shift",shift);
            revealBlock.SetFloat("_Base",ground);revealBlock.SetFloat("_Wobble",wobble);revealBlock.SetFloat("_Spikes",spikes);
            if(pool.z>0)revealBlock.SetVector("_Pool",pool);
            renderer.SetPropertyBlock(revealBlock);revealBlock.Clear();
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
            float build=b.Stats.BuildSeconds,fill=Mathf.Clamp(3/build,.2f,.45f),drain=(1-fill)*build;
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
            if(view.Liquid==null)view.Liquid=scenery.Model("Nanite form",LiquidMesh(view.Model.GetComponent<MeshFilter>().sharedMesh),liquidMaterial,worldRoot.transform);
            if(view.Liquid.activeSelf!=shown)view.Liquid.SetActive(shown);
            view.Liquid.transform.SetPositionAndRotation(view.Model.transform.position,view.Model.transform.rotation);
            view.Liquid.transform.localScale=view.Model.transform.localScale;
            // The puddle at the middle of the site: it spreads while the worker melts, then sinks into the rising liquid.
            float sinking=Mathf.InverseLerp(melt,melt+(fill-melt)*.4f,b.Ready),radius=w*.42f*Mathf.SmoothStep(0,1,b.Ready/Mathf.Max(1e-4f,melt*.8f))*(1-Mathf.SmoothStep(0,1,sinking));
            if(radius>.02f)
            {
                if(view.Puddle==null)view.Puddle=scenery.Model("Nanite puddle",PuddleMesh(),liquidMaterial,worldRoot.transform);
                if(view.Puddle.activeSelf!=shown)view.Puddle.SetActive(shown);
                view.Puddle.transform.SetPositionAndRotation(new Vector3(cx,ground-.02f-.08f*sinking,cz),Quaternion.Euler(0,b.X*37,0));
                view.Puddle.transform.localScale=new Vector3(radius,1,radius);
                LiquidBlock(view.Puddle.GetComponent<MeshRenderer>(),1000,0,ground-1,2);
            }
            else if(view.Puddle!=null){Destroy(view.Puddle);view.Puddle=null;}
            view.PuddleRadius=view.Puddle!=null?radius:0;
            float front,flow,rough;
            static float Ease(float from,float to,float x){float t=Mathf.Clamp01((x-from)/(to-from));return t*t*(3-2*t);}
            if(b.Ready<fill)
            {
                // Filling: the front sets off gently, rises fast and settles at the top; the building itself is not there yet.
                float progress=Mathf.InverseLerp(melt,fill,b.Ready),s=Mathf.SmoothStep(0,1,progress);
                front=view.Front=Mathf.Lerp(ground-LiquidRough*.35f-.02f,full,1-(1-s)*(1-s));flow=1.1f;view.Pool=new Vector4(cx,cz,w*.5f,-pool);
                rough=LiquidRough*(1-Ease(.8f,1,progress));
                revealBlock.SetFloat("_RevealBelow",1000);
            }
            else
            {
                // Draining: one edge for both, so the building shows exactly where the liquid has left. It picks up speed
                // over the first tenth or so and keeps its pace to the end, so the last of the liquid leaves just as the
                // building is ready.
                float s=(b.Ready-fill)/(1-fill);const float Start=.06f;
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
            melts.Add(new Melt{View=melt,At=body.position,Scale=body.lossyScale,Turn=body.rotation,Start=Time.time,Ground=Match.HeightAt(unit.X,unit.Z)});
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
                LiquidBlock(m.View.GetComponent<MeshRenderer>(),1000,-1.5f*(Time.time-m.Start),m.Ground,3*sag,LiquidWobble*Mathf.SmoothStep(0,1,Mathf.Min(1,s*4)));
            }
        }
        // Cancelling a machine site plays it all back, quickly: the liquid shape slumps down into a puddle and the puddle
        // gathers up into the worker, which can be ordered from the start; its model stays hidden until it has formed.
        sealed class Gather{public GameObject Form,Puddle,Body;public PrototypeUnit Worker;public float Start,Top,Ground,Radius,Scale,Flow,Shift,PuddleFrom;public Vector3 Center;public Vector4 Pool;}
        readonly List<Gather> gathers=new List<Gather>();
        const float GatherSeconds=.6f;
        void OnCancelled(PrototypeBuilding b,PrototypeUnit worker)
        {
            // The site's own view goes at once, not with the next refresh: left standing, it would hide the start of the gathering.
            if(!b.SelfBuilt||!buildingViews.TryGetValue(b,out var view)||!view.Model.activeSelf){SyncBuildings();return;}
            float ground=Match.Map.Height(b.X,b.Z);
            // It starts from where the liquid is: as high as it has filled, flowing as it flowed, the puddle as wide as it was.
            var g=new Gather{Worker=worker,Start=Time.time,Ground=ground,Top=view.Liquid!=null?view.Front:ground+b.Stats.Height*1.08f+.15f,Radius=b.Width*.42f,
                Flow=view.Flow,Shift=view.Shift,PuddleFrom=view.PuddleRadius,Pool=view.Pool,Center=new Vector3(b.X+b.Width*.5f,ground,b.Z+b.Width*.5f)};
            g.Form=scenery.Model("Nanite form",LiquidMesh(view.Model.GetComponent<MeshFilter>().sharedMesh),liquidMaterial,worldRoot.transform);
            g.Form.transform.SetPositionAndRotation(view.Model.transform.position,view.Model.transform.rotation);
            g.Puddle=scenery.Model("Nanite puddle",PuddleMesh(),liquidMaterial,worldRoot.transform);
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
                LiquidBlock(g.Form.GetComponent<MeshRenderer>(),Mathf.Lerp(g.Top,g.Ground-.4f,slump),g.Shift,g.Ground,1+slump,pool:g.Pool);
                // The puddle spreads as the shape slumps, then draws in under the worker.
                float spread=Mathf.SmoothStep(0,1,Mathf.Clamp01(s*2.5f-.3f)),draw=Mathf.SmoothStep(0,1,Mathf.Clamp01(s*2-1));
                var at=g.Worker!=null&&g.Worker.Alive?new Vector3(g.Worker.X,Match.HeightAt(g.Worker.X,g.Worker.Z),g.Worker.Z):g.Center;
                g.Puddle.transform.position=Vector3.Lerp(g.Center,at,draw)+Vector3.down*.02f;
                float radius=Mathf.Lerp(Mathf.Max(g.PuddleFrom,g.Radius*spread),.12f,draw);g.Puddle.transform.localScale=new Vector3(radius,1,radius);
                LiquidBlock(g.Puddle.GetComponent<MeshRenderer>(),1000,0,g.Ground-1,2);
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
                float cx=b.X+b.Width*.5f,cz=b.Z+b.Width*.5f,r=b.Stats.Coverage;
                for(int i=0;i<CoverageSegments;i++)
                {
                    float a=i*Mathf.PI*2/CoverageSegments,x=cx+Mathf.Cos(a)*r,z=cz+Mathf.Sin(a)*r;
                    line.SetPosition(i,new Vector3(x,Match.HeightAt(Mathf.Clamp(x,0,63.99f),Mathf.Clamp(z,0,63.99f))+.07f,z));
                }
            }
            for(int i=shown;i<coverageRings.Count;i++)coverageRings[i].enabled=false;
        }
        const int CoverageSegments=72;
        public bool CoverageShown{get;private set;}
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
                ghost.transform.position=new Vector3(x+w*.5f,Match.Map.Height(x,z)+.03f,z+w*.5f);
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
        void RefreshWorld()
        {
            scenery.SetVision(Match.Visible,Match.Explored);
            if(hud!=null)Shader.SetGlobalColor("_OrderTeamColor",hud.OwnColor);
            for(int i=0;i<depositViews.Count;i++)
            {
                var r=Match.Map.Resources[i];depositViews[i].SetActive(Match.Explored[r.X,r.Z]&&r.Remaining>0);
                // Veins shrink as they are mined out.
                if(!r.Plasma)depositViews[i].transform.localScale=new Vector3(1,Mathf.Lerp(.4f,1,r.Remaining/(float)Mathf.Max(1,Match.Balance.MetalDeposit)),1);
            }
            SyncBuildings();
            AnimateBuildings();
            RefreshMinimap();hud?.Refresh();
        }
        // Terrain, fog, deposits and buildings; the camera frame, worker, route and pins are vector overlays in the HUD.
        void RefreshMinimap()
        {
            const int t=TempleScenery.FogTexels;int n=TempleMap.Size*t;
            var pixels=new Color[n*n];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float lit=Match.Visible[x/t,z/t]?1:Match.Explored[x/t,z/t]?.32f:.05f;
                var c=miniBase[z*n+x]*lit;c.a=1;pixels[z*n+x]=c;
            }
            void Cell(int cx,int cz,Color color){for(int z=cz*t;z<cz*t+t;z++)for(int x=cx*t;x<cx*t+t;x++)pixels[z*n+x]=color;}
            foreach(var r in Match.Map.Resources)if(Match.Explored[r.X,r.Z]&&r.Remaining>0)Cell(r.X,r.Z,(r.Plasma?MinimapPanel.Plasma:MinimapPanel.Metal)*(Match.Visible[r.X,r.Z]?1:.4f));
            foreach(var b in Match.Buildings)
            {
                if(!BuildingShown(b))continue;
                var color=(b==selectedBuilding?MinimapPanel.Selected:b.Owner==PrototypeMatch.Player?MinimapPanel.Own:MinimapPanel.Enemy)*(b.Ready>=1?1:.6f);color.a=1;
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
            if(mode==MatchMode.Build&&Array.IndexOf(Match.Roster(PrototypeMatch.Player).Buildable,kind)<0)mode=MatchMode.None;
            if(mode==MatchMode.Attack&&!HasFighter()){Match.Message="Выбранные юниты не атакуют.";mode=MatchMode.None;}
            if(mode==MatchMode.Rally&&(selectedBuilding==null||selectedBuilding.Stats.Trains.Length==0))mode=MatchMode.None;
            if((mode==MatchMode.Move&&!selection.Own)||(mode==MatchMode.Harvest&&!HasHarvester())||(mode==MatchMode.Repair&&Repairers(null).Count==0))SelectWorker();
            if(mode==MatchMode.Repair&&Repairers(null).Count==0){Match.Message="Чинить могут только рабочие людей.";mode=MatchMode.None;}
            Mode=mode;buildKind=mode==MatchMode.Build?kind:-1;ModeHint=Hint();hud?.Refresh();
        }
        bool HasHarvester(){foreach(var u in selection.Orderable)if(u.Stats.HarvestAmount>0)return true;return false;}
        // The selected workers of a side that repairs, but not the machinery they are to mend.
        List<PrototypeUnit> Repairers(PrototypeUnit target)
        {
            var workers=new List<PrototypeUnit>();
            foreach(var u in selection.Orderable)if(u.Kind==UnitKind.Worker&&u!=target&&Match.Roster(u.Owner).Repairs)workers.Add(u);
            return workers;
        }
        // Every selected worker mends the target, as SCVs do; the rest of the selection walks up to it.
        public bool RepairSelection(PrototypeBuilding building,PrototypeUnit unit)
        {
            var workers=Repairers(unit);
            if(workers.Count==0){Match.Message="Чинить могут только рабочие людей.";hud?.Refresh();return false;}
            int started=0;string reason=null;
            foreach(var w in workers){if(building!=null?Match.Repair(w,building):Match.Repair(w,unit))started++;else reason??=Match.Message;}
            if(started==0){Match.Message=reason;hud?.Refresh();return false;}
            float cx=building!=null?building.X+building.Width*.5f:unit.X,cz=building!=null?building.Z+building.Width*.5f:unit.Z;
            var rest=new List<PrototypeUnit>(selection.Orderable);rest.RemoveAll(u=>workers.Contains(u)||u==unit);
            var message=$"{(started>1?$"Рабочие ×{started}":workers[0].Stats.Name)}: ремонт — {(building!=null?building.Stats.Name:unit.Stats.Name)}.";
            if(rest.Count>0)Match.MoveGroup(rest,cx,cz);
            Match.Message=message;Mark(new Vector2(cx,cz),true);hud?.Refresh();
            return true;
        }
        // Shift: every selected worker repairs the target after what it is doing now.
        bool QueueRepairSelection(PrototypeBuilding building,PrototypeUnit unit)
        {
            var workers=Repairers(unit);int queued=0;string reason=null;
            foreach(var w in workers){if(building!=null?Match.QueueRepair(w,building):Match.QueueRepair(w,unit))queued++;else reason??=Match.Message;}
            if(queued==0){if(reason!=null)Match.Message=reason;hud?.Refresh();return false;}
            Match.Message=$"Ремонт добавлен в очередь: {(building!=null?building.Stats.Name:unit.Stats.Name)}.";
            Mark(building!=null?new Vector2(building.X+building.Width*.5f,building.Z+building.Width*.5f):new Vector2(unit.X,unit.Z),true);hud?.Refresh();
            return true;
        }
        // Any packed howitzer in the selection deploys where it stands; when all of them are deployed, they pack up.
        public void ToggleDeploySelection()
        {
            var guns=new List<PrototypeUnit>(selection.Orderable).FindAll(u=>u.Stats.Siege);if(guns.Count==0)return;
            bool deploy=guns.Exists(u=>u.SiegeMode==SiegeState.Packed||u.SiegeMode==SiegeState.Packing);
            foreach(var g in guns)if((g.SiegeMode==SiegeState.Packed||g.SiegeMode==SiegeState.Packing)==deploy)Match.ToggleDeploy(g);
            if(guns.Count>1)Match.Message=$"Гаубицы ×{guns.Count}: {(deploy?"развёртывание":"сворачиваются")}.";
            hud?.Refresh();
        }
        public void ToggleAutoRepair()
        {
            var workers=Repairers(null);if(workers.Count==0)return;
            bool on=!workers.TrueForAll(u=>u.AutoRepair);
            foreach(var w in workers)Match.SetAutoRepair(w,on);
            Match.Message=$"Авторемонт {(on?"включён":"выключен")}.";hud?.Refresh();
        }
        // Right click on an own extractor means its rift: the ray past the model would hit the ground behind it.
        Vector2 ExtractorPoint(Vector2 screen,Vector2 point)
        {
            var b=BuildingAt(screen);
            return b!=null&&b.Owner==PrototypeMatch.Player&&b.Stats.Extracts?new Vector2(b.X+b.Width*.5f,b.Z+b.Width*.5f):point;
        }
        // The own building or machinery under the cursor that needs repair; with `damaged` only one that is damaged.
        bool RepairTargetAt(Vector2 screen,bool damaged,out PrototypeBuilding building,out PrototypeUnit unit)
        {
            unit=UnitAt(screen);building=null;
            if(unit!=null&&(unit.Owner!=PrototypeMatch.Player||!unit.Stats.Mechanical||(damaged&&unit.Health>=unit.Stats.Health)))unit=null;
            if(unit==null)
            {
                building=BuildingAt(screen);
                if(building!=null&&(building.Owner!=PrototypeMatch.Player||building.Ready<1||(damaged&&building.Health>=building.Stats.Health)))building=null;
            }
            return unit!=null||building!=null;
        }
        public void DisassembleSelection()
        {
            int started=0,metal=0,plasma=0;string reason=null;
            foreach(var u in new List<PrototypeUnit>(selection.Orderable))
            {
                var (m,p)=Match.DisassemblyReturn(u);
                if(Match.Disassemble(u)){started++;metal+=m;plasma+=p;}else reason??=Match.Message;
            }
            if(started>1)Match.Message=$"Разборка: {started} юнитов, вернётся металл {metal}"+(plasma>0?$", плазма {plasma}.":".");
            else if(started==0&&reason!=null)Match.Message=reason;
            SetMode(MatchMode.None);
        }
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
        // Construction, as in StarCraft: the selected worker nearest the site goes to build it; with no worker selected, the
        // nearest free one does (an idle one first, then a metal miner with empty hands).
        public int BuildOrders{get;private set;}
        public bool Build(int x,int z,int kind)=>Build(x,z,kind,false);
        // queue: Shift — the selected worker already building or with builds queued takes this one too.
        bool Build(int x,int z,int kind,bool queue)
        {
            if(Array.IndexOf(Match.Roster(PrototypeMatch.Player).Buildable,kind)<0)return false;
            int w=Match.Roster(PrototypeMatch.Player).Buildings[kind].Width;float cx=x+w*.5f,cz=z+w*.5f;
            PrototypeUnit worker=null;float best=float.MaxValue;
            // A machine worker becomes the building, so Shift sends the next free worker of the selection instead of
            // queueing the order.
            bool next=Match.Roster(PrototypeMatch.Player).ConsumesWorker&&queue;
            if(next)queue=false;
            if(queue)foreach(var u in selection.Orderable)if(u.Kind==UnitKind.Worker&&(u.SiteKind>=0||u.Constructing!=null||u.Queued.Count>0)){worker=u;break;}
            if(worker==null)foreach(var u in selection.Orderable)
            {
                if(u.Kind!=UnitKind.Worker||(next&&u.SiteKind>=0))continue;
                float d=(u.X-cx)*(u.X-cx)+(u.Z-cz)*(u.Z-cz);
                if(d<best){best=d;worker=u;}
            }
            if(worker==null)foreach(var u in Match.Units)
            {
                if(u.Owner!=PrototypeMatch.Player||u.Kind!=UnitKind.Worker||u.Constructing!=null||u.SiteKind>=0)continue;
                float d=(u.X-cx)*(u.X-cx)+(u.Z-cz)*(u.Z-cz)+(u.HarvestTarget==null&&!u.Moving?0:u.Carrying>0||(u.HarvestTarget!=null&&u.HarvestTarget.Plasma)?20000:10000);
                if(d<best){best=d;worker=u;}
            }
            if(worker==null){Match.Message="Нет рабочих.";hud?.Refresh();return false;}
            if(queue){bool queued=Match.QueueBuild(worker,x,z,kind);if(queued)Mark(new Vector2(cx,cz),true);hud?.Refresh();return queued;}
            bool ok=Match.Build(worker,x,z,kind);
            if(ok){BuildOrders++;Mark(new Vector2(cx,cz),true);}
            hud?.Refresh();return ok;
        }
        // Right click on an own building under construction: the nearest selected worker carries on with it, the rest of the
        // selection walks up to it. False when no selected worker can take it, so the click is a plain move.
        bool ResumeSelection(PrototypeBuilding site)
        {
            if(site.Builder!=null&&!selection.Contains(site.Builder))return false;
            PrototypeUnit worker=null;float best=float.MaxValue,cx=site.X+site.Width*.5f,cz=site.Z+site.Width*.5f;
            foreach(var u in selection.Orderable)
            {
                if(u.Kind!=UnitKind.Worker)continue;
                float d=(u.X-cx)*(u.X-cx)+(u.Z-cz)*(u.Z-cz);
                if(d<best){best=d;worker=u;}
            }
            if(worker==null||!Match.Resume(worker,site))return false;
            var rest=new List<PrototypeUnit>(selection.Orderable);rest.Remove(worker);
            if(rest.Count>0){var message=Match.Message;Match.MoveGroup(rest,cx,cz);Match.Message=message;}
            Mark(new Vector2(cx,cz),true);hud?.Refresh();
            return true;
        }
        public bool CancelConstruction()
        {
            bool ok=selectedBuilding!=null&&selectedBuilding.Owner==PrototypeMatch.Player&&Match.CancelConstruction(selectedBuilding);
            if(ok)selectedBuilding=null;
            hud?.Refresh();return ok;
        }
        // Production in the selected building; the queue and rally point live in the simulation.
        public bool Train(UnitKind kind){bool ok=selectedBuilding!=null&&Match.Train(selectedBuilding,kind);hud?.Refresh();return ok;}
        public void CancelTraining(int index){if(selectedBuilding!=null)Match.CancelTraining(selectedBuilding,index);hud?.Refresh();}
        // A rally point on a deposit is drawn at its centre: workers will harvest it.
        public void SetRallyPoint(Vector2 point)
        {
            if(selectedBuilding==null||selectedBuilding.Owner!=PrototypeMatch.Player||selectedBuilding.Stats.Trains.Length==0)return;
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
            if(Mode==MatchMode.Build){bool queue=Queueing();if(Build((int)point.x,(int)point.y,buildKind,queue)&&!queue)SetMode(MatchMode.None);return;}
            if(Mode==MatchMode.Attack){bool queue=Queueing();AttackMoveSelection(point,queue);if(!queue)SetMode(MatchMode.None);return;}
            // From the minimap only a building can be pointed at.
            if(Mode==MatchMode.Repair)
            {
                var target=Match.Buildings.Find(b=>b.Owner==PrototypeMatch.Player&&b.Ready>=1&&point.x>=b.X&&point.x<b.X+b.Width&&point.y>=b.Z&&point.y<b.Z+b.Width);
                if(target==null){Match.Message="Укажите своё здание.";hud?.Refresh();return;}
                if(RepairSelection(target,null))SetMode(MatchMode.None);
                return;
            }
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
        // An attack the player cannot see on screen: a line in the event log with the grid square, and a ping on the minimap.
        void OnUnderAttack(float x,float z,bool building)
        {
            var frame=ViewportOnGround();var point=new Vector2(x,z);
            if(frame!=null&&Inside(frame,point))return;
            alerts.RemoveAll(a=>Time.unscaledTime-a.time>AlertSeconds);
            alerts.Add((point,Time.unscaledTime));
            Match.Message=(building?"Атакована база: ":"Наши войска под атакой: ")+MapGrid.Name(x,z,TempleMap.Size)+".";
            hud?.Refresh();
        }
        static bool Inside(Vector2[] polygon,Vector2 p)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                if((polygon[i].y>p.y)!=(polygon[j].y>p.y)&&p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;
            return inside;
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
        // A new match on the same screen with other factions (the editor cheat panel).
        (FactionId,FactionId)? restartAs;
        public void Restart(FactionId player,FactionId enemy)
        {
            var root=ui;var exit=onExit;var with=services;
            Shutdown();restartAs=(player,enemy);Initialize(root,exit,with);
        }
        public void Shutdown()
        {
            enabled=false;
            hud?.Dispose();hud=null;
            if(ecsWorld!=null&&ecsWorld.IsCreated&&ecsWorld.EntityManager.Exists(matchEntity))ecsWorld.EntityManager.DestroyEntity(matchEntity);
            if(worldRoot!=null){worldRoot.SetActive(false);Destroy(worldRoot);worldRoot=null;}
            scenery?.Dispose();scenery=null;
            if(miniTexture!=null){Destroy(miniTexture);miniTexture=null;}
            buildingViews.Clear();depositViews.Clear();pinViews.Clear();siteGhosts.Clear();coverageRings.Clear();pins.Clear();Match=null;
            foreach(var m in melts)if(m.View!=null)Destroy(m.View);melts.Clear();
            foreach(var g in gathers){if(g.Form!=null)Destroy(g.Form);if(g.Puddle!=null)Destroy(g.Puddle);if(g.Body!=null)Destroy(g.Body);}gathers.Clear();
            combat?.Dispose();combat=null;strikes.Clear();deaths.Clear();buildingStrikes.Clear();ruins.Clear();waypointLines.Clear();waypointDots.Clear();alerts.Clear();
            unitViews.Clear();unitModels.Clear();selectionRings.Clear();hoverRings.Clear();hovered.Clear();markers.Clear();
            selection=new UnitSelection();lastClickUnit=null;lastGroup=-1;pressing=dragging=false;
            Array.Clear(buildingMeshes,0,buildingMeshes.Length);liquidMeshes.Clear();liquidSpans.Clear();puddleMesh=null;
            ghostModel=null;ghostKind=-1;Mode=MatchMode.None;
        }
        void OnDestroy()=>Shutdown();
    }
}
