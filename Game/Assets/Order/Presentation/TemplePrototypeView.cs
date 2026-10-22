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
        public PrototypeMatch Match { get; private set; }
        public Camera MapCamera { get; private set; }
        public MatchMode Mode { get; private set; }
        public int BuildKind => buildKind;
        public int SelectedBuilding => selectedBuilding;
        public ResourceDeposit SelectedDeposit => selectedDeposit;
        public IReadOnlyList<Vector2> Pins => pins;
        public Texture MinimapTexture => miniTexture;
        public string ModeHint { get; private set; } = "";
        VisualElement ui;
        MatchHud hud;
        MatchServices services;
        readonly List<Vector2> pins = new List<Vector2>();
        readonly List<GameObject> pinViews = new List<GameObject>();
        Texture2D miniTexture;
        GameObject worldRoot, scout, ghost, selectionRing, ghostModel;
        int ghostKind=-1;
        readonly List<GameObject> buildingViews = new List<GameObject>();
        readonly List<GameObject> depositViews = new List<GameObject>();
        readonly List<Material> materials = new List<Material>();
        Mesh ground;
        Color[] groundColors;
        readonly List<Color> tileColors = new List<Color>();
        Entity matchEntity;
        World ecsWorld;
        Vector3 focus = new Vector3(13,0,13);
        float zoom=18, refresh;
        int buildKind=-1, hoverX, hoverZ;
        int selectedBuilding=-1;
        ResourceDeposit selectedDeposit;
        bool overUI, hasHover;
        Action onExit;
        Material ghostMaterial;

        void Start()
        {
            if(Match==null)Initialize(GetComponent<UIDocument>().rootVisualElement,()=>UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"));
        }

        public void Initialize(VisualElement root, Action exit, MatchServices services=null)
        {
            if(Match!=null)return;
            this.services=services;
            enabled=true;Mode=MatchMode.None;buildKind=-1;selectedBuilding=-1;selectedDeposit=null;focus=new Vector3(13,0,13);zoom=18;refresh=0;
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
            MapCamera.clearFlags=CameraClearFlags.SolidColor;MapCamera.backgroundColor=Color.black;
            MapCamera.nearClipPlane=.1f;MapCamera.farClipPlane=180;
            MapCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderShadows=true;
            var sun=new GameObject("Sun",typeof(Light));sun.transform.SetParent(worldRoot.transform);
            sun.transform.rotation=Quaternion.Euler(48,-35,0);var light=sun.GetComponent<Light>();
            light.type=LightType.Directional;light.color=new Color(1,.9f,.72f);light.intensity=1.5f;light.shadows=LightShadows.Soft;light.shadowStrength=.85f;
            CreateGround();
            for(int i=0;i<Match.Map.Resources.Count;i++)
            {
                var r=Match.Map.Resources[i];
                var go=Primitive(r.Gas?PrimitiveType.Cylinder:PrimitiveType.Cube,r.Gas?new Color(.3f,.9f,.6f):new Color(.25f,.7f,1),
                    new Vector3(r.X+.5f,Match.Map.Height(r.X,r.Z)+.7f,r.Z+.5f),r.Gas?new Vector3(.8f,.7f,.8f):new Vector3(.65f,1.25f,.65f));
                go.name=r.Gas?"Gas placeholder":"Mineral placeholder";
                if(!r.Gas)go.transform.rotation=Quaternion.Euler(0,25,12);
                depositViews.Add(go);
            }
            scout=Primitive(PrimitiveType.Capsule,new Color(1,.78f,.3f),Vector3.zero,new Vector3(.65f,.65f,.65f));scout.name="Worker / scout";
            selectionRing=Primitive(PrimitiveType.Cylinder,new Color(.85f,.9f,.25f),Vector3.zero,new Vector3(1,.015f,1));
            ghost=Primitive(PrimitiveType.Cube,Color.green,Vector3.zero,Vector3.one);ghostMaterial=ghost.GetComponent<Renderer>().sharedMaterial;ghost.SetActive(false);
            ghostMaterial.SetFloat("_Grid",1);
            BuildUI();UpdateCamera();RefreshWorld();
        }
        GameObject BuildingModel(int kind)
        {
            var stats=Match.Balance.Buildings[kind];float w=stats.Width,h=stats.Height;
            var root=new GameObject(stats.Name);root.transform.SetParent(worldRoot.transform);
            void Part(Vector3 position,Vector3 scale,Color color,PrimitiveType type=PrimitiveType.Cube)
            {
                var part=Primitive(type,color,Vector3.zero,Vector3.one);part.transform.SetParent(root.transform,false);part.transform.localPosition=position;part.transform.localScale=scale;
            }
            Color metal=new Color(.18f,.25f,.3f),armor=new Color(.42f,.49f,.52f),team=new Color(.08f,.48f,.68f),glow=new Color(.2f,.9f,1);
            Part(new Vector3(0,h*.12f,0),new Vector3(w*.94f,h*.24f,w*.94f),metal);
            Part(new Vector3(0,h*.48f,0),new Vector3(w*.7f,h*.65f,w*.68f),armor);
            Part(new Vector3(0,h*.83f,0),new Vector3(w*.76f,h*.12f,w*.75f),team);
            for(int side=-1;side<=1;side+=2)
            {
                Part(new Vector3(side*w*.4f,h*.38f,0),new Vector3(w*.18f,h*.5f,w*.5f),metal);
                Part(new Vector3(side*w*.4f,h*.66f,-w*.22f),new Vector3(w*.1f,h*.07f,w*.18f),glow);
            }
            if(kind==1)
            {
                Part(new Vector3(w*.24f,h*.8f,w*.2f),new Vector3(w*.12f,h*.1f,w*.12f),metal,PrimitiveType.Cylinder);
                Part(new Vector3(0,h*.32f,-w*.35f),new Vector3(w*.4f,h*.35f,.08f),team);
            }
            else Part(new Vector3(0,h*.94f,0),new Vector3(w*.35f,h*.03f,w*.35f),glow,PrimitiveType.Cylinder);
            return root;
        }
        Material Material(Color color)
        {
            var shader=Shader.Find("Order/PrototypeTerrain");
            var material=new Material(shader);material.SetFloat("_VertexColors",0);material.SetColor("_BaseColor",color);materials.Add(material);return material;
        }
        GameObject Primitive(PrimitiveType type,Color color,Vector3 position,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(type);go.transform.SetParent(worldRoot.transform);
            go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=Material(color);
            Destroy(go.GetComponent<Collider>());return go;
        }
        void CreateGround()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int z=0;z<64;z++)for(int x=0;x<64;x++)
            {
                float a=Match.Map.Height(x,z,0),b=Match.Map.Height(x,z,1);
                Color color=Match.Map.Ramp(x,z)?new Color(.58f,.48f,.28f):a>0?new Color(.28f,.43f,.37f):new Color(.31f,.32f,.23f);
                Quad(new Vector3(x,a,z),new Vector3(x,a,z+1),new Vector3(x+1,b,z+1),new Vector3(x+1,b,z),color,vertices,triangles);
                // Vertical cell walls make cliffs readable from either side of the map.
                Quad(new Vector3(x,-.5f,z),new Vector3(x,-.5f,z+1),new Vector3(x,a,z+1),new Vector3(x,a,z),color*.55f,vertices,triangles);
                Quad(new Vector3(x+1,b,z),new Vector3(x+1,b,z+1),new Vector3(x+1,-.5f,z+1),new Vector3(x+1,-.5f,z),color*.55f,vertices,triangles);
                Quad(new Vector3(x,a,z),new Vector3(x+1,b,z),new Vector3(x+1,-.5f,z),new Vector3(x,-.5f,z),color*.65f,vertices,triangles);
                Quad(new Vector3(x,-.5f,z+1),new Vector3(x+1,-.5f,z+1),new Vector3(x+1,b,z+1),new Vector3(x,a,z+1),color*.65f,vertices,triangles);
            }
            ground=new Mesh {name="Temple cell terrain",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            ground.SetVertices(vertices);ground.SetTriangles(triangles,0);ground.RecalculateNormals();ground.RecalculateBounds();
            groundColors=new Color[vertices.Count];
            var go=new GameObject("Terrain",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(worldRoot.transform);
            go.GetComponent<MeshFilter>().sharedMesh=ground;go.GetComponent<MeshCollider>().sharedMesh=ground;
            var material=new Material(Shader.Find("Order/PrototypeTerrain"));materials.Add(material);go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,List<Vector3> v,List<int> t)
        {
            int n=v.Count;v.Add(a);v.Add(b);v.Add(c);v.Add(d);tileColors.Add(color);
            t.Add(n);t.Add(n+1);t.Add(n+2);t.Add(n);t.Add(n+2);t.Add(n+3);
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
                if(!overUI)zoom=Mathf.Clamp(zoom-mouse.scroll.ReadValue().y*.015f,10,38);
                hasHover=!overUI&&Physics.Raycast(MapCamera.ScreenPointToRay(p),out var hit,180);
                if(hasHover)
                {
                    // Repeat ray explicitly to keep the hit definitely assigned on all compiler versions.
                    Physics.Raycast(MapCamera.ScreenPointToRay(p),out hit,180);
                    hoverX=Mathf.FloorToInt(hit.point.x);hoverZ=Mathf.FloorToInt(hit.point.z);
                    var point=new Vector2(hit.point.x,hit.point.z);
                    if(mouse.rightButton.wasPressedThisFrame){if(Mode!=MatchMode.None)SetMode(MatchMode.None);else if(Match.Selected)Match.Move(hit.point.x,hit.point.z);}
                    if(mouse.leftButton.wasPressedThisFrame)
                    {
                        if(keys!=null&&keys.altKey.isPressed)AddPin(point);
                        else if(Mode==MatchMode.Build){if(Match.Build(hoverX,hoverZ,buildKind))SetMode(MatchMode.None);}
                        else if(Mode!=MatchMode.None)OrderTo(point);
                        else
                        {
                            Match.Selected=Vector2.Distance(new Vector2(hit.point.x,hit.point.z),new Vector2(Match.X,Match.Z))<1.5f;
                            selectedBuilding=Match.Selected?-1:Match.Buildings.FindIndex(b=>hoverX>=b.X&&hoverX<b.X+b.Width&&hoverZ>=b.Z&&hoverZ<b.Z+b.Width);
                            selectedDeposit=Match.Selected||selectedBuilding>=0||!Match.Map.Inside(hoverX,hoverZ)||!Match.Explored[hoverX,hoverZ]?null:Match.Map.ResourceAt(hoverX,hoverZ);
                            hud?.Refresh();
                        }
                    }
                }
            }
            focus.x=Mathf.Clamp(focus.x,0,64);focus.z=Mathf.Clamp(focus.z,0,64);UpdateCamera();
            Vector3 nextPosition=new Vector3(Match.X,Match.HeightAt(Match.X,Match.Z)+.65f,Match.Z);
            Vector3 direction=nextPosition-scout.transform.position;direction.y=0;
            if(direction.sqrMagnitude>.0001f)scout.transform.rotation=Quaternion.Slerp(scout.transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*12);
            scout.transform.position=nextPosition;
            selectionRing.SetActive(Match.Selected);selectionRing.transform.position=scout.transform.position-new Vector3(0,.61f,0);
            ghost.SetActive(Mode==MatchMode.Build&&hasHover);
            if(ghost.activeSelf)
            {
                bool valid=Match.CanBuild(hoverX,hoverZ,buildKind,out var reason);int w=Match.Balance.Buildings[buildKind].Width;
                ghost.transform.position=new Vector3(hoverX+w*.5f,Match.Map.Height(hoverX,hoverZ)+.025f,hoverZ+w*.5f);
                ghost.transform.localScale=new Vector3(w,.025f,w);ghostMaterial.SetColor("_BaseColor",valid?new Color(.1f,.7f,.3f):new Color(.9f,.1f,.15f));
                ModeHint=Match.Balance.Buildings[buildKind].Name+": "+reason+" · ПКМ / Esc — отмена";
                if(ghostKind!=buildKind)
                {
                    if(ghostModel!=null)Destroy(ghostModel);ghostModel=BuildingModel(buildKind);ghostKind=buildKind;
                    foreach(var r in ghostModel.GetComponentsInChildren<Renderer>()){r.sharedMaterial.SetFloat("_Opacity",.35f);r.sharedMaterial.SetFloat("_ZWrite",0);r.sharedMaterial.renderQueue=3000;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
                }
                ghostModel.SetActive(true);ghostModel.transform.position=ghost.transform.position;
            }
            else{if(ghostModel!=null)ghostModel.SetActive(false);ModeHint=Hint();}
            for(int i=0;i<pinViews.Count;i++)pinViews[i].transform.localScale=new Vector3(1,1+.08f*Mathf.Sin(Time.time*4+i),1);
            AnimateBuildings();
            refresh+=Time.deltaTime;if(refresh>.15f){refresh=0;RefreshWorld();}
        }
        float Option(string id,float fallback){var settings=services?.App?.Settings;return settings.HasValue?MenuCatalog.Value(settings.Value,MenuCatalog.Find(id)):fallback;}
        // "Скорость камеры" 0–100 maps to 0.4–1.6 of the base speed; 50 keeps the original pace.
        float CameraSpeed()=>.4f+Option("controls.cameraSpeed",50)/100f*1.2f;
        string Hint()=>Mode==MatchMode.Build?"Укажите место на карте · ПКМ / Esc — отмена":Mode==MatchMode.Move?"Движение: ЛКМ — точка на карте или миникарте · ПКМ / Esc — отмена"
            :Mode==MatchMode.Harvest?"Добыча: ЛКМ — разведанная залежь · ПКМ / Esc — отмена":Mode==MatchMode.Ping?"Метка: ЛКМ — место на карте или миникарте · Alt+ЛКМ — всегда":"";
        void UpdateCamera()
        {
            MapCamera.transform.rotation=Quaternion.Euler(53,0,0);
            MapCamera.transform.position=focus-MapCamera.transform.forward*(zoom/Mathf.Tan(MapCamera.fieldOfView*Mathf.Deg2Rad*.5f));
        }
        void AnimateBuildings()
        {
            for(int i=0;i<buildingViews.Count;i++)
            {
                var b=Match.Buildings[i];float h=Match.Balance.Buildings[b.Kind].Height;
                // The full-sized model rises through the opaque terrain; it never scales up.
                buildingViews[i].transform.position=new Vector3(b.X+b.Width*.5f,Match.Map.Height(b.X,b.Z)-h*(1-b.Ready),b.Z+b.Width*.5f);
            }
        }
        void RefreshWorld()
        {
            for(int z=0;z<64;z++)for(int x=0;x<64;x++)
            {
                int tile=z*64+x;float light=Match.Visible[x,z]?1:Match.Explored[x,z]?.28f:0;
                for(int face=0;face<5;face++)for(int v=0;v<4;v++)groundColors[tile*20+face*4+v]=tileColors[tile*5+face]*light;
            }
            ground.colors=groundColors;
            for(int i=0;i<depositViews.Count;i++)
            {
                var r=Match.Map.Resources[i];depositViews[i].SetActive(Match.Explored[r.X,r.Z]&&r.Remaining>0);
                depositViews[i].GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor",(r.Gas?new Color(.3f,.9f,.6f):new Color(.25f,.7f,1))*(Match.Visible[r.X,r.Z]?1:.28f));
            }
            while(buildingViews.Count<Match.Buildings.Count)
                buildingViews.Add(BuildingModel(Match.Buildings[buildingViews.Count].Kind));
            AnimateBuildings();
            RefreshMinimap();hud?.Refresh();
        }
        // Terrain, fog, deposits and buildings; the camera frame, worker, route and pins are vector overlays in the HUD.
        void RefreshMinimap()
        {
            var pixels=new Color[4096];var own=hud?.OwnColor??Color.white;
            for(int z=0;z<64;z++)for(int x=0;x<64;x++)
            {
                float lit=Match.Visible[x,z]?1:Match.Explored[x,z]?.28f:0;
                pixels[z*64+x]=(Match.Map.Ramp(x,z)?new Color(.65f,.5f,.25f):Match.Map.Height(x,z)>0?new Color(.35f,.55f,.43f):new Color(.3f,.34f,.26f))*lit;
            }
            foreach(var r in Match.Map.Resources)if(Match.Explored[r.X,r.Z]&&r.Remaining>0)pixels[r.Z*64+r.X]=(r.Gas?new Color(.3f,.9f,.6f):new Color(.35f,.75f,1))*(Match.Visible[r.X,r.Z]?1:.35f);
            foreach(var b in Match.Buildings)for(int z=b.Z;z<b.Z+b.Width;z++)for(int x=b.X;x<b.X+b.Width;x++)pixels[z*64+x]=own*(b.Ready>=1?1:.6f);
            miniTexture.SetPixels(pixels);miniTexture.Apply(false);
        }
        void BuildUI()
        {
            miniTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            hud=new MatchHud(ui,this,services);
        }
        public void SetMode(MatchMode mode,int kind=-1)
        {
            if(mode==MatchMode.Build&&(kind<0||kind>1))mode=MatchMode.None;
            if((mode==MatchMode.Move||mode==MatchMode.Harvest)&&!Match.Selected)SelectWorker();
            Mode=mode;buildKind=mode==MatchMode.Build?kind:-1;ModeHint=Hint();hud?.Refresh();
        }
        public void SelectWorker(){Match.Selected=true;selectedBuilding=-1;selectedDeposit=null;hud?.Refresh();}
        public void Focus(Vector2 point)=>focus=new Vector3(Mathf.Clamp(point.x,0,64),0,Mathf.Clamp(point.y,0,64));
        public void FocusWorker()=>focus=new Vector3(Match.X,0,Match.Z);
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
            }
            if(!Match.Selected)SelectWorker();
            Match.Move(point.x,point.y);SetMode(MatchMode.None);
        }
        public void AddPin(Vector2 point)
        {
            if(pins.Count>=9){pins.RemoveAt(0);Destroy(pinViews[0]);pinViews.RemoveAt(0);}
            pins.Add(point);
            var beacon=new GameObject("Pin");beacon.transform.SetParent(worldRoot.transform);
            var height=Match.HeightAt(Mathf.Clamp(point.x,0,63.9f),Mathf.Clamp(point.y,0,63.9f));
            beacon.transform.position=new Vector3(point.x,height,point.y);
            var color=new Color(1,.42f,.24f);
            var pillar=Primitive(PrimitiveType.Cylinder,color,Vector3.zero,new Vector3(.12f,1.6f,.12f));pillar.transform.SetParent(beacon.transform,false);pillar.transform.localPosition=new Vector3(0,1.6f,0);
            var ring=Primitive(PrimitiveType.Cylinder,color,Vector3.zero,new Vector3(1.4f,.02f,1.4f));ring.transform.SetParent(beacon.transform,false);ring.transform.localPosition=new Vector3(0,.04f,0);
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
            if(ground!=null){Destroy(ground);ground=null;}if(miniTexture!=null){Destroy(miniTexture);miniTexture=null;}
            foreach(var material in materials)if(material!=null)Destroy(material);
            materials.Clear();
            buildingViews.Clear();depositViews.Clear();tileColors.Clear();pinViews.Clear();pins.Clear();Match=null;
            ghostModel=null;ghostKind=-1;Mode=MatchMode.None;
        }
        void OnDestroy()=>Shutdown();
    }
}
