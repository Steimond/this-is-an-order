using UnityEngine;
using UnityEngine.Serialization;
namespace Order.Balance
{
    [CreateAssetMenu(menuName="Order/Balance/Economy")]
    public sealed class EconomyBalanceAsset : ScriptableObject
    {
        [Min(0), FormerlySerializedAs("StartingMinerals")] public int StartingMetal=500;
        [Min(0), FormerlySerializedAs("StartingGas")] public int StartingPlasma=100;
        [Min(1), FormerlySerializedAs("MineralDeposit")] public int MetalDeposit=1500;
        [Min(1), FormerlySerializedAs("GasDeposit")] public int PlasmaDeposit=2500;
    }
}
