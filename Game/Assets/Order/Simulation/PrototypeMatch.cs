using System;
using System.Collections.Generic;
using Order.Content;
using Unity.Entities;

namespace Order.Simulation
{
    public sealed class PrototypeMatch
    {
        public readonly TempleMap Map = new TempleMap();
        public readonly bool[,] Visible = new bool[64,64], Explored = new bool[64,64];
        public readonly List<PrototypeBuilding> Buildings = new List<PrototypeBuilding>();
        public readonly MatchBalance Balance;
        public float X = 13.5f, Z = 13.5f;
        public int Minerals, Gas;
        public string Message = "Разведчик готов. ПКМ — движение / добыча.";
        public bool Selected = true;
        public readonly struct Waypoint { public readonly float X,Z; public Waypoint(float x,float z){X=x;Z=z;} }
        readonly Queue<Waypoint> route = new Queue<Waypoint>();
        public int WaypointCount=>route.Count;
        ResourceDeposit harvesting;
        float visionTimer, harvestTimer;
        public PrototypeMatch(MatchBalance balance=null)
        {
            Balance=balance??new MatchBalance();Minerals=Balance.StartingMinerals;Gas=Balance.StartingGas;
            foreach(var r in Map.Resources)r.Remaining=r.Gas?Balance.GasDeposit:Balance.MineralDeposit;
            Buildings.Add(new PrototypeBuilding { X=10, Z=10, Width=Balance.Buildings[2].Width, Ready=1, Kind=2 });UpdateVision();
        }
        public float HeightAt(float x,float z)=>Map.Height((int)x,(int)z,x-(int)x);
        public bool Occupied(int x, int z) => Map.ResourceAt(x,z)!=null || Buildings.Exists(b => x>=b.X && x<b.X+b.Width && z>=b.Z && z<b.Z+b.Width);
        public bool CanStep(int x,int z,int nx,int nz) => Map.Inside(nx,nz) && !Occupied(nx,nz) && Math.Abs(Map.Height(x,z)-Map.Height(nx,nz))<=.51f;
        public bool Move(int tx,int tz)=>Move(tx+.5f,tz+.5f);
        // Continuous destinations and straight segments. The cell graph is only an obstacle-search fallback.
        public bool CanTravel(float ax,float az,float bx,float bz)
        {
            float dx=bx-ax,dz=bz-az;int count=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(dx*dx+dz*dz)/.1));
            float previous=HeightAt(ax,az),radius=Balance.Worker.Radius;
            for(int i=0;i<=count;i++)
            {
                float x=ax+dx*i/count,z=az+dz*i/count;
                if(x<radius||z<radius||x>=64-radius||z>=64-radius)return false;
                float h=HeightAt(x,z);if(Math.Abs(h-previous)>.51f)return false;previous=h;
                for(int zz=(int)(z-radius);zz<=(int)(z+radius);zz++)for(int xx=(int)(x-radius);xx<=(int)(x+radius);xx++)
                {
                    float nearX=Math.Max(xx,Math.Min(x,xx+1)),nearZ=Math.Max(zz,Math.Min(z,zz+1));
                    if((x-nearX)*(x-nearX)+(z-nearZ)*(z-nearZ)>=radius*radius)continue;
                    if(Occupied(xx,zz)||Math.Abs(Map.Height(xx,zz,Math.Max(0,Math.Min(1,x-xx)))-h)>.51f)return false;
                }
            }
            return true;
        }
        public bool Move(float targetX,float targetZ)
        {
            if(float.IsNaN(targetX)||float.IsNaN(targetZ)||targetX<0||targetZ<0||targetX>=64||targetZ>=64)return false;
            int tx=(int)targetX,tz=(int)targetZ;
            harvesting=null;
            var deposit=Explored[tx,tz]?Map.ResourceAt(tx,tz):null;
            if(deposit==null&&CanTravel(X,Z,targetX,targetZ))
            {route.Clear();route.Enqueue(new Waypoint(targetX,targetZ));Message="Приказ: движение.";return true;}
            var previous=new int[4096]; Array.Fill(previous,-1);
            var open=new Queue<int>(); int start=(int)Z*64+(int)X,end=-1;
            open.Enqueue(start); previous[start]=start;
            int[] dx={1,-1,0,0},dz={0,0,1,-1};
            while(open.Count>0)
            {
                int p=open.Dequeue(),x=p%64,z=p/64;
                if ((deposit==null && x==tx && z==tz) || (deposit!=null && Math.Abs(x-tx)+Math.Abs(z-tz)==1)) {end=p;break;}
                for(int i=0;i<4;i++)
                {
                    int nx=x+dx[i],nz=z+dz[i]; if(!CanStep(x,z,nx,nz)||!CanTravel(x+.5f,z+.5f,nx+.5f,nz+.5f))continue;
                    int next=nz*64+nx;if(previous[next]>=0)continue;
                    previous[next]=p;open.Enqueue(next);
                }
            }
            if(end<0) {Message="Нет прохода: используйте рампу.";return false;}
            var reverse=new Stack<int>();for(int p=end;p!=start;p=previous[p])reverse.Push(p);
            var points=new List<Waypoint>{new Waypoint(X,Z)};
            while(reverse.Count>0){int p=reverse.Pop();points.Add(new Waypoint(p%64+.5f,p/64+.5f));}
            if(deposit==null)
            {
                var last=points[points.Count-1];
                if(!CanTravel(last.X,last.Z,targetX,targetZ)){Message="Недостаточно места для рабочего.";return false;}
                points.Add(new Waypoint(targetX,targetZ));
            }
            route.Clear();int anchor=0;
            while(anchor<points.Count-1)
            {
                int next=points.Count-1;
                while(next>anchor+1&&!CanTravel(points[anchor].X,points[anchor].Z,points[next].X,points[next].Z))next--;
                if(!CanTravel(points[anchor].X,points[anchor].Z,points[next].X,points[next].Z)){route.Clear();return false;}
                route.Enqueue(points[next]);anchor=next;
            }
            harvesting=deposit;Message=deposit==null?"Приказ: движение.":"Приказ: добыча ресурса.";return true;
        }
        public bool CanBuild(int x,int z,int kind,out string reason)
        {
            if(kind<0||kind>1){reason="Неизвестный тип здания";return false;}
            var stats=Balance.Buildings[kind];int w=stats.Width;float h=Map.Inside(x,z)?Map.Height(x,z):0;
            for(int zz=z;zz<z+w;zz++)for(int xx=x;xx<x+w;xx++)
            {
                if(!Map.Inside(xx,zz)){reason="За границей карты";return false;}
                if(!Visible[xx,zz]){reason="Нужна текущая видимость";return false;}
                if(Map.Ramp(xx,zz)||Map.Height(xx,zz)!=h||Occupied(xx,zz)){reason="Занято, рампа или перепад высот";return false;}
                float nx=Math.Max(xx,Math.Min(X,xx+1)),nz=Math.Max(zz,Math.Min(Z,zz+1));
                if((X-nx)*(X-nx)+(Z-nz)*(Z-nz)<Balance.Worker.Radius*Balance.Worker.Radius){reason="Здесь находится рабочий";return false;}
            }
            if(Minerals<stats.Minerals||Gas<stats.Gas){reason="Недостаточно ресурсов";return false;}
            reason="ЛКМ — построить";return true;
        }
        public bool Build(int x,int z,int kind)
        {
            if(!CanBuild(x,z,kind,out var reason)){Message=reason;return false;}
            var stats=Balance.Buildings[kind];Minerals-=stats.Minerals;Gas-=stats.Gas;
            Buildings.Add(new PrototypeBuilding{X=x,Z=z,Width=stats.Width,Kind=kind});
            route.Clear();harvesting=null;Message="Строительство начато.";UpdateVision();return true;
        }
        public void Tick(float dt)
        {
            foreach(var b in Buildings)b.Ready=Math.Min(1,b.Ready+dt/Balance.Buildings[b.Kind].BuildSeconds);
            float travel=dt*Balance.Worker.Speed;
            while(route.Count>0&&travel>0)
            {
                var p=route.Peek();float dx=p.X-X,dz=p.Z-Z,d=(float)Math.Sqrt(dx*dx+dz*dz);
                if(d<=travel){X=p.X;Z=p.Z;travel-=d;route.Dequeue();}
                else{X+=dx/d*travel;Z+=dz/d*travel;travel=0;}
            }
            if(route.Count==0&&harvesting!=null&&harvesting.Remaining>0)
            {
                harvestTimer+=dt;while(harvestTimer>=Balance.Worker.HarvestInterval&&harvesting.Remaining>0){harvestTimer-=Balance.Worker.HarvestInterval;int amount=Math.Min(Balance.Worker.HarvestAmount,harvesting.Remaining);harvesting.Remaining-=amount;if(harvesting.Gas)Gas+=amount;else Minerals+=amount;}
            }
            visionTimer+=dt;if(visionTimer>=.15f){visionTimer=0;UpdateVision();}
        }
        public void UpdateVision()
        {
            Array.Clear(Visible,0,Visible.Length);Reveal((int)X,(int)Z,Balance.Worker.Vision);
            foreach(var b in Buildings)if(b.Ready>=1)Reveal(b.X+b.Width/2,b.Z+b.Width/2,Balance.Buildings[b.Kind].Vision);
        }
        void Reveal(int sx,int sz,int radius)
        {
            float observer=Map.Height(sx,sz)+.6f;
            for(int z=Math.Max(0,sz-radius);z<=Math.Min(63,sz+radius);z++)
            for(int x=Math.Max(0,sx-radius);x<=Math.Min(63,sx+radius);x++)
            {
                int dx=x-sx,dz=z-sz;if(dx*dx+dz*dz>radius*radius)continue;
                int steps=Math.Max(Math.Abs(dx),Math.Abs(dz));bool clear=true;
                for(int i=1;i<=steps;i++){int xx=(int)Math.Round(sx+dx*(float)i/steps),zz=(int)Math.Round(sz+dz*(float)i/steps);if(Map.Height(xx,zz)>observer){clear=false;break;}}
                if(clear){Visible[x,z]=true;Explored[x,z]=true;}
            }
        }
    }
    public sealed class PrototypeBuilding {public int X,Z,Width,Kind;public float Ready;}
    public sealed class PrototypeMatchState : IComponentData {public PrototypeMatch Match;}
    public partial class PrototypeMatchSystem : SystemBase
    {
        EntityQuery matches;
        protected override void OnCreate() => matches=GetEntityQuery(ComponentType.ReadOnly<PrototypeMatchState>());
        protected override void OnUpdate()
        {
            float dt=SystemAPI.Time.DeltaTime;
            using var entities=matches.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach(var entity in entities)EntityManager.GetComponentObject<PrototypeMatchState>(entity).Match.Tick(dt);
        }
    }
}
