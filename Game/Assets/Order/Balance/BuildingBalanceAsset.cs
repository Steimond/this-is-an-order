using Order.Content;
using UnityEngine;
using UnityEngine.Serialization;
namespace Order.Balance
{
    [CreateAssetMenu(menuName="Order/Balance/Building")]
    public sealed class BuildingBalanceAsset : ScriptableObject
    {
        public string DisplayName;
        [Range(1,8)] public int Footprint=2;
        [Min(0), FormerlySerializedAs("Minerals")] public int Metal=100;
        [Min(0), FormerlySerializedAs("Gas")] public int Plasma;
        [Min(0)] public int Supply;
        [Min(1)] public int Health=500, Vision=7;
        [Min(.1f)] public float ConstructionSeconds=5, ModelHeight=1.4f;
        [Tooltip("Юниты, которых обучает здание, в порядке карты команд.")] public UnitKind[] Trains=new UnitKind[0];
        [Header("Сеть — только машины")]
        [Tooltip("Радиус покрытия сети в клетках: у ядра и ретранслятора; 0 — здание сеть не даёт.")] [Min(0)] public float Coverage;
        [Tooltip("Сколько ретрансляторов разрешает это здание (ядро).")] [Min(0)] public int Relays;
        [Tooltip("Исследует улучшения оружия и брони своей стороны.")] public bool Researches;
        [Tooltip("Ставится на разлом плазмы; сторона с таким зданием добывает плазму только через него.")] public bool Extracts;
        public BuildingStats Snapshot()=>new BuildingStats{Name=DisplayName,Width=Mathf.Clamp(Footprint,1,8),Metal=Mathf.Max(0,Metal),Plasma=Mathf.Max(0,Plasma),Supply=Mathf.Max(0,Supply),Health=Mathf.Max(1,Health),Vision=Mathf.Clamp(Vision,1,64),BuildSeconds=Mathf.Max(.1f,ConstructionSeconds),Height=Mathf.Max(.1f,ModelHeight),Trains=(UnitKind[])(Trains??new UnitKind[0]).Clone(),Coverage=Mathf.Max(0,Coverage),Relays=Mathf.Max(0,Relays),Researches=Researches,Extracts=Extracts};
    }
}
