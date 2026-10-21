namespace Order.Content
{
    // Runtime copies isolate a running match from Inspector edits. Defaults also seed new assets and tests.
    public sealed class UnitStats
    {
        public string Name="Рабочий";
        public float Speed=5, Radius=.28f;
        public int Health=40, Vision=9, HarvestAmount=8;
        public float HarvestInterval=1;
    }
    public sealed class BuildingStats
    {
        public string Name;
        public int Width, Minerals, Gas, Supply, Vision=7, Health=500;
        public float BuildSeconds=5, Height=1;
    }
    public sealed class MatchBalance
    {
        public UnitStats Worker=new UnitStats();
        public BuildingStats[] Buildings={
            new BuildingStats{Name="Узел снабжения",Width=2,Minerals=100,Supply=8,Height=1.4f},
            new BuildingStats{Name="Цех",Width=3,Minerals=150,Gas=50,Height=2.2f,BuildSeconds=8},
            new BuildingStats{Name="Командный узел",Width=3,Supply=10,Height=2.6f,Health=1500}};
        public int StartingMinerals=500, StartingGas=100, MineralDeposit=1500, GasDeposit=2500;
    }
}
