using Order.Content;
using UnityEngine;
namespace Order.Balance
{
    [CreateAssetMenu(menuName="Order/Balance/Unit")]
    public sealed class UnitBalanceAsset : ScriptableObject
    {
        public string DisplayName="Рабочий";
        [Min(.1f)] public float MoveSpeed=5;
        [Range(.1f,.45f)] public float CollisionRadius=.28f;
        [Min(1)] public int Health=40, Vision=9, HarvestAmount=8;
        [Min(.1f)] public float HarvestInterval=1;
        public UnitStats Snapshot()=>new UnitStats{Name=DisplayName,Speed=Mathf.Max(.1f,MoveSpeed),Radius=Mathf.Clamp(CollisionRadius,.1f,.45f),Health=Mathf.Max(1,Health),Vision=Mathf.Clamp(Vision,1,64),HarvestAmount=Mathf.Max(1,HarvestAmount),HarvestInterval=Mathf.Max(.1f,HarvestInterval)};
    }
}
