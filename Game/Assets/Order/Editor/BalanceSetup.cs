using Order.Balance;
using Order.Content;
using UnityEditor;
using UnityEngine;
namespace Order.Editor
{
    public static class BalanceSetup
    {
        [MenuItem("Order/Create Missing Balance Assets")]
        public static void Create()
        {
            var defaults=new MatchBalance();
            var catalog=Asset<BalanceCatalog>("Resources/PrototypeBalanceCatalog");
            catalog.Worker=Asset<UnitBalanceAsset>("Faction1/Units/Worker");
            catalog.Economy=Asset<EconomyBalanceAsset>("Shared/Economy");
            catalog.Supply=Building("Supply",defaults.Buildings[0]);
            catalog.Factory=Building("Factory",defaults.Buildings[1]);
            catalog.Headquarters=Building("Headquarters",defaults.Buildings[2]);
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }
        static BuildingBalanceAsset Building(string name,BuildingStats defaults)
        {
            string path=$"Assets/Order/Balance/Faction1/Buildings/{name}.asset";
            bool exists=AssetDatabase.LoadAssetAtPath<BuildingBalanceAsset>(path)!=null;
            var asset=Asset<BuildingBalanceAsset>($"Faction1/Buildings/{name}");
            if(!exists){asset.DisplayName=defaults.Name;asset.Footprint=defaults.Width;asset.Minerals=defaults.Minerals;asset.Gas=defaults.Gas;asset.Supply=defaults.Supply;asset.Health=defaults.Health;asset.Vision=defaults.Vision;asset.ConstructionSeconds=defaults.BuildSeconds;asset.ModelHeight=defaults.Height;EditorUtility.SetDirty(asset);}
            return asset;
        }
        static T Asset<T>(string relative) where T:ScriptableObject
        {
            string path="Assets/Order/Balance/"+relative+".asset";
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;
            string directory=System.IO.Path.GetDirectoryName(path).Replace('\\','/');
            string current="Assets";foreach(string part in directory.Substring(7).Split('/')){string next=current+"/"+part;if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,part);current=next;}
            asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;
        }
    }
}
