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
            catalog.Worker=Unit("Worker",defaults.Units[(int)UnitKind.Worker]);
            catalog.Melee=Unit("Assault",defaults.Units[(int)UnitKind.Melee]);
            catalog.Ranged=Unit("Rifleman",defaults.Units[(int)UnitKind.Ranged]);
            catalog.Air=Unit("Gunship",defaults.Units[(int)UnitKind.Air]);
            catalog.Economy=Asset<EconomyBalanceAsset>("Shared/Economy");
            catalog.Supply=Building("Supply",defaults.Buildings[0]);
            catalog.Factory=Building("Factory",defaults.Buildings[1]);
            catalog.Headquarters=Building("Headquarters",defaults.Buildings[2]);
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }
        static UnitBalanceAsset Unit(string name,UnitStats defaults)
        {
            string path=$"Assets/Order/Balance/Faction1/Units/{name}.asset";
            bool exists=AssetDatabase.LoadAssetAtPath<UnitBalanceAsset>(path)!=null;
            var asset=Asset<UnitBalanceAsset>($"Faction1/Units/{name}");
            if(!exists){asset.DisplayName=defaults.Name;asset.MoveSpeed=defaults.Speed;asset.CollisionRadius=defaults.Radius;asset.Health=defaults.Health;asset.Vision=defaults.Vision;asset.HarvestAmount=defaults.HarvestAmount;asset.HarvestInterval=defaults.HarvestInterval;asset.Supply=defaults.Supply;asset.Flying=defaults.Flying;asset.Damage=defaults.Damage;asset.AttackRange=defaults.AttackRange;asset.AttackCooldown=defaults.AttackCooldown;asset.HitsGround=defaults.HitsGround;asset.HitsAir=defaults.HitsAir;EditorUtility.SetDirty(asset);}
            return asset;
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
