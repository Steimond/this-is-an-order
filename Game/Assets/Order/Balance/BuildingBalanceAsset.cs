using Order.Content;
using UnityEngine;
namespace Order.Balance
{
    [CreateAssetMenu(menuName="Order/Balance/Building")]
    public sealed class BuildingBalanceAsset : ScriptableObject
    {
        public string DisplayName;
        [Range(1,8)] public int Footprint=2;
        [Min(0)] public int Minerals=100, Gas, Supply;
        [Min(1)] public int Health=500, Vision=7;
        [Min(.1f)] public float ConstructionSeconds=5, ModelHeight=1.4f;
        public BuildingStats Snapshot()=>new BuildingStats{Name=DisplayName,Width=Mathf.Clamp(Footprint,1,8),Minerals=Mathf.Max(0,Minerals),Gas=Mathf.Max(0,Gas),Supply=Mathf.Max(0,Supply),Health=Mathf.Max(1,Health),Vision=Mathf.Clamp(Vision,1,64),BuildSeconds=Mathf.Max(.1f,ConstructionSeconds),Height=Mathf.Max(.1f,ModelHeight)};
    }
}
