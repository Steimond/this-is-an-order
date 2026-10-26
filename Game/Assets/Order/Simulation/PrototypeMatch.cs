using System;
using System.Collections.Generic;
using Order.Content;
using Order.Contracts;

namespace Order.Simulation
{
    public readonly struct Waypoint { public readonly Fix X,Z; public Waypoint(Fix x,Fix z){X=x;Z=z;} }
    // Rules of the match: sides, economy and vision. Construction lives in PrototypeMatch.Construction.cs, unit movement in
    // PrototypeMatch.Movement.cs, attacks in PrototypeMatch.Combat.cs. A match has two to MaxSides sides, every one an
    // enemy of every other. Side 0 is the player the messages, alerts and cheats are for; on the test ground side 1 is
    // its opponent.
    public sealed partial class PrototypeMatch
    {
        public const int Player = 0, Enemy = 1, MaxSides = 8;
        public readonly TempleMap Map = new TempleMap();
        // What each side sees and has seen. Air units are seen anywhere within an observer's radius, ground units only
        // where the observer has a line of sight. Visible and Explored are the player's.
        readonly bool[][,] ground, sky, explored;
        public bool[,] Visible=>ground[Player];
        public bool[,] Explored=>explored[Player];
        public bool[,] ExploredBy(int side)=>explored[side];
        public readonly List<PrototypeBuilding> Buildings = new List<PrototypeBuilding>();
        public readonly List<PrototypeUnit> Units = new List<PrototypeUnit>();
        public readonly MatchBalance Balance;
        // Resources of each side; Metal and Plasma are the player's.
        readonly int[] metalStock,plasmaStock;
        public int Metal{get=>metalStock[Player];set=>metalStock[Player]=value;}
        public int Plasma{get=>plasmaStock[Player];set=>plasmaStock[Player]=value;}
        public int MetalOf(int side)=>metalStock[side];
        public int PlasmaOf(int side)=>plasmaStock[side];
        // Seconds of play since the match began.
        public Fix Clock{get;private set;}
        public string Message = "Рабочий готов. ПКМ — движение / добыча.";
        int nextUnitId = 1;
        Fix visionTimer;
        // The test ground: the player's command node and worker on the south-west plateau, an opponent with nothing yet
        // (AddEnemyBase, AddEnemySquad and StartBot give it a base, a squad and a mind).
        public PrototypeMatch(MatchBalance balance=null,FactionId player=FactionId.Expedition,FactionId enemy=FactionId.Expedition):this(balance,new[]{player,enemy})
        {
            AddBuilding(2,10,10,Player,1);
            Spawn(UnitKind.Worker,(Fix)13.5f,(Fix)13.5f);UpdateVision();
        }
        // An empty match of these sides, one faction each: AddStart seats them on the map.
        public PrototypeMatch(MatchBalance balance,IReadOnlyList<FactionId> sides)
        {
            if(sides==null||sides.Count<2||sides.Count>MaxSides)throw new ArgumentException($"A match has 2 to {MaxSides} sides.",nameof(sides));
            Balance=balance??new MatchBalance();
            int count=sides.Count;
            factions=new FactionId[count];for(int side=0;side<count;side++)factions[side]=sides[side];
            metalStock=new int[count];plasmaStock=new int[count];supplyCap=new int[count];defeated=new bool[count];levels=new int[count,2];
            ground=new bool[count][,];sky=new bool[count][,];explored=new bool[count][,];
            for(int side=0;side<count;side++)
            {
                metalStock[side]=Balance.StartingMetal;plasmaStock[side]=Balance.StartingPlasma;supplyCap[side]=Balance.SupplyCap;
                ground[side]=new bool[64,64];sky[side]=new bool[64,64];explored[side]=new bool[64,64];
            }
            foreach(var r in Map.Resources)r.Remaining=r.Plasma?Balance.PlasmaDeposit:Balance.MetalDeposit;
        }
        public int Sides=>factions.Length;
        // A side's start: a finished main building on one of the map's start spots (TempleMap.Bases, the first four) and
        // workers round it.
        public PrototypeBuilding AddStart(int side,int seat,int workers=4)
        {
            var (x,z)=Map.Bases[seat];
            var main=AddBuilding(FactionBalance.Main,x,z,side,1);
            SpawnAround(UnitKind.Worker,workers,main.X+main.Width*(Fix).5f,main.Z<32?main.Z+main.Width+(Fix)1.5f:main.Z-(Fix)1.5f,side);
            return main;
        }
        // Each side plays one faction: its roster, how its workers gather and how it builds.
        readonly FactionId[] factions;
        public FactionId FactionOf(int side)=>factions[side];
        public FactionBalance Roster(int side)=>Balance.Of(factions[side]);
        public PrototypeUnit Spawn(UnitKind kind,Fix x,Fix z,int owner=Player)
        {
            var unit=new PrototypeUnit(nextUnitId++,kind,Roster(owner).Unit(kind),owner){X=x,Z=z,startX=x,startZ=z};
            Units.Add(unit);return unit;
        }
        // Test-ground setup: a mixed squad west of the command node for selection and movement checks; with the worker it fills the starting supply.
        public void AddTestSquad()
        {
            Spawn(UnitKind.Melee,8,(Fix)15.5f);Spawn(UnitKind.Melee,(Fix)9.2f,(Fix)15.5f);
            for(int i=0;i<3;i++)Spawn(UnitKind.Ranged,(Fix)7.4f+i*(Fix)1.1f,17);
            Spawn(UnitKind.Air,(Fix)8.5f,(Fix)18.6f);UpdateVision();
            Message="Отряд на позиции.";
        }
        // Test-ground enemy: a squad in the basin north-east of the centre. It stands still and only answers attacks.
        public void AddEnemySquad()
        {
            Spawn(UnitKind.Melee,33,40,Enemy);Spawn(UnitKind.Melee,35,40,Enemy);
            for(int i=0;i<3;i++)Spawn(UnitKind.Ranged,(Fix)32.8f+i*(Fix)1.2f,(Fix)41.6f,Enemy);
            Spawn(UnitKind.Air,34,43,Enemy);UpdateVision();
        }
        // Units in training count from the moment their training starts, as in StarCraft.
        public int SupplyUsed=>Supply(Player);
        int Supply(int owner)
        {
            int used=0;
            foreach(var u in Units)if(u.Owner==owner)used+=u.Stats.Supply;
            foreach(var b in Buildings)if(b.Owner==owner&&b.started)used+=Roster(owner).Unit(b.queue[0]).Supply;
            return used;
        }
        public int SupplyLimit=>Limit(Player);
        // What the side's finished, working buildings provide, up to the cap the scenario sets (MatchBalance.SupplyCap; a
        // campaign mission may raise or lower it for one side).
        int Limit(int owner){int limit=0;foreach(var b in Buildings)if(b.Owner==owner&&b.Ready>=1&&b.Powered)limit+=b.Stats.Supply;return Math.Min(limit,supplyCap[owner]);}
        readonly int[] supplyCap;
        public int SupplyCapOf(int side)=>supplyCap[side];
        public void SetSupplyCap(int side,int cap)=>supplyCap[side]=Math.Max(0,cap);
        public Fix HeightAt(Fix x,Fix z)=>Map.Height((int)x,(int)z,x-(int)x);
        // The match advances only in fixed steps of TickSeconds, so the same orders on the same steps give the same match on
        // every machine; the view interpolates between steps. Frame counts the steps made.
        public static Fix TickSeconds=>Step;
        // "Never" for timers that count the time since something happened: far in the past, yet clear of overflow.
        internal static readonly Fix LongAgo=1000000;
        public int Frame{get;private set;}
        public void Advance(){Tick(Step);Frame++;}
        // Any stretch of time at once, for tests and tools; it does not count frames.
        public void Tick(Fix dt)
        {
            // Commands wait for the step to end (Execute), also those the view would give from the match's events.
            stepping=true;
            try
            {
                UpdateNetwork(dt);
                TickBuildings(dt);
                Clock+=dt;
                TickProduction(dt);
                TickResearch(dt);
                TickBot(dt);
                // Movement runs in fixed steps, so a long frame and many short ones end in the same place.
                for(Fix left=dt;left>(Fix)1e-6f;left-=Step)StepUnits(Math.Min(Step,left));
                visionTimer+=dt;if(visionTimer>=(Fix).15f){visionTimer=0;UpdateVision();}
            }
            finally{stepping=false;}
        }
        public void UpdateVision()
        {
            for(int side=0;side<Sides;side++){Array.Clear(ground[side],0,ground[side].Length);Array.Clear(sky[side],0,sky[side].Length);}
            foreach(var u in Units)Reveal((int)u.X,(int)u.Z,u.Stats.Vision,u.Stats.Flying,u.Owner);
            foreach(var b in Buildings)if(b.Ready>=1)Reveal(b.X+b.Width/2,b.Z+b.Width/2,b.Stats.Vision,false,b.Owner);
            // Cheat: the player sees the whole map.
            if(Revealed)for(int z=0;z<64;z++)for(int x=0;x<64;x++){ground[Player][x,z]=sky[Player][x,z]=explored[Player][x,z]=true;}
        }
        // Whether a side currently sees the unit; a side always sees its own units.
        public bool SeenBy(int side,PrototypeUnit u)
        {
            if(u.Owner==side)return true;
            int x=Math.Max(0,Math.Min(63,(int)u.X)),z=Math.Max(0,Math.Min(63,(int)u.Z));
            var grid=u.Stats.Flying?sky[side]:ground[side];
            return grid[x,z];
        }
        // Ground observers cannot see up a cliff; air units see everything within their radius.
        void Reveal(int sx,int sz,int radius,bool fromAir,int side)
        {
            bool[,] ground=this.ground[side],sky=this.sky[side],explored=this.explored[side];
            Fix observer=fromAir?Fix.MaxValue:Map.Height(sx,sz)+(Fix).6f;
            for(int z=Math.Max(0,sz-radius);z<=Math.Min(63,sz+radius);z++)
            for(int x=Math.Max(0,sx-radius);x<=Math.Min(63,sx+radius);x++)
            {
                int dx=x-sx,dz=z-sz;if(dx*dx+dz*dz>radius*radius)continue;
                sky[x,z]=true;
                int steps=Math.Max(Math.Abs(dx),Math.Abs(dz));bool clear=true;
                for(int i=1;i<=steps;i++){int xx=(int)Math.Round(sx+dx*(Fix)i/steps),zz=(int)Math.Round(sz+dz*(Fix)i/steps);if(Map.Height(xx,zz)>observer){clear=false;break;}}
                if(clear){ground[x,z]=true;explored[x,z]=true;}
            }
        }
    }
    // Idle units fight back and engage enemies nearby; Move ignores enemies until arrival; AttackMove engages anything on
    // the way; Attack pursues one target; Hold stands still and shoots what is in range.
    public enum UnitOrder { Idle, Move, AttackMove, Attack, Hold }
    public sealed class PrototypeUnit
    {
        public readonly int Id;
        public readonly UnitKind Kind;
        public readonly UnitStats Stats;
        public readonly int Owner;
        public Fix X, Z;
        public int Health;
        public bool Alive=>Health>0;
        public UnitOrder Order { get; internal set; }
        // The enemy the unit is aiming at or closing in on right now.
        public PrototypeUnit Target { get; internal set; }
        // The enemy building it attacks when no enemy unit is targeted.
        public PrototypeBuilding TargetBuilding { get; internal set; }
        public ResourceDeposit HarvestTarget { get; internal set; }
        // Construction: the building this worker raises (it rises only while the worker stands by it), and the site the
        // worker walks to before that building is placed (kind -1: none).
        public PrototypeBuilding Constructing { get; internal set; }
        // A machine worker spent on a building: it left the match by dissolving into that site, not by dying.
        public PrototypeBuilding AbsorbedInto { get; internal set; }
        public int SiteKind { get; internal set; } = -1;
        public int SiteX { get; internal set; }
        public int SiteZ { get; internal set; }
        internal Fix siteTimer;
        // Repair (humans): the building or machinery this worker mends, whether it is at work on it right now, and the
        // health and price owed in fractions between steps. See PrototypeMatch.Repair.cs.
        public PrototypeBuilding Repairing { get; internal set; }
        public PrototypeUnit RepairingUnit { get; internal set; }
        public bool Welding { get; internal set; }
        // Automatic repair (autocast): an idle worker mends damaged things of its side nearby by itself.
        public bool AutoRepair { get; internal set; }
        internal Fix repairHealth, repairMetal, repairPlasma, repairTimer;
        // Machines: whether it mends inside the network right now, and how far it is taken apart (0 while it is not).
        public bool Mending { get; internal set; }
        public Fix Disassembly { get; internal set; }
        public bool Disassembling { get; internal set; }
        internal Fix mended;
        // Nanite mining: how far the worker has wrapped its deposit (0–1) and the cocoon it wears while wrapped; seconds
        // since it was last hit, for the cocoon to grow back and for machines to mend.
        public Fix Wrap{get;internal set;}
        public int Cocoon{get;internal set;}
        internal Fix sinceHit=PrototypeMatch.LongAgo,cocoonGrowth;
        // A leaper in the middle of a jump: from where to where, how far it has flown, where it passes its target.
        internal bool leaping,leapHit;
        internal Fix leapFromX,leapFromZ,leapToX,leapToZ,leapLength,leapDone,leapPass;
        internal PrototypeUnit leapTarget;
        public bool Leaping=>leaping;
        // A howitzer: packed, deploying, deployed or packing up, how far along the change is, how long a deployed one on
        // an attack-move has had nothing to shoot, and whether it was packed by hand (it then stays packed until the next
        // order). See PrototypeMatch.Siege.cs.
        // An emitter's beam: what it holds, since when, and when it last struck (PrototypeMatch.Upgrades.cs, Strike).
        internal object focus;
        internal Fix focusStart,lastFocusHit=-PrototypeMatch.LongAgo;
        public SiegeState SiegeMode{get;internal set;}
        internal Fix siegeTimer,siegeIdle;
        internal bool packedByHand;
        // 0 packed, 1 deployed, in between while it deploys or packs up.
        public Fix DeployShare=>SiegeMode==SiegeState.Deployed?1:SiegeMode==SiegeState.Packed?0
            :Stats.DeploySeconds<=0?1:SiegeMode==SiegeState.Deploying?siegeTimer/Stats.DeploySeconds:1-siegeTimer/Stats.DeploySeconds;
        public Fix LeapShare=>leaping?leapDone/leapLength:0;
        // Mining trips: the load a worker carries to a command node, whether it is plasma, and whether it is mining right now.
        public int Carrying { get; internal set; }
        public bool CarriesPlasma { get; internal set; }
        public bool Mining { get; internal set; }
        internal bool delivering;
        // Combat state: the target of an Attack order, the AttackMove destination, the last enemy that hit this unit,
        // and whether the current route is a chase rather than the order's own route.
        internal PrototypeUnit ordered, aggressor;
        internal PrototypeBuilding orderedBuilding;
        internal readonly List<QueuedOrder> queued = new List<QueuedOrder>();
        public IReadOnlyList<QueuedOrder> Queued => queued;
        internal Fix attackX, attackZ, cooldown, chaseTimer;
        internal bool engaged;
        internal readonly Queue<Waypoint> route = new Queue<Waypoint>();
        internal Fix harvestTimer;
        // Movement state: the order's destination, where the last order ended (for group arrival) and progress tracking.
        internal Fix goalX, goalZ, arrivedX, arrivedZ, levelY, bestDistance, stuckTime, startX, startZ, lastDx, lastDz;
        internal bool hasGoal, arrived, touchedArrived, returning, holdsSpot;
        internal int replans;
        // Where the unit came to rest after its last order, and how long since a passing unit last shoved it from there.
        internal Fix restX, restZ, calmTime;
        internal int returns;
        public bool Moving=>route.Count>0;
        public int WaypointCount=>route.Count;
        // Where the unit stood before the last movement step, for the view to interpolate.
        public Fix PreviousX=>startX;
        public Fix PreviousZ=>startZ;
        public IEnumerable<Waypoint> Route=>route;
        internal PrototypeUnit(int id,UnitKind kind,UnitStats stats,int owner){Id=id;Kind=kind;Stats=stats;Owner=owner;Health=stats.Health;}
    }
    public sealed class PrototypeBuilding
    {
        // Id: the building's number in its match, as commands and the view name it (0: a stand-in, never placed).
        public int Id{get;internal set;}
        public int X,Z,Width,Kind,Owner,Health;
        public Fix Ready;
        // The owner faction's numbers for this kind; a machine building rises by itself once its site is placed.
        public BuildingStats Stats{get;internal set;}
        public bool SelfBuilt{get;internal set;}
        // A machine site made of the worker that dissolved into it; cancelling the site gives the worker back.
        internal bool holdsWorker;
        // Machines: whether it works, covered by the network or still on its grace (a core always is); humans always are.
        // See PrototypeMatch.Network.cs.
        public bool Powered{get;internal set;}=true;
        // Whether the network reaches it right now, and for how long it has been cut off: it stays powered on a grace of
        // NetworkGrace seconds.
        public bool Linked{get;internal set;}=true;
        public Fix DarkFor{get;internal set;}
        // Seconds since it was last hit, and the health a machine building mends in fractions between steps.
        internal Fix sinceHit=PrototypeMatch.LongAgo,mended;
        public bool Mending{get;internal set;}
        // The upgrade a research building is researching, and for how long it has been at it.
        internal Upgrade? research;
        public Upgrade? Researching=>research;
        public Fix ResearchTime{get;internal set;}
        // The worker raising it, while it is at work on it; nobody when the construction waits.
        internal PrototypeUnit builder;
        public PrototypeUnit Builder=>builder!=null&&builder.Alive&&builder.Constructing==this?builder:null;
        // Production: the queue (the front unit is in training), seconds trained on it, whether it has taken its supply
        // or waits for it, and where new units go.
        internal readonly List<UnitKind> queue=new List<UnitKind>();
        internal bool started;
        public IReadOnlyList<UnitKind> Queue=>queue;
        public Fix Training{get;internal set;}
        public bool SupplyBlocked{get;internal set;}
        public bool HasRally{get;internal set;}
        public Fix RallyX{get;internal set;}
        public Fix RallyZ{get;internal set;}
    }
}
