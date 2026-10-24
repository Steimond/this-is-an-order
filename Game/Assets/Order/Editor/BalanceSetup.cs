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
            var humans=defaults.Expedition;
            catalog.Worker=Unit("Faction1","Worker",humans.Unit(UnitKind.Worker));
            catalog.Melee=Unit("Faction1","Assault",humans.Unit(UnitKind.Melee));
            catalog.Ranged=Unit("Faction1","Rifleman",humans.Unit(UnitKind.Ranged));
            catalog.Air=Unit("Faction1","Gunship",humans.Unit(UnitKind.Air));
            catalog.Tank=Unit("Faction1","Tank",humans.Unit(UnitKind.Tank));
            catalog.Artillery=Unit("Faction1","Howitzer",humans.Unit(UnitKind.Artillery));
            catalog.Fighter=Unit("Faction1","Interceptor",humans.Unit(UnitKind.Fighter));
            catalog.Supply=Building("Faction1","Supply",humans.Buildings[0]);
            catalog.Factory=Building("Faction1","Factory",humans.Buildings[1]);
            catalog.Headquarters=Building("Faction1","Headquarters",humans.Buildings[2]);
            catalog.Research=Building("Faction1","Workshop",humans.Buildings[humans.Research]);
            catalog.Extractor=Building("Faction1","Extractor",humans.Buildings[humans.Extractor]);
            var machines=defaults.Resonance;
            catalog.Resonance??=new FactionAssets();
            catalog.Resonance.Worker=Unit("Faction2","Weaver",machines.Unit(UnitKind.Worker));
            catalog.Resonance.Melee=Unit("Faction2","Spider",machines.Unit(UnitKind.Melee));
            catalog.Resonance.Ranged=Unit("Faction2","Emitter",machines.Unit(UnitKind.Ranged));
            catalog.Resonance.Air=Unit("Faction2","Glider",machines.Unit(UnitKind.Air));
            catalog.Resonance.Leaper=Unit("Faction2","Jumper",machines.Unit(UnitKind.Leaper));
            catalog.Resonance.Supply=Building("Faction2","ComputeNode",machines.Buildings[0]);
            catalog.Resonance.Factory=Building("Faction2","Assembler",machines.Buildings[1]);
            catalog.Resonance.Headquarters=Building("Faction2","Core",machines.Buildings[2]);
            catalog.Resonance.Relay=Building("Faction2","Relay",machines.Buildings[FactionBalance.Relay]);
            catalog.Resonance.Research=Building("Faction2","Firmware",machines.Buildings[machines.Research]);
            catalog.Economy=Asset<EconomyBalanceAsset>("Shared/Economy");
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }
        static UnitBalanceAsset Unit(string faction,string name,UnitStats defaults)
        {
            string relative=$"{faction}/Units/{name}";
            bool exists=AssetDatabase.LoadAssetAtPath<UnitBalanceAsset>($"Assets/Order/Balance/{relative}.asset")!=null;
            var asset=Asset<UnitBalanceAsset>(relative);
            if(!exists)
            {
                asset.DisplayName=defaults.Name;asset.MoveSpeed=defaults.Speed;asset.CollisionRadius=defaults.Radius;asset.Health=defaults.Health;asset.Vision=defaults.Vision;asset.HarvestAmount=defaults.HarvestAmount;asset.HarvestInterval=defaults.HarvestInterval;asset.Supply=defaults.Supply;asset.Flying=defaults.Flying;asset.Damage=defaults.Damage;asset.AttackRange=defaults.AttackRange;asset.AttackCooldown=defaults.AttackCooldown;asset.HitsGround=defaults.HitsGround;asset.HitsAir=defaults.HitsAir;asset.Metal=defaults.Metal;asset.Plasma=defaults.Plasma;asset.TrainSeconds=defaults.TrainSeconds;
                asset.WrapSeconds=defaults.WrapSeconds;asset.Cocoon=defaults.Cocoon;asset.CocoonRegen=defaults.CocoonRegen;asset.Leap=defaults.Leap;asset.Mechanical=defaults.Mechanical;
                asset.Armor=defaults.Armor;asset.Siege=defaults.Siege;asset.MinRange=defaults.MinRange;asset.Splash=defaults.Splash;asset.DeploySeconds=defaults.DeploySeconds;asset.Ramp=defaults.Ramp;asset.RampSeconds=defaults.RampSeconds;
                EditorUtility.SetDirty(asset);
            }
            return asset;
        }
        static BuildingBalanceAsset Building(string faction,string name,BuildingStats defaults)
        {
            string relative=$"{faction}/Buildings/{name}";
            bool exists=AssetDatabase.LoadAssetAtPath<BuildingBalanceAsset>($"Assets/Order/Balance/{relative}.asset")!=null;
            var asset=Asset<BuildingBalanceAsset>(relative);
            if(!exists){asset.DisplayName=defaults.Name;asset.Footprint=defaults.Width;asset.Metal=defaults.Metal;asset.Plasma=defaults.Plasma;asset.Supply=defaults.Supply;asset.Health=defaults.Health;asset.Vision=defaults.Vision;asset.ConstructionSeconds=defaults.BuildSeconds;asset.ModelHeight=defaults.Height;asset.Trains=(UnitKind[])defaults.Trains.Clone();asset.Coverage=defaults.Coverage;asset.Relays=defaults.Relays;asset.Researches=defaults.Researches;asset.Extracts=defaults.Extracts;EditorUtility.SetDirty(asset);}
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
