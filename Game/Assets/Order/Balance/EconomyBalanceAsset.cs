using UnityEngine;
namespace Order.Balance
{
    [CreateAssetMenu(menuName="Order/Balance/Economy")]
    public sealed class EconomyBalanceAsset : ScriptableObject
    {
        [Min(0)] public int StartingMinerals=500, StartingGas=100;
        [Min(1)] public int MineralDeposit=1500, GasDeposit=2500;
    }
}
