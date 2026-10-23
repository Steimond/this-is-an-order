using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // Construction of the human side, the way Terran workers build in StarCraft:
    // - a worker ordered to build walks to the site; the building is placed and paid for when the worker gets there (the
    //   cost is checked when the order is given and again on arrival);
    // - own idle units standing on the site step off it and the worker waits for them a few seconds; enemies block it;
    // - the building rises only while its worker stands by it, one worker per building. A worker given another order or
    //   killed leaves the building waiting, and any worker of the side can resume it;
    // - a building under construction can be cancelled for three quarters of its cost.
    public sealed partial class PrototypeMatch
    {
        // How close to the footprint a worker builds; the view uses it for the welding beam too.
        public const float BuildReach = 1;
        const float SiteWait = 3, CancelRefund = .75f;

        // Whether the site can take the building now. The player builds only where it sees; the bot builds anywhere on its
        // own ground. Own units do not block a site (they step off it when the building is placed), visible enemies do.
        public bool CanBuild(int x,int z,int kind,out string reason,int owner=Player)
        {
            if(!SiteFits(x,z,kind,owner,owner==Player,out reason))return false;
            int w=Balance.Buildings[kind].Width;
            foreach(var u in Units)if(u.Owner!=owner&&SeenBy(owner,u)&&On(u,x,z,w)){reason="Здесь стоит юнит противника";return false;}
            reason="ЛКМ — построить";return true;
        }
        // Terrain, other buildings, deposits and the cost; units are checked separately.
        bool SiteFits(int x,int z,int kind,int owner,bool sight,out string reason)
        {
            if(kind<0||kind>1){reason="Неизвестный тип здания";return false;}
            var stats=Balance.Buildings[kind];int w=stats.Width;float h=Map.Inside(x,z)?Map.Height(x,z):0;
            for(int zz=z;zz<z+w;zz++)for(int xx=x;xx<x+w;xx++)
            {
                if(!Map.Inside(xx,zz)){reason="За границей карты";return false;}
                if(sight&&!Visible[xx,zz]){reason="Нужна текущая видимость";return false;}
                if(Map.Ramp(xx,zz)||Map.Height(xx,zz)!=h||Occupied(xx,zz)){reason="Занято, рампа или перепад высот";return false;}
            }
            if(metalStock[owner]<stats.Metal){reason="Недостаточно металла";return false;}
            if(plasmaStock[owner]<stats.Plasma){reason="Недостаточно плазмы";return false;}
            reason=null;return true;
        }
        static bool On(PrototypeUnit u,int x,int z,int w)
        {
            if(u.Stats.Flying)return false;
            float nx=Math.Max(x,Math.Min(u.X,x+w)),nz=Math.Max(z,Math.Min(u.Z,z+w));
            return (u.X-nx)*(u.X-nx)+(u.Z-nz)*(u.Z-nz)<u.Stats.Radius*u.Stats.Radius;
        }

        // The worker goes to build the building with its corner at (x, z).
        public bool Build(PrototypeUnit worker,int x,int z,int kind)
        {
            if(worker==null||!worker.Alive||worker.Kind!=UnitKind.Worker){Message="Строить может только рабочий.";return false;}
            int owner=worker.Owner;
            if(!CanBuild(x,z,kind,out var reason,owner)){if(owner==Player)Message=reason;return false;}
            var stats=Balance.Buildings[kind];
            if(!SiteSpot(worker,x,z,stats.Width,out float sx,out float sz)){if(owner==Player)Message="К месту стройки не подойти";return false;}
            var message=Message;
            GiveOrder(worker,UnitOrder.Move);PlanPath(worker,sx,sz);
            worker.SiteKind=kind;worker.SiteX=x;worker.SiteZ=z;worker.siteTimer=0;
            Message=owner==Player?$"{worker.Stats.Name}: идёт строить — {stats.Name}.":message;
            return true;
        }
        // The worker goes to an unfinished building of its side that nobody is building and carries on with it.
        public bool Resume(PrototypeUnit worker,PrototypeBuilding building)
        {
            float sx=0,sz=0;
            string reason=worker==null||!worker.Alive||worker.Kind!=UnitKind.Worker?"Строить может только рабочий."
                :building==null||building.Health<=0||building.Ready>=1||building.Owner!=worker.Owner?"Это здание не строится."
                :building.Builder!=null&&building.Builder!=worker?"Здание уже строит другой рабочий."
                :!SiteSpot(worker,building.X,building.Z,building.Width,out sx,out sz)?"К зданию не подойти.":null;
            if(reason!=null){if(worker==null||worker.Owner==Player)Message=reason;return false;}
            var message=Message;
            GiveOrder(worker,UnitOrder.Move);PlanPath(worker,sx,sz);
            worker.Constructing=building;building.builder=worker;worker.siteTimer=0;
            Message=worker.Owner==Player?$"{worker.Stats.Name}: продолжает стройку — {Balance.Buildings[building.Kind].Name}.":message;
            return true;
        }
        // Returns three quarters of the cost; the worker is free.
        public bool CancelConstruction(PrototypeBuilding building)
        {
            if(building==null||building.Ready>=1||building.Health<=0||!Buildings.Contains(building))return false;
            var stats=Balance.Buildings[building.Kind];
            int metal=(int)(stats.Metal*CancelRefund),plasma=(int)(stats.Plasma*CancelRefund);
            metalStock[building.Owner]+=metal;plasmaStock[building.Owner]+=plasma;
            var builder=building.Builder;if(builder!=null)builder.Constructing=null;
            building.builder=null;Buildings.Remove(building);NavigationChanged(building.X,building.Z,building.Width);
            if(building.Owner==Player)Message=$"Стройка отменена: {stats.Name}. Возвращено: металл {metal}"+(plasma>0?$", плазма {plasma}.":".");
            return true;
        }
        static bool Busy(PrototypeUnit u)=>u.Constructing!=null||u.SiteKind>=0;

        // Buildings rise while their worker is by them.
        void TickBuildings(float dt)
        {
            foreach(var b in Buildings)
            {
                if(b.Ready>=1)continue;
                var builder=b.Builder;
                if(builder==null||Gap(builder,b)>BuildReach)continue;
                // A building under construction gains its health as it rises, from a tenth of the full value: by whole points
                // of the progress made, so short frames do not round it up faster than the building rises. Damage taken
                // on the way stays.
                var stats=Balance.Buildings[b.Kind];float rate=FastBuild&&b.Owner==Player?FastFactor:1,step=Math.Min(dt*rate/stats.BuildSeconds,1-b.Ready);
                float before=b.Ready;
                b.Ready=Math.Min(1,b.Ready+step);
                if(1-b.Ready<1e-5f)b.Ready=1;
                b.Health=Math.Min(stats.Health,b.Health+(int)Math.Round(stats.Health*.9f*b.Ready)-(int)Math.Round(stats.Health*.9f*before));
                if(b.Ready<1)continue;
                builder.Constructing=null;b.builder=null;
                if(b.Owner==Player)Message=$"Построено: {stats.Name}.";
            }
        }
        // Workers on their way to a site place the building on arrival; builders pushed away from theirs walk back.
        void TickConstruction(float dt)
        {
            foreach(var u in Units)
            {
                if(!u.Alive)continue;
                var b=u.Constructing;
                if(b!=null)
                {
                    if(b.Health<=0||b.Ready>=1||!Buildings.Contains(b)){u.Constructing=null;continue;}
                    if(u.Moving||Gap(u,b)<=BuildReach||(u.siteTimer+=dt)<1)continue;
                    u.siteTimer=0;
                    if(SiteSpot(u,b.X,b.Z,b.Width,out float bx,out float bz)){var message=Message;PlanPath(u,bx,bz);Message=message;}
                    continue;
                }
                if(u.SiteKind<0||u.Moving)continue;
                var stats=Balance.Buildings[u.SiteKind];int x=u.SiteX,z=u.SiteZ,w=stats.Width;
                if(SiteGap(u,x,z,w)>BuildReach){EndSite(u,"Не дойти до места стройки.");continue;}
                if(!SiteFits(x,z,u.SiteKind,u.Owner,false,out var reason)){EndSite(u,reason+".");continue;}
                bool blocked=false;
                foreach(var v in Units)
                {
                    if(v==u||!v.Alive||!On(v,x,z,w))continue;
                    blocked=true;
                    if(v.Owner==u.Owner&&v.Order==UnitOrder.Idle&&!v.Moving&&v.HarvestTarget==null&&!Busy(v)&&SiteSpot(v,x,z,w,out float vx,out float vz))
                    {var message=Message;PlanPath(v,vx,vz);Message=message;}
                }
                if(blocked){if((u.siteTimer+=dt)>SiteWait)EndSite(u,"Место стройки занято.");continue;}
                metalStock[u.Owner]-=stats.Metal;plasmaStock[u.Owner]-=stats.Plasma;
                var building=AddBuilding(u.SiteKind,x,z,u.Owner,0);
                RouteAroundNewBuildings();
                u.SiteKind=-1;u.Constructing=building;building.builder=u;u.siteTimer=0;
                if(u.Owner==Player)Message=$"Строительство начато: {stats.Name}.";
            }
        }
        void EndSite(PrototypeUnit u,string reason){u.SiteKind=-1;u.siteTimer=0;if(u.Owner==Player)Message=reason;}
        static float SiteGap(PrototypeUnit u,int x,int z,int w)
        {
            float nx=Math.Max(x,Math.Min(u.X,x+w)),nz=Math.Max(z,Math.Min(u.Z,z+w));
            return (float)Math.Sqrt((u.X-nx)*(u.X-nx)+(u.Z-nz)*(u.Z-nz))-u.Stats.Radius;
        }
        // The point just outside the footprint nearest the unit where it can stand, on the footprint's level.
        bool SiteSpot(PrototypeUnit u,int x,int z,int w,out float sx,out float sz)
        {
            float r=u.Stats.Radius,m=r+.05f,x0=x-m,x1=x+w+m,z0=z-m,z1=z+w+m,level=Map.Inside(x,z)?Map.Height(x,z):0,best=float.MaxValue;
            float bestX=0,bestZ=0;
            void Try(float px,float pz)
            {
                if(!InMap(px,pz)||Math.Abs(HeightAt(px,pz)-level)>.01f||!CanTravel(px,pz,px,pz,r))return;
                float d=(px-u.X)*(px-u.X)+(pz-u.Z)*(pz-u.Z);
                if(d<best){best=d;bestX=px;bestZ=pz;}
            }
            int count=(int)Math.Ceiling((w+2*m)/.25f);
            for(int i=0;i<=count;i++)
            {
                float t=(float)i/count,px=x0+(x1-x0)*t,pz=z0+(z1-z0)*t;
                Try(px,z0);Try(px,z1);Try(x0,pz);Try(x1,pz);
            }
            sx=bestX;sz=bestZ;
            return best<float.MaxValue;
        }
    }
}
