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
        [Min(1)] public int Health=40, Vision=9;
        [Tooltip("0 — юнит не добывает ресурсы.")] [Min(0)] public int HarvestAmount=8;
        [Min(.1f)] public float HarvestInterval=1;
        [Min(0)] public int Supply=1;
        [Tooltip("Летает над скалами и зданиями.")] public bool Flying;
        [Header("Атака (применится вместе с боем)")]
        [Min(0)] public int Damage;
        [Min(0)] public float AttackRange;
        [Min(.1f)] public float AttackCooldown=1;
        public UnitStats Snapshot()=>new UnitStats{Name=DisplayName,Speed=Mathf.Max(.1f,MoveSpeed),Radius=Mathf.Clamp(CollisionRadius,.1f,.45f),Health=Mathf.Max(1,Health),
            Vision=Mathf.Clamp(Vision,1,64),HarvestAmount=Mathf.Max(0,HarvestAmount),HarvestInterval=Mathf.Max(.1f,HarvestInterval),Supply=Mathf.Max(0,Supply),Flying=Flying,
            Damage=Mathf.Max(0,Damage),AttackRange=Mathf.Max(0,AttackRange),AttackCooldown=Mathf.Max(.1f,AttackCooldown)};
    }
}
