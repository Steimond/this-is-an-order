using Order.Contracts;

namespace Order.Content
{
    // Unit roles of a faction; the value is the index in FactionBalance.Units, where a role the faction lacks is null.
    // Leaper is a machine role; Tank, Artillery and Fighter are the humans' tank, howitzer and VTOL interceptor.
    // New roles go at the end: assets keep the numbers of the roles their buildings train.
    public enum UnitKind { Worker, Melee, Ranged, Air, Leaper, Tank, Artillery, Fighter }
    // Runtime copies isolate a running match from Inspector edits. Defaults also seed new assets and tests.
    public sealed class UnitStats
    {
        public string Name="Рабочий";
        public float Speed=5, Radius=.28f;
        public int Health=40, Vision=9, HarvestAmount=5, Supply=1, Damage, Metal=50, Plasma;
        // Each point of armour takes one off every blow the unit receives (down to one); upgrades add to it.
        public int Armor;
        // Hauling workers carry HarvestAmount per trip after HarvestInterval seconds at the deposit; nanite workers add
        // HarvestAmount to the stock every HarvestInterval seconds while they stay wrapped round it.
        public float HarvestInterval=2, AttackRange, AttackCooldown=1, TrainSeconds=12;
        // Nanite workers: seconds to wrap a deposit, and the cocoon they wear while wrapped — extra health that takes
        // the blows first and grows back by CocoonRegen a second once they have not been hit for a while.
        public float WrapSeconds, CocoonRegen;
        public int Cocoon;
        // Attack range is measured between the edges of the two units. A leaper attacks by jumping through its target:
        // AttackRange is the reach of the jump and AttackCooldown the time between jumps (PrototypeMatch.Leap.cs).
        public bool Flying, HitsGround=true, HitsAir, Leap;
        // Machinery: human workers repair it (their drone and the gunship); every machine unit is machinery.
        public bool Mechanical;
        // Siege (the howitzer): it fires only deployed, and deploying or packing up takes DeploySeconds; it cannot hit
        // targets nearer than MinRange (edge to edge). Splash: other enemy ground units within this radius of the target's
        // centre take half the blow (PrototypeMatch.Siege.cs).
        public bool Siege;
        public float MinRange, Splash, DeploySeconds;
        // A beam (the machines' emitter): held on one target, its blow grows to Ramp times the damage over RampSeconds
        // (1: no beam).
        public float Ramp=1, RampSeconds;
    }
    public sealed class BuildingStats
    {
        public string Name;
        public int Width, Metal, Plasma, Supply, Vision=7, Health=500;
        public float BuildSeconds=5, Height=1;
        // Units this building trains, in command card order.
        public UnitKind[] Trains=new UnitKind[0];
        // Machine network: the radius a core or relay covers, and how many relays a core allows.
        public float Coverage;
        public int Relays;
        // Researches the weapon and armour upgrades of its side.
        public bool Researches;
        // Stands on a plasma rift: a side that has such a building mines plasma only through it.
        public bool Extracts;
    }
    // A faction's roster and the way it gathers and builds (SETTING.md). Buildings: 0 supply, 1 production, 2 main,
    // then the humans' workshop and extractor, the machines' relay and firmware node.
    public sealed class FactionBalance
    {
        // Relay is the machines' index 3; the humans have their workshop there.
        public const int Main = 2, Relay = 3;
        // The building that researches upgrades, and the one a rift needs before the side can mine its plasma (-1: none).
        public int Research=-1, Extractor=-1;
        public UnitStats[] Units;
        public BuildingStats[] Buildings;
        // Machines wrap a deposit with nanites instead of hauling loads, and their buildings rise by themselves once the
        // worker has thrown the construction grenade; humans haul and build by hand. Machine buildings other than the
        // core stand and work only inside the coverage of the side's network.
        public bool NaniteMining, SelfBuilding, Network;
        // Humans: workers repair buildings and machinery. Machines: inside the network their units and buildings mend
        // themselves, and a unit can be taken apart for part of its price (PrototypeMatch.Repair.cs).
        public bool Repairs, Mends;
        // Machines: the worker dissolves into the nanites of the building it raises and is spent on it, as a zerg drone is;
        // cancelling the site gives it back (SETTING.md, «Наниты — суть расы»).
        public bool ConsumesWorker;
        // What a worker can build, in command card order; the main building starts a new base.
        public int[] Buildable={0,1,Main,3};
        public UnitStats Worker=>Units[(int)UnitKind.Worker];
        public UnitStats Unit(UnitKind kind)=>Units[(int)kind];
        public bool Has(UnitKind kind)=>(int)kind<Units.Length&&Units[(int)kind]!=null;
    }
    public sealed class MatchBalance
    {
        // Names are temporary, like the faction names. Melee units hit only ground, riflemen hit ground and air; the tank
        // is armoured, the howitzer deploys to fire far and over an area, the interceptor hits only air.
        public FactionBalance Expedition=new FactionBalance{Research=3,Extractor=4,Repairs=true,Buildable=new[]{0,1,FactionBalance.Main,3,4},
            Units=new[]{
                new UnitStats{Mechanical=true},
                new UnitStats{Name="Штурмовик",Speed=4.2f,Radius=.38f,Health=120,Vision=8,HarvestAmount=0,Supply=2,Damage=12,AttackRange=.3f,AttackCooldown=1.1f,Metal=100,Plasma=25,TrainSeconds=22},
                new UnitStats{Name="Стрелок",Speed=4.4f,Radius=.3f,Health=60,Vision=9,HarvestAmount=0,Supply=1,Damage=7,AttackRange=5.5f,AttackCooldown=.85f,HitsAir=true,Metal=50,TrainSeconds=18},
                new UnitStats{Name="Винтокрыл",Speed=6,Radius=.45f,Health=110,Vision=11,HarvestAmount=0,Supply=2,Damage=9,AttackRange=4.5f,AttackCooldown=1.2f,Flying=true,Metal=150,Plasma=100,TrainSeconds=32,Mechanical=true},
                // No leaper; then the tank, the howitzer and the VTOL interceptor (SETTING.md, «Составы рас»).
                null,
                new UnitStats{Name="Танк",Speed=3.6f,Radius=.45f,Health=200,Armor=1,Vision=9,HarvestAmount=0,Supply=3,Damage=20,AttackRange=6,AttackCooldown=1.5f,Metal=150,Plasma=100,TrainSeconds=34,Mechanical=true},
                new UnitStats{Name="Гаубица",Speed=3.2f,Radius=.45f,Health=140,Armor=1,Vision=10,HarvestAmount=0,Supply=3,Damage=35,AttackRange=12,AttackCooldown=3,Metal=150,Plasma=125,TrainSeconds=40,Mechanical=true,
                    Siege=true,MinRange=2.5f,Splash=1.3f,DeploySeconds=2.5f},
                new UnitStats{Name="Перехватчик",Speed=7,Radius=.42f,Health=120,Vision=11,HarvestAmount=0,Supply=2,Damage=14,AttackRange=6.5f,AttackCooldown=1.3f,Flying=true,HitsGround=false,HitsAir=true,Metal=125,Plasma=75,TrainSeconds=28,Mechanical=true}},
            Buildings=new[]{
                new BuildingStats{Name="Узел снабжения",Width=2,Metal=100,Supply=8,Height=1.4f},
                new BuildingStats{Name="Цех",Width=3,Metal=150,Plasma=50,Height=2.2f,BuildSeconds=8,Health=1000,Trains=new[]{UnitKind.Melee,UnitKind.Ranged,UnitKind.Air,UnitKind.Tank,UnitKind.Artillery,UnitKind.Fighter}},
                new BuildingStats{Name="Командный узел",Width=3,Metal=400,Supply=10,Height=2.6f,BuildSeconds=50,Health=1500,Trains=new[]{UnitKind.Worker}},
                new BuildingStats{Name="Мастерская",Width=2,Metal=125,Height=1.6f,BuildSeconds=20,Health=750,Researches=true},
                new BuildingStats{Name="Экстрактор",Width=1,Metal=75,Height=1.5f,BuildSeconds=12,Health=500,Vision=5,Extracts=true}}};
        // Machines: a worker brings about twice a human worker's income (150 against 80 metal a minute), so its cocoon
        // roughly doubles its health on the deposit, and it is assembled faster (SETTING.md). A worker is spent on every
        // building it raises, so it costs as little as a human one and the buildings 50 less: a building and its worker
        // together cost what the building did before. Spiders are a
        // cheap fast swarm, weak one by one; the leaper jumps through one target at a time. The emitter's beam is weak at
        // first and three times as strong after four seconds on one target: good against armour and buildings, poor
        // against a swarm. The glider copies the humans' gunship until the machines get their own.
        public FactionBalance Resonance=new FactionBalance{NaniteMining=true,SelfBuilding=true,Network=true,Mends=true,ConsumesWorker=true,Buildable=new[]{0,1,FactionBalance.Relay,FactionBalance.Main,4},Research=4,
            Units=new[]{
                new UnitStats{Name="Ткач",HarvestAmount=20,HarvestInterval=8,WrapSeconds=2,Cocoon=40,CocoonRegen=8,Metal=50,TrainSeconds=8,Mechanical=true},
                new UnitStats{Name="Паучок",Speed=6,Radius=.22f,Health=35,Vision=7,HarvestAmount=0,Supply=1,Damage=5,AttackRange=.2f,AttackCooldown=.6f,Metal=40,TrainSeconds=12,Mechanical=true},
                new UnitStats{Name="Излучатель",Speed=4.2f,Radius=.3f,Health=80,Vision=9,HarvestAmount=0,Supply=2,Damage=3,AttackRange=5,AttackCooldown=.35f,HitsAir=true,Metal=100,Plasma=25,TrainSeconds=22,Mechanical=true,Ramp=3,RampSeconds=4},
                new UnitStats{Name="Планёр",Speed=6,Radius=.45f,Health=110,Vision=11,HarvestAmount=0,Supply=2,Damage=9,AttackRange=4.5f,AttackCooldown=1.2f,Flying=true,Metal=150,Plasma=100,TrainSeconds=32,Mechanical=true},
                new UnitStats{Name="Прыгун",Speed=4.6f,Radius=.32f,Health=90,Vision=8,HarvestAmount=0,Supply=2,Damage=30,AttackRange=4.5f,AttackCooldown=4,Leap=true,Metal=100,Plasma=50,TrainSeconds=24,Mechanical=true}},
            Buildings=new[]{
                new BuildingStats{Name="Вычислительный узел",Width=2,Metal=50,Supply=8,Height=1.4f},
                new BuildingStats{Name="Сборочный узел",Width=3,Metal=100,Plasma=50,Height=2.2f,BuildSeconds=8,Health=1000,Trains=new[]{UnitKind.Melee,UnitKind.Leaper,UnitKind.Ranged,UnitKind.Air}},
                new BuildingStats{Name="Ядро",Width=3,Metal=350,Supply=10,Height=2.6f,BuildSeconds=45,Health=1500,Trains=new[]{UnitKind.Worker},Coverage=10,Relays=3},
                new BuildingStats{Name="Ретранслятор",Width=1,Metal=25,Height=1.9f,BuildSeconds=4,Health=200,Vision=8,Coverage=7},
                new BuildingStats{Name="Узел прошивки",Width=2,Metal=75,Height=1.6f,BuildSeconds=20,Health=750,Researches=true}}};
        public FactionBalance Of(FactionId faction)=>faction==FactionId.Resonance?Resonance:Expedition;
        public int StartingMetal=500, StartingPlasma=100, MetalDeposit=1500, PlasmaDeposit=2500;
    }
}
