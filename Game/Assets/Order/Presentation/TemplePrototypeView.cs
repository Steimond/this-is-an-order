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
    public sealed class TemplePrototypeView : MonoBehaviour
    {
        public PrototypeMatch Match { get; private set; }
        public Camera MapCamera { get; private set; }
        VisualElement ui;
        Label resources, selection, status, placement;
        Image minimap;
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

        public void Initialize(VisualElement root, Action exit)
        {
            if(Match!=null)return;
            enabled=true;buildKind=-1;selectedBuilding=-1;selectedDeposit=null;focus=new Vector3(13,0,13);zoom=18;refresh=0;
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
            if(keys!=null)
            {
                float x=(keys.dKey.isPressed||keys.rightArrowKey.isPressed?1:0)-(keys.aKey.isPressed||keys.leftArrowKey.isPressed?1:0);
                float z=(keys.wKey.isPressed||keys.upArrowKey.isPressed?1:0)-(keys.sKey.isPressed||keys.downArrowKey.isPressed?1:0);
                focus+=new Vector3(x,0,z)*Time.deltaTime*zoom;
                if(keys.spaceKey.wasPressedThisFrame)focus=new Vector3(Match.X,0,Match.Z);
                if(keys.bKey.wasPressedThisFrame)buildKind=0;
                if(keys.escapeKey.wasPressedThisFrame)buildKind=-1;
            }
            if(mouse!=null)
            {
                Vector2 p=mouse.position.ReadValue();
                Vector2 panelPoint=RuntimePanelUtils.ScreenToPanel(ui.panel,new Vector2(p.x,Screen.height-p.y));
                overUI=panelPoint.y<56||panelPoint.y>ui.resolvedStyle.height-184;
                if(!overUI)zoom=Mathf.Clamp(zoom-mouse.scroll.ReadValue().y*.015f,10,38);
                hasHover=!overUI&&Physics.Raycast(MapCamera.ScreenPointToRay(p),out var hit,180);
                if(hasHover)
                {
                    // Repeat ray explicitly to keep the hit definitely assigned on all compiler versions.
                    Physics.Raycast(MapCamera.ScreenPointToRay(p),out hit,180);
                    hoverX=Mathf.FloorToInt(hit.point.x);hoverZ=Mathf.FloorToInt(hit.point.z);
                    if(mouse.rightButton.wasPressedThisFrame){if(buildKind>=0)buildKind=-1;else if(Match.Selected)Match.Move(hit.point.x,hit.point.z);}
                    if(mouse.leftButton.wasPressedThisFrame)
                    {
                        if(buildKind>=0){if(Match.Build(hoverX,hoverZ,buildKind))buildKind=-1;}
                        else
                        {
                            Match.Selected=Vector2.Distance(new Vector2(hit.point.x,hit.point.z),new Vector2(Match.X,Match.Z))<1.5f;
                            selectedBuilding=Match.Buildings.FindIndex(b=>hoverX>=b.X&&hoverX<b.X+b.Width&&hoverZ>=b.Z&&hoverZ<b.Z+b.Width);
                            selectedDeposit=Match.Map.Inside(hoverX,hoverZ)&&Match.Explored[hoverX,hoverZ]?Match.Map.ResourceAt(hoverX,hoverZ):null;
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
            ghost.SetActive(buildKind>=0&&hasHover);
            if(ghost.activeSelf)
            {
                bool valid=Match.CanBuild(hoverX,hoverZ,buildKind,out var reason);int w=Match.Balance.Buildings[buildKind].Width;
                ghost.transform.position=new Vector3(hoverX+w*.5f,Match.Map.Height(hoverX,hoverZ)+.025f,hoverZ+w*.5f);
                ghost.transform.localScale=new Vector3(w,.025f,w);ghostMaterial.SetColor("_BaseColor",valid?new Color(.1f,.7f,.3f):new Color(.9f,.1f,.15f));placement.text=reason;
                if(ghostKind!=buildKind)
                {
                    if(ghostModel!=null)Destroy(ghostModel);ghostModel=BuildingModel(buildKind);ghostKind=buildKind;
                    foreach(var r in ghostModel.GetComponentsInChildren<Renderer>()){r.sharedMaterial.SetFloat("_Opacity",.35f);r.sharedMaterial.SetFloat("_ZWrite",0);r.sharedMaterial.renderQueue=3000;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
                }
                ghostModel.SetActive(true);ghostModel.transform.position=ghost.transform.position;
            }
            else {if(ghostModel!=null)ghostModel.SetActive(false);placement.text=buildKind>=0?"Укажите место на карте. Esc / ПКМ — отмена":"ЛКМ — выбрать рабочего · ПКМ — приказ · B — строительство";}
            AnimateBuildings();
            refresh+=Time.deltaTime;if(refresh>.15f){refresh=0;RefreshWorld();}
        }
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
            int supply=0;foreach(var b in Match.Buildings)if(b.Ready>=1)supply+=Match.Balance.Buildings[b.Kind].Supply;
            resources.text=$"◆ {Match.Minerals}     ◉ {Match.Gas}      Рабочие 1 / {supply}     |     TEMPLE BASIN · ТЕСТ";
            var worker=Match.Balance.Worker;
            selection.text=Match.Selected?$"{worker.Name}\nЗдоровье {worker.Health} / {worker.Health} · Обзор {worker.Vision}\nСкорость {worker.Speed:0.#} · Добыча {worker.HarvestAmount} / {worker.HarvestInterval:0.#} с":"Нет выделения\nВыберите рабочего на карте";
            if(!Match.Selected&&selectedBuilding>=0)
            {
                var b=Match.Buildings[selectedBuilding];var stats=Match.Balance.Buildings[b.Kind];selection.text=stats.Name+$"\nРазмер {b.Width} × {b.Width} · Прочность {stats.Health}\n"+(b.Ready<1?$"Строительство {b.Ready*100:0}% · осталось {(1-b.Ready)*stats.BuildSeconds:0.0} с":"Готово");
            }
            else if(!Match.Selected&&selectedDeposit!=null)selection.text=(selectedDeposit.Gas?"ГАЗ · ПЛЕЙСХОЛДЕР":"МИНЕРАЛЫ · ПЛЕЙСХОЛДЕР")+$"\nОсталось: {selectedDeposit.Remaining}";
            status.text=Match.Message;RefreshMinimap();
        }
        void RefreshMinimap()
        {
            var pixels=new Color[4096];
            for(int z=0;z<64;z++)for(int x=0;x<64;x++)
            {
                float lit=Match.Visible[x,z]?1:Match.Explored[x,z]?.28f:0;
                pixels[z*64+x]=(Match.Map.Ramp(x,z)?new Color(.65f,.5f,.25f):Match.Map.Height(x,z)>0?new Color(.35f,.55f,.43f):new Color(.3f,.34f,.26f))*lit;
            }
            foreach(var r in Match.Map.Resources)if(Match.Explored[r.X,r.Z]&&r.Remaining>0)pixels[r.Z*64+r.X]=(r.Gas?Color.green:Color.cyan)*(Match.Visible[r.X,r.Z]?1:.3f);
            foreach(var b in Match.Buildings)for(int z=b.Z;z<b.Z+b.Width;z++)for(int x=b.X;x<b.X+b.Width;x++)pixels[z*64+x]=Color.white;
            pixels[(int)Match.Z*64+(int)Match.X]=Color.yellow;
            // Project the four viewport corners onto the ground plane for the minimap frame.
            var points=new Vector3[4];Vector2[] corners={new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
            var plane=new Plane(Vector3.up,Vector3.zero);
            for(int i=0;i<4;i++){var ray=MapCamera.ViewportPointToRay(corners[i]);if(plane.Raycast(ray,out float d))points[i]=ray.GetPoint(d);}
            for(int i=0;i<4;i++)for(int j=0;j<=150;j++)
            {var p=Vector3.Lerp(points[i],points[(i+1)%4],j/150f);int x=(int)p.x,z=(int)p.z;if(x>=0&&x<64&&z>=0&&z<64)pixels[z*64+x]=new Color(.95f,.85f,.5f);}
            miniTexture.SetPixels(pixels);miniTexture.Apply(false);
        }
        VisualElement Panel(VisualElement parent)
        {
            var panel=new VisualElement();panel.style.backgroundColor=new Color(.035f,.065f,.075f,.97f);panel.style.paddingLeft=14;panel.style.paddingRight=14;panel.style.paddingTop=10;panel.style.paddingBottom=10;
            panel.style.borderTopWidth=1;panel.style.borderTopColor=new Color(.35f,.49f,.46f);parent.Add(panel);return panel;
        }
        Button Button(VisualElement parent,string text,Action action)
        {
            var button=new Button(action){text=text};button.style.height=30;button.style.marginTop=4;
            button.style.backgroundColor=new Color(.12f,.22f,.23f);button.style.color=new Color(.88f,.94f,.9f);button.style.fontSize=12;
            parent.Add(button);return button;
        }
        void BuildUI()
        {
            ui.Clear();ui.styleSheets.Clear();ui.style.color=new Color(.85f,.91f,.87f);ui.style.fontSize=14;
            var top=Panel(ui);top.style.position=Position.Absolute;top.style.top=0;top.style.left=0;top.style.right=0;top.style.height=56;top.style.flexDirection=FlexDirection.Row;top.style.justifyContent=Justify.SpaceBetween;
            resources=new Label();top.Add(resources);Button(top,"В меню",()=>onExit?.Invoke());
            var bottom=Panel(ui);bottom.style.position=Position.Absolute;bottom.style.bottom=0;bottom.style.left=0;bottom.style.right=0;bottom.style.height=184;bottom.style.flexDirection=FlexDirection.Row;
            miniTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            minimap=new Image{image=miniTexture,scaleMode=ScaleMode.StretchToFill};minimap.style.width=160;minimap.style.height=160;minimap.style.flexShrink=0;bottom.Add(minimap);
            minimap.RegisterCallback<PointerDownEvent>(evt=>
            {
                Vector2 p=evt.localPosition;float x=Mathf.Clamp(p.x/160*64,.5f,63.5f),z=Mathf.Clamp((1-p.y/160)*64,.5f,63.5f);
                if(evt.button==1&&Match.Selected)Match.Move(x,z);else focus=new Vector3(x,0,z);evt.StopPropagation();
            });
            var info=Panel(bottom);info.style.flexGrow=1;selection=new Label();selection.style.whiteSpace=WhiteSpace.Normal;info.Add(selection);
            status=new Label();status.style.marginTop=12;info.Add(status);
            placement=new Label();placement.style.whiteSpace=WhiteSpace.Normal;placement.style.marginTop=10;info.Add(placement);
            var commands=Panel(bottom);commands.style.width=275;
            commands.style.paddingTop=0;commands.style.paddingBottom=0;
            commands.Add(new Label("СТРОИТЕЛЬСТВО · сетка 1 × 1"));
            for(int i=0;i<2;i++){int kind=i;var b=Match.Balance.Buildings[i];Button(commands,$"{(i==0?"[B] ":"")}{b.Name} {b.Width}×{b.Width} · {b.Minerals}◆ {b.Gas}◉ · {b.BuildSeconds:0.#}с",()=>buildKind=kind);}
            Button(commands,"К рабочему [Space]",()=>focus=new Vector3(Match.X,0,Match.Z));
            commands.Add(new Label("WASD / стрелки · колесо — масштаб"));
        }
        public void Shutdown()
        {
            enabled=false;
            if(ecsWorld!=null&&ecsWorld.IsCreated&&ecsWorld.EntityManager.Exists(matchEntity))ecsWorld.EntityManager.DestroyEntity(matchEntity);
            if(worldRoot!=null){worldRoot.SetActive(false);Destroy(worldRoot);worldRoot=null;}
            if(ground!=null){Destroy(ground);ground=null;}if(miniTexture!=null){Destroy(miniTexture);miniTexture=null;}
            foreach(var material in materials)if(material!=null)Destroy(material);
            materials.Clear();
            buildingViews.Clear();depositViews.Clear();tileColors.Clear();Match=null;
            ghostModel=null;ghostKind=-1;
        }
        void OnDestroy()=>Shutdown();
    }
}
