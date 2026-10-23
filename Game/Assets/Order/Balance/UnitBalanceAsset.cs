using Order.Content;
using UnityEngine;
using UnityEngine.Serialization;
namespace Order.Balance
{
    [CreateAssetMenu(menuName="Order/Balance/Unit")]
    public sealed class UnitBalanceAsset : ScriptableObject
    {
        public string DisplayName="Рабочий";
        [Min(.1f)] public float MoveSpeed=5;
        [Range(.1f,.45f)] public float CollisionRadius=.28f;
        [Min(1)] public int Health=40, Vision=9;
        [Tooltip("Груз за один рейс, у машин — порция за тик; 0 — юнит не добывает ресурсы.")] [Min(0)] public int HarvestAmount=5;
        [Tooltip("Сколько секунд рабочий добывает у залежи перед рейсом к командному узлу, у машин — длительность тика.")] [Min(.1f)] public float HarvestInterval=2;
        [Header("Наниты — только рабочие машин")]
        [Tooltip("Сколько секунд рабочий обвивает залежь перед первым тиком.")] [Min(0)] public float WrapSeconds;
        [Tooltip("Прочность нанитового кокона, пока рабочий обвит вокруг залежи; принимает удары первой.")] [Min(0)] public int Cocoon;
        [Tooltip("Сколько прочности кокон восстанавливает в секунду, когда рабочего давно не били.")] [Min(0)] public float CocoonRegen;
        [Min(0)] public int Supply=1;
        [Tooltip("Летает над скалами и зданиями.")] public bool Flying;
        [Header("Атака")]
        [Tooltip("0 — юнит не атакует.")] [Min(0)] public int Damage;
        [Tooltip("Дальность от края юнита до края цели.")] [Min(0)] public float AttackRange;
        [Min(.1f)] public float AttackCooldown=1;
        public bool HitsGround=true, HitsAir;
        [Header("Производство")]
        [Min(0), FormerlySerializedAs("Minerals")] public int Metal=50;
        [Min(0), FormerlySerializedAs("Gas")] public int Plasma;
        [Min(.1f)] public float TrainSeconds=12;
        public UnitStats Snapshot()=>new UnitStats{Name=DisplayName,Speed=Mathf.Max(.1f,MoveSpeed),Radius=Mathf.Clamp(CollisionRadius,.1f,.45f),Health=Mathf.Max(1,Health),
            Vision=Mathf.Clamp(Vision,1,64),HarvestAmount=Mathf.Max(0,HarvestAmount),HarvestInterval=Mathf.Max(.1f,HarvestInterval),Supply=Mathf.Max(0,Supply),Flying=Flying,
            Damage=Mathf.Max(0,Damage),AttackRange=Mathf.Max(0,AttackRange),AttackCooldown=Mathf.Max(.1f,AttackCooldown),HitsGround=HitsGround,HitsAir=HitsAir,
            Metal=Mathf.Max(0,Metal),Plasma=Mathf.Max(0,Plasma),TrainSeconds=Mathf.Max(.1f,TrainSeconds),
            WrapSeconds=Mathf.Max(0,WrapSeconds),Cocoon=Mathf.Max(0,Cocoon),CocoonRegen=Mathf.Max(0,CocoonRegen)};
    }
}
