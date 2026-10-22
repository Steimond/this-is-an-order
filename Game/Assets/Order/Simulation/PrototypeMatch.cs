using System;
using System.Collections.Generic;
using Order.Content;
using Unity.Entities;

namespace Order.Simulation
{
    public readonly struct Waypoint { public readonly float X,Z; public Waypoint(float x,float z){X=x;Z=z;} }
    // Rules of the test match: economy, construction and vision. Unit movement lives in PrototypeMatch.Movement.cs.
    public sealed partial class PrototypeMatch
    {
        public readonly TempleMap Map = new TempleMap();
        public readonly bool[,] Visible = new bool[64,64], Explored = new bool[64,64];
        public readonly List<PrototypeBuilding> Buildings = new List<PrototypeBuilding>();
        public readonly List<PrototypeUnit> Units = new List<PrototypeUnit>();
        public readonly MatchBalance Balance;
        public int Minerals, Gas;
        public string Message = "Рабочий готов. ПКМ — движение / добыча.";
        int nextUnitId = 1;
        float visionTimer;
        public PrototypeMatch(MatchBalance balance=null)
        {
            Balance=balance??new MatchBalance();Minerals=Balance.StartingMinerals;Gas=Balance.StartingGas;
            foreach(var r in Map.Resources)r.Remaining=r.Gas?Balance.GasDeposit:Balance.MineralDeposit;
            Buildings.Add(new PrototypeBuilding { X=10, Z=10, Width=Balance.Buildings[2].Width, Ready=1, Kind=2 });
            Spawn(UnitKind.Worker,13.5f,13.5f);UpdateVision();
        }
        public PrototypeUnit Spawn(UnitKind kind,float x,float z)
        {
            var unit=new PrototypeUnit(nextUnitId++,kind,Balance.Unit(kind)){X=x,Z=z};
            Units.Add(unit);return unit;
        }
        // Test-ground setup: a mixed squad west of the command node for selection and movement checks; with the worker it fills the starting supply.
        public void AddTestSquad()
        {
            Spawn(UnitKind.Melee,8,15.5f);Spawn(UnitKind.Melee,9.2f,15.5f);
            for(int i=0;i<3;i++)Spawn(UnitKind.Ranged,7.4f+i*1.1f,17);
            Spawn(UnitKind.Air,8.5f,18.6f);UpdateVision();
            Message="Отряд на позиции: штурмовики, стрелки и винтокрыл.";
        }
        public int SupplyUsed{get{int used=0;foreach(var u in Units)used+=u.Stats.Supply;return used;}}
        public int SupplyLimit{get{int limit=0;foreach(var b in Buildings)if(b.Ready>=1)limit+=Balance.Buildings[b.Kind].Supply;return limit;}}
        public float HeightAt(float x,float z)=>Map.Height((int)x,(int)z,x-(int)x);
        public bool CanBuild(int x,int z,int kind,out string reason)
        {
            if(kind<0||kind>1){reason="Неизвестный тип здания";return false;}
            var stats=Balance.Buildings[kind];int w=stats.Width;float h=Map.Inside(x,z)?Map.Height(x,z):0;
            for(int zz=z;zz<z+w;zz++)for(int xx=x;xx<x+w;xx++)
            {
                if(!Map.Inside(xx,zz)){reason="За границей карты";return false;}
                if(!Visible[xx,zz]){reason="Нужна текущая видимость";return false;}
                if(Map.Ramp(xx,zz)||Map.Height(xx,zz)!=h||Occupied(xx,zz)){reason="Занято, рампа или перепад высот";return false;}
                foreach(var u in Units)
                {
                    if(u.Stats.Flying)continue;
                    float nx=Math.Max(xx,Math.Min(u.X,xx+1)),nz=Math.Max(zz,Math.Min(u.Z,zz+1));
                    if((u.X-nx)*(u.X-nx)+(u.Z-nz)*(u.Z-nz)<u.Stats.Radius*u.Stats.Radius){reason="Здесь стоит юнит";return false;}
                }
            }
            if(Minerals<stats.Minerals||Gas<stats.Gas){reason="Недостаточно ресурсов";return false;}
            reason="ЛКМ — построить";return true;
        }
        public bool Build(int x,int z,int kind)
        {
            if(!CanBuild(x,z,kind,out var reason)){Message=reason;return false;}
            var stats=Balance.Buildings[kind];Minerals-=stats.Minerals;Gas-=stats.Gas;
            Buildings.Add(new PrototypeBuilding{X=x,Z=z,Width=stats.Width,Kind=kind});
            navigationVersion++;RouteAroundNewBuildings();
            Message="Строительство начато.";UpdateVision();return true;
        }
        public void Tick(float dt)
        {
            foreach(var b in Buildings)b.Ready=Math.Min(1,b.Ready+dt/Balance.Buildings[b.Kind].BuildSeconds);
            // Movement runs in fixed steps, so a long frame and many short ones end in the same place.
            for(float left=dt;left>1e-6f;left-=Step)StepUnits(Math.Min(Step,left));
            foreach(var u in Units)
            {
                var target=u.HarvestTarget;
                if(u.route.Count>0||target==null||target.Remaining<=0)continue;
                u.harvestTimer+=dt;
                while(u.harvestTimer>=u.Stats.HarvestInterval&&target.Remaining>0)
                {
                    u.harvestTimer-=u.Stats.HarvestInterval;int amount=Math.Min(u.Stats.HarvestAmount,target.Remaining);target.Remaining-=amount;
                    if(target.Gas)Gas+=amount;else Minerals+=amount;
                    // A depleted deposit no longer blocks movement.
                    if(target.Remaining==0)navigationVersion++;
                }
            }
            visionTimer+=dt;if(visionTimer>=.15f){visionTimer=0;UpdateVision();}
        }
        public void UpdateVision()
        {
            Array.Clear(Visible,0,Visible.Length);
            foreach(var u in Units)Reveal((int)u.X,(int)u.Z,u.Stats.Vision,u.Stats.Flying);
            foreach(var b in Buildings)if(b.Ready>=1)Reveal(b.X+b.Width/2,b.Z+b.Width/2,Balance.Buildings[b.Kind].Vision,false);
        }
        // Ground observers cannot see up a cliff; air units see everything within their radius.
        void Reveal(int sx,int sz,int radius,bool fromAir)
        {
            float observer=fromAir?float.MaxValue:Map.Height(sx,sz)+.6f;
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
    public sealed class PrototypeUnit
    {
        public readonly int Id;
        public readonly UnitKind Kind;
        public readonly UnitStats Stats;
        public float X, Z;
        public int Health;
        public ResourceDeposit HarvestTarget { get; internal set; }
        internal readonly Queue<Waypoint> route = new Queue<Waypoint>();
        internal float harvestTimer;
        // Movement state: the order's destination, where the last order ended (for group arrival) and progress tracking.
        internal float goalX, goalZ, arrivedX, arrivedZ, levelY, bestDistance, stuckTime, startX, startZ, lastDx, lastDz;
        internal bool hasGoal, arrived, touchedArrived, returning;
        internal int replans;
        // Where the unit came to rest after its last order, and how long since a passing unit last shoved it from there.
        internal float restX, restZ, calmTime;
        internal int returns;
        public bool Moving=>route.Count>0;
        public int WaypointCount=>route.Count;
        public IEnumerable<Waypoint> Route=>route;
        internal PrototypeUnit(int id,UnitKind kind,UnitStats stats){Id=id;Kind=kind;Stats=stats;Health=stats.Health;}
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
