namespace Order.Content
{
    // Unit roles of the prototype faction; the value is the index in MatchBalance.Units.
    public enum UnitKind { Worker, Melee, Ranged, Air }
    // Runtime copies isolate a running match from Inspector edits. Defaults also seed new assets and tests.
    public sealed class UnitStats
    {
        public string Name="Рабочий";
        public float Speed=5, Radius=.28f;
        public int Health=40, Vision=9, HarvestAmount=8, Supply=1, Damage;
        public float HarvestInterval=1, AttackRange, AttackCooldown=1;
        public bool Flying;
    }
    public sealed class BuildingStats
    {
        public string Name;
        public int Width, Minerals, Gas, Supply, Vision=7, Health=500;
        public float BuildSeconds=5, Height=1;
    }
    public sealed class MatchBalance
    {
        // Names are temporary, like the faction names. Attack values are shown now and used once combat exists.
        public UnitStats[] Units={
            new UnitStats(),
            new UnitStats{Name="Штурмовик",Speed=4.2f,Radius=.38f,Health=120,Vision=8,HarvestAmount=0,Supply=2,Damage=12,AttackRange=.3f,AttackCooldown=1.1f},
            new UnitStats{Name="Стрелок",Speed=4.4f,Radius=.3f,Health=60,Vision=9,HarvestAmount=0,Supply=1,Damage=7,AttackRange=5.5f,AttackCooldown=.85f},
            new UnitStats{Name="Винтокрыл",Speed=6,Radius=.45f,Health=110,Vision=11,HarvestAmount=0,Supply=2,Damage=9,AttackRange=4.5f,AttackCooldown=1.2f,Flying=true}};
        public UnitStats Worker=>Units[(int)UnitKind.Worker];
        public UnitStats Unit(UnitKind kind)=>Units[(int)kind];
        public BuildingStats[] Buildings={
            new BuildingStats{Name="Узел снабжения",Width=2,Minerals=100,Supply=8,Height=1.4f},
            new BuildingStats{Name="Цех",Width=3,Minerals=150,Gas=50,Height=2.2f,BuildSeconds=8},
            new BuildingStats{Name="Командный узел",Width=3,Supply=10,Height=2.6f,Health=1500}};
        public int StartingMinerals=500, StartingGas=100, MineralDeposit=1500, GasDeposit=2500;
    }
}
