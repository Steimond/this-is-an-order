namespace Order.Content
{
    // Unit roles of the prototype faction; the value is the index in MatchBalance.Units.
    public enum UnitKind { Worker, Melee, Ranged, Air }
    // Runtime copies isolate a running match from Inspector edits. Defaults also seed new assets and tests.
    public sealed class UnitStats
    {
        public string Name="Рабочий";
        public float Speed=5, Radius=.28f;
        public int Health=40, Vision=9, HarvestAmount=5, Supply=1, Damage, Metal=50, Plasma;
        // A worker carries HarvestAmount per trip after HarvestInterval seconds at the deposit.
        public float HarvestInterval=2, AttackRange, AttackCooldown=1, TrainSeconds=12;
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
    public sealed class MatchBalance
    {
        // Names are temporary, like the faction names. Melee units hit only ground, riflemen hit ground and air.
        public UnitStats[] Units={
            new UnitStats(),
            new UnitStats{Name="Штурмовик",Speed=4.2f,Radius=.38f,Health=120,Vision=8,HarvestAmount=0,Supply=2,Damage=12,AttackRange=.3f,AttackCooldown=1.1f,Metal=100,Plasma=25,TrainSeconds=22},
            new UnitStats{Name="Стрелок",Speed=4.4f,Radius=.3f,Health=60,Vision=9,HarvestAmount=0,Supply=1,Damage=7,AttackRange=5.5f,AttackCooldown=.85f,HitsAir=true,Metal=50,TrainSeconds=18},
            new UnitStats{Name="Винтокрыл",Speed=6,Radius=.45f,Health=110,Vision=11,HarvestAmount=0,Supply=2,Damage=9,AttackRange=4.5f,AttackCooldown=1.2f,Flying=true,Metal=150,Plasma=100,TrainSeconds=32}};
        public UnitStats Worker=>Units[(int)UnitKind.Worker];
        public UnitStats Unit(UnitKind kind)=>Units[(int)kind];
        public BuildingStats[] Buildings={
            new BuildingStats{Name="Узел снабжения",Width=2,Metal=100,Supply=8,Height=1.4f},
            new BuildingStats{Name="Цех",Width=3,Metal=150,Plasma=50,Height=2.2f,BuildSeconds=8,Health=1000,Trains=new[]{UnitKind.Melee,UnitKind.Ranged,UnitKind.Air}},
            new BuildingStats{Name="Командный узел",Width=3,Supply=10,Height=2.6f,Health=1500,Trains=new[]{UnitKind.Worker}}};
        public int StartingMetal=500, StartingPlasma=100, MetalDeposit=1500, PlasmaDeposit=2500;
    }
}
