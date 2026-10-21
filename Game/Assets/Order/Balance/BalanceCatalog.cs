using Order.Content;
using UnityEngine;
namespace Order.Balance
{
    public sealed class BalanceCatalog : ScriptableObject
    {
        public UnitBalanceAsset Worker;
        public BuildingBalanceAsset Supply, Factory, Headquarters;
        public EconomyBalanceAsset Economy;
        public MatchBalance Snapshot()
        {
            if(Worker==null||Supply==null||Factory==null||Headquarters==null||Economy==null)
                throw new System.InvalidOperationException("Balance catalog has missing references.");
            return new MatchBalance{Worker=Worker.Snapshot(),Buildings=new[]{Supply.Snapshot(),Factory.Snapshot(),Headquarters.Snapshot()},StartingMinerals=Mathf.Max(0,Economy.StartingMinerals),StartingGas=Mathf.Max(0,Economy.StartingGas),MineralDeposit=Mathf.Max(1,Economy.MineralDeposit),GasDeposit=Mathf.Max(1,Economy.GasDeposit)};
        }
    }
}
