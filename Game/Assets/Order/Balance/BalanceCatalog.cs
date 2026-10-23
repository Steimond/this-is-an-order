using Order.Content;
using UnityEngine;
namespace Order.Balance
{
    public sealed class BalanceCatalog : ScriptableObject
    {
        public UnitBalanceAsset Worker, Melee, Ranged, Air;
        public BuildingBalanceAsset Supply, Factory, Headquarters;
        public EconomyBalanceAsset Economy;
        public MatchBalance Snapshot()
        {
            if(Worker==null||Melee==null||Ranged==null||Air==null||Supply==null||Factory==null||Headquarters==null||Economy==null)
                throw new System.InvalidOperationException("Balance catalog has missing references.");
            return new MatchBalance{Units=new[]{Worker.Snapshot(),Melee.Snapshot(),Ranged.Snapshot(),Air.Snapshot()},Buildings=new[]{Supply.Snapshot(),Factory.Snapshot(),Headquarters.Snapshot()},StartingMetal=Mathf.Max(0,Economy.StartingMetal),StartingPlasma=Mathf.Max(0,Economy.StartingPlasma),MetalDeposit=Mathf.Max(1,Economy.MetalDeposit),PlasmaDeposit=Mathf.Max(1,Economy.PlasmaDeposit)};
        }
    }
}
