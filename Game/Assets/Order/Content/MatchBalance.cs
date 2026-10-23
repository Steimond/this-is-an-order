using Order.Contracts;

namespace Order.Content
{
    // Unit roles of a faction; the value is the index in FactionBalance.Units.
    public enum UnitKind { Worker, Melee, Ranged, Air }
    // Runtime copies isolate a running match from Inspector edits. Defaults also seed new assets and tests.
    public sealed class UnitStats
    {
        public string Name="Рабочий";
        public float Speed=5, Radius=.28f;
        public int Health=40, Vision=9, HarvestAmount=5, Supply=1, Damage, Metal=50, Plasma;
        // Hauling workers carry HarvestAmount per trip after HarvestInterval seconds at the deposit; nanite workers add
        // HarvestAmount to the stock every HarvestInterval seconds while they stay wrapped round it.
        public float HarvestInterval=2, AttackRange, AttackCooldown=1, TrainSeconds=12;
        // Nanite workers: seconds to wrap a deposit, and the cocoon they wear while wrapped — extra health that takes
        // the blows first and grows back by CocoonRegen a second once they have not been hit for a while.
        public float WrapSeconds, CocoonRegen;
        public int Cocoon;
        // Attack range is measured between the edges of the two units.
        public bool Flying, HitsGround=true, HitsAir;
    }
    public sealed class BuildingStats
    {
        public string Name;
        public int Width, Metal, Plasma, Supply, Vision=7, Health=500;
        public float BuildSeconds=5, Height=1;
        // Units this building trains, in command card order.
        public UnitKind[] Trains=new UnitKind[0];
    }
    // A faction's roster and the way it gathers and builds (SETTING.md). Buildings: 0 supply, 1 production, 2 main.
    public sealed class FactionBalance
    {
        public UnitStats[] Units;
        public BuildingStats[] Buildings;
        // Machines wrap a deposit with nanites instead of hauling loads, and their buildings rise by themselves once the
        // worker has thrown the construction grenade; humans haul and build by hand.
        public bool NaniteMining, SelfBuilding;
        public UnitStats Worker=>Units[(int)UnitKind.Worker];
        public UnitStats Unit(UnitKind kind)=>Units[(int)kind];
    }
    public sealed class MatchBalance
    {
        // Names are temporary, like the faction names. Melee units hit only ground, riflemen hit ground and air.
        public FactionBalance Expedition=new FactionBalance{
            Units=new[]{
                new UnitStats(),
                new UnitStats{Name="Штурмовик",Speed=4.2f,Radius=.38f,Health=120,Vision=8,HarvestAmount=0,Supply=2,Damage=12,AttackRange=.3f,AttackCooldown=1.1f,Metal=100,Plasma=25,TrainSeconds=22},
                new UnitStats{Name="Стрелок",Speed=4.4f,Radius=.3f,Health=60,Vision=9,HarvestAmount=0,Supply=1,Damage=7,AttackRange=5.5f,AttackCooldown=.85f,HitsAir=true,Metal=50,TrainSeconds=18},
                new UnitStats{Name="Винтокрыл",Speed=6,Radius=.45f,Health=110,Vision=11,HarvestAmount=0,Supply=2,Damage=9,AttackRange=4.5f,AttackCooldown=1.2f,Flying=true,Metal=150,Plasma=100,TrainSeconds=32}},
            Buildings=new[]{
                new BuildingStats{Name="Узел снабжения",Width=2,Metal=100,Supply=8,Height=1.4f},
                new BuildingStats{Name="Цех",Width=3,Metal=150,Plasma=50,Height=2.2f,BuildSeconds=8,Health=1000,Trains=new[]{UnitKind.Melee,UnitKind.Ranged,UnitKind.Air}},
                new BuildingStats{Name="Командный узел",Width=3,Supply=10,Height=2.6f,Health=1500,Trains=new[]{UnitKind.Worker}}}};
        // Machines: a worker brings about twice a human worker's income (150 against 80 metal a minute), so its cocoon
        // roughly doubles its health on the deposit, and it is assembled faster for more metal (SETTING.md). The combat
        // roles copy the humans' numbers until the machines get their own units.
        public FactionBalance Resonance=new FactionBalance{NaniteMining=true,SelfBuilding=true,
            Units=new[]{
                new UnitStats{Name="Ткач",HarvestAmount=20,HarvestInterval=8,WrapSeconds=2,Cocoon=40,CocoonRegen=8,Metal=75,TrainSeconds=8},
                new UnitStats{Name="Резак",Speed=4.2f,Radius=.38f,Health=120,Vision=8,HarvestAmount=0,Supply=2,Damage=12,AttackRange=.3f,AttackCooldown=1.1f,Metal=100,Plasma=25,TrainSeconds=22},
                new UnitStats{Name="Излучатель",Speed=4.4f,Radius=.3f,Health=60,Vision=9,HarvestAmount=0,Supply=1,Damage=7,AttackRange=5.5f,AttackCooldown=.85f,HitsAir=true,Metal=50,TrainSeconds=18},
                new UnitStats{Name="Планёр",Speed=6,Radius=.45f,Health=110,Vision=11,HarvestAmount=0,Supply=2,Damage=9,AttackRange=4.5f,AttackCooldown=1.2f,Flying=true,Metal=150,Plasma=100,TrainSeconds=32}},
            Buildings=new[]{
                new BuildingStats{Name="Вычислительный узел",Width=2,Metal=100,Supply=8,Height=1.4f},
                new BuildingStats{Name="Сборочный узел",Width=3,Metal=150,Plasma=50,Height=2.2f,BuildSeconds=8,Health=1000,Trains=new[]{UnitKind.Melee,UnitKind.Ranged,UnitKind.Air}},
                new BuildingStats{Name="Ядро",Width=3,Supply=10,Height=2.6f,Health=1500,Trains=new[]{UnitKind.Worker}}}};
        public FactionBalance Of(FactionId faction)=>faction==FactionId.Resonance?Resonance:Expedition;
        public int StartingMetal=500, StartingPlasma=100, MetalDeposit=1500, PlasmaDeposit=2500;
    }
}
