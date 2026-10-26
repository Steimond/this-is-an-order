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
    // Disposable view of a match: it builds the world from the match each frame and gives the player's orders as commands.
    // No scene-authored gameplay state. Parts: Units (unit puppets), Buildings (buildings, nanite liquid, coverage),
    // Selection (picking, groups, rings, waypoints), Orders (what the selection is told), Combat (shots and deaths),
    // Markers (order markers, focus ring, rally), Camera, Map (minimap, pins, alerts).
    [DisallowMultipleComponent]
    public sealed partial class TemplePrototypeView : MonoBehaviour, IMatchHost
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
            unitRingMaterial, hoverRingMaterial, enemyRingMaterial, enemyHoverMaterial, markerMaterial, oreMaterial, plasmaCargoMaterial, naniteMaterial, liquidMaterial, spikeMaterial, cocoonMaterial, coverageMaterial, darkCoverageMaterial;
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
        // Real time not yet played by the match, and how far the frame is between the last two match steps (0–1).
        float pending, blend;
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

        // A scene of its own can lay the match out instead of the test ground's squads and bot (VisualLab): the sides
        // to play and a setup run once the match exists.
        [NonSerialized] public (FactionId player,FactionId enemy)? Sides;
        [NonSerialized] public Action<PrototypeMatch> Setup;
        // A scene of its own may make the match itself, of any sides (StressLab); the balance snapshot is given.
        [NonSerialized] public Func<MatchBalance,PrototypeMatch> Create;
        public void Initialize(VisualElement root, Action exit, MatchServices services=null)
        {
            if(Match!=null)return;
            this.services=services;
            enabled=true;Mode=MatchMode.None;buildKind=-1;selectedBuilding=null;selectedDeposit=null;focus=new Vector3(13,0,13);zoom=targetZoom=StartZoom;refresh=0;
            var catalog=Resources.Load<BalanceCatalog>("PrototypeBalanceCatalog");
            if(catalog==null)throw new InvalidOperationException("Create balance assets using Order/Create Missing Balance Assets.");
            // Factions: those chosen for the match in the menu (by default humans against the machines), or a restart's.
            var (player,enemy)=restartAs??Sides??(services?.App?.PlayerFaction??FactionId.Expedition,services?.App?.EnemyFaction??FactionId.Resonance);restartAs=null;
            Match=Create!=null?Create(catalog.Snapshot()):new PrototypeMatch(catalog.Snapshot(),player,enemy);ui=root;onExit=exit;pending=0;blend=1;
            worldRoot=new GameObject("Temple Basin • generated world");
            var cameraObject=new GameObject("RTS Camera",typeof(Camera),typeof(AudioListener));
            cameraObject.transform.SetParent(worldRoot.transform);
            MapCamera=cameraObject.GetComponent<Camera>();MapCamera.orthographic=false;MapCamera.fieldOfView=42;
            MapCamera.nearClipPlane=.3f;MapCamera.farClipPlane=180;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            scenery=new TempleScenery(Match.Map,worldRoot.transform,MapCamera);
            focusHeight=(float)Match.HeightAt(focus.x,focus.z);
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
                go.transform.position=new Vector3((float)r.X+.5f,(float)Match.Map.Height(r.X,r.Z),(float)r.Z+.5f);go.transform.rotation=Quaternion.Euler(0,i*67%360,0);
                if(r.Plasma)scenery.PlasmaWisps(go.transform,new Vector3(0,.2f,0));
                depositViews.Add(go);
            }
            beamMaterial=scenery.Glow("Mining beam",new Color(.45f,.9f,1)*3);
            naniteMaterial=scenery.Glow("Nanite threads",new Color(.62f,.86f,1)*2.4f);
            // The liquid is tuned in its material asset (Resources/NaniteLiquid), live, even in Play Mode; the spikes take
            // its settings (SyncSpikes).
            liquidMaterial=Resources.Load<Material>("NaniteLiquid");
            if(liquidMaterial==null)liquidMaterial=scenery.NaniteLiquid("Nanite liquid");
            spikeMaterial=scenery.Own(new Material(liquidMaterial){name="Nanite spikes"});spikeMaterial.EnableKeyword("NANITE_SPIKES");
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
            if(Setup!=null)Setup(Match);else{Match.AddTestSquad();Match.AddEnemySquad();Match.AddEnemyBase();Match.StartBot();}
            Match.PrepareNavigation();SelectWorker();
            // The machines' liquid meshes take a moment to make, the spikes most: made now, not as the first site rises.
            if(Match.FactionOf(PrototypeMatch.Player)==FactionId.Resonance||Match.FactionOf(PrototypeMatch.Enemy)==FactionId.Resonance)
            {
                var machines=Match.Balance.Of(FactionId.Resonance).Buildings;
                for(int kind=0;kind<machines.Length;kind++)if(machines[kind]!=null){var shape=BuildingMesh(FactionId.Resonance,kind);LiquidMesh(shape);SpikeMesh(shape);}
            }
            var square=new MeshBuilder();square.Quad(new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f));
            footprint=scenery.Model("Placement footprint",scenery.Own(square.ToMesh("Footprint")),footprintMaterial,worldRoot.transform,false);footprint.SetActive(false);
            UpdateCamera();RefreshWorld();
        }
        Mesh BuildingMesh(FactionId faction,int kind)
        {
            int f=(int)faction;
            if(buildingMeshes[f,kind]==null){var stats=Match.Balance.Of(faction).Buildings[kind];buildingMeshes[f,kind]=scenery.Own(faction==FactionId.Resonance?MachineModels.Building(kind,stats.Width,(float)stats.Height):MatchModels.Building(kind,stats.Width,(float)stats.Height));}
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
            PlayMatch();
            focus.x=Mathf.Clamp(focus.x,0,MapSize);focus.z=Mathf.Clamp(focus.z,0,MapSize);
            zoom=Mathf.Lerp(zoom,targetZoom,1-Mathf.Exp(-Time.deltaTime*12));
            focusHeight=Mathf.Lerp(focusHeight,(float)Match.HeightAt(Mathf.Clamp(focus.x,0,MapSize-.1f),Mathf.Clamp(focus.z,0,MapSize-.1f)),1-Mathf.Exp(-Time.deltaTime*4));
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
                footprint.transform.position=new Vector3(hoverX+w*.5f,(float)Match.Map.Height(hoverX,hoverZ)+.03f,hoverZ+w*.5f);
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
            SyncSpikes();
            AnimateBuildings();
            AnimateMelts();
            AnimateGathers();
            scenery.Tick(Time.deltaTime);
            refresh+=Time.deltaTime;if(refresh>.15f){refresh=0;RefreshWorld();}
        }
        // The match plays the frame's time in its fixed steps, after this frame's orders; a long frame (a hitch, a breakpoint)
        // plays at most a few steps, so the match slows down instead of freezing the view to catch up.
        const int MaxStepsPerFrame=5;
        void PlayMatch()
        {
            pending+=Time.deltaTime;
            for(int steps=0;pending>=PrototypeMatch.TickSeconds;steps++)
            {
                if(steps==MaxStepsPerFrame){pending=0;break;}
                Match.Advance();pending-=(float)PrototypeMatch.TickSeconds;
            }
            blend=Mathf.Clamp01(pending/(float)PrototypeMatch.TickSeconds);
        }
        // The view orders only through commands of the player's side, applied between the match's steps.
        const int Me=PrototypeMatch.Player;
        public bool Order(MatchCommand command)=>Match.Execute(command);
        // The map's side in cells, for the camera and the clamps of points on the ground.
        float MapSize=>Match.Map.Size;
        // Where a unit is drawn this frame: between where it stood before the last match step and where it stands now.
        Vector2 Place(PrototypeUnit unit)=>new Vector2(Mathf.Lerp((float)unit.PreviousX,(float)unit.X,blend),Mathf.Lerp((float)unit.PreviousZ,(float)unit.Z,blend));
        const int NaniteThread=28;
        float Option(string id,float fallback){var settings=services?.App?.Settings;return settings.HasValue?MenuCatalog.Value(settings.Value,MenuCatalog.Find(id)):fallback;}
        // "Скорость камеры" 0–100 maps to 0.4–1.6 of the base speed; 50 keeps the original pace.
        float CameraSpeed()=>.4f+Option("controls.cameraSpeed",50)/100f*1.2f;
        string Hint()=>Mode==MatchMode.Build?"Укажите место на карте · ПКМ / Esc — отмена":Mode==MatchMode.Move?"Движение: ЛКМ — точка на карте или миникарте · ПКМ / Esc — отмена"
            :Mode==MatchMode.Harvest?"Добыча: ЛКМ — разведанная залежь · ПКМ / Esc — отмена"
            :Mode==MatchMode.Attack?"Атака: ЛКМ — враг или точка (атака с движением) · ПКМ / Esc — отмена"
            :Mode==MatchMode.Rally?"Точка сбора: ЛКМ — место или залежь на карте или миникарте · ПКМ / Esc — отмена"
            :Mode==MatchMode.Repair?"Ремонт: ЛКМ — своё повреждённое здание или техника · ПКМ / Esc — отмена"
            :Mode==MatchMode.Ping?"Метка: ЛКМ — место на карте или миникарте · Alt+ЛКМ — всегда":"";
        // A machine building rises out of nanite liquid (SETTING.md, «Наниты — суть расы»):
        // - the worker walks to the middle of the site and melts down there into a puddle (StartMelt);
        // - the puddle flows into the building: the liquid fills the building's whole shape from the ground up, flowing
        //   upwards with a cold meniscus on top, while the puddle sinks into it;
        // - then the liquid drains off from the top down in tongues, and the real building shows above its edge, wet and
        //   mirror-dark at first, drying off as the edge moves on; once it stands the last of the film dries.
        // The liquid's own shape swells and shivers all the while (the vertex wobble of Order/NaniteLiquid, on a finely
        // split copy of the model), and magnetic spikes stand out of it, rising and sinking in patches (a child mesh of
        // cones, NaniteMeshes.Spikes). Driven by the progress, so a cancelled site plays its own way back (OnCancelled) and a
        // destroyed one simply disappears with its view.
        // A flat top keeps its liquid longer towards the middle of the building, as deep as this share of the width
        // (NaniteLevel in OrderNanite.hlsl): it drains off such a face as a shrinking puddle and floods it from the rim,
        // instead of the whole face turning at once.
        const float LiquidRough=.3f,DryingSeconds=1.6f,MeltSeconds=.9f,PoolDepth=.25f;
        Mesh puddleMesh;
        Mesh puddleSpikes;
        const float GatherSeconds=.6f;
        const int CoverageSegments=72;
        public bool CoverageShown{get;private set;}
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
        void BuildUI()
        {
            int n=Match.Map.Size*TempleScenery.FogTexels;
            miniTexture=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            miniBase=scenery.MinimapBase();
            hud=new MatchHud(ui,this,services);
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
            if(worldRoot!=null){worldRoot.SetActive(false);Destroy(worldRoot);worldRoot=null;}
            scenery?.Dispose();scenery=null;
            if(miniTexture!=null){Destroy(miniTexture);miniTexture=null;}
            buildingViews.Clear();depositViews.Clear();pinViews.Clear();siteGhosts.Clear();coverageRings.Clear();pins.Clear();Match=null;
            foreach(var m in melts)if(m.View!=null)Destroy(m.View);melts.Clear();
            foreach(var g in gathers){if(g.Form!=null)Destroy(g.Form);if(g.Puddle!=null)Destroy(g.Puddle);if(g.Body!=null)Destroy(g.Body);}gathers.Clear();
            combat?.Dispose();combat=null;strikes.Clear();deaths.Clear();buildingStrikes.Clear();ruins.Clear();waypointLines.Clear();waypointDots.Clear();alerts.Clear();
            unitViews.Clear();idlePuppets.Clear();unitModels.Clear();selectionRings.Clear();hoverRings.Clear();hovered.Clear();markers.Clear();
            selection=new UnitSelection();lastClickUnit=null;lastGroup=-1;pressing=dragging=false;
            Array.Clear(buildingMeshes,0,buildingMeshes.Length);liquidMeshes.Clear();liquidSpans.Clear();spikeMeshes.Clear();puddleMesh=puddleSpikes=null;
            ghostModel=null;ghostKind=-1;Mode=MatchMode.None;
        }
        void OnDestroy()=>Shutdown();
    }
}
