using System;
using Order.Content;
using UnityEngine;
namespace Order.Balance
{
    // A faction's unit and building definitions.
    [Serializable]
    public sealed class FactionAssets
    {
        public UnitBalanceAsset Worker, Melee, Ranged, Air;
        // Research: the building that researches upgrades (the humans' workshop, the machines' firmware node).
        public BuildingBalanceAsset Supply, Factory, Headquarters, Research;
        // Machines only: the network relay and the leaper.
        public BuildingBalanceAsset Relay;
        public UnitBalanceAsset Leaper;
        // Humans only: the extractor that stands on a plasma rift, the tank, the howitzer and the interceptor.
        public BuildingBalanceAsset Extractor;
        public UnitBalanceAsset Tank, Artillery, Fighter;
        public bool Complete=>Worker!=null&&Melee!=null&&Ranged!=null&&Air!=null&&Supply!=null&&Factory!=null&&Headquarters!=null&&Research!=null;
    }
    public sealed class BalanceCatalog : ScriptableObject
    {
        // The humans' references stay at the top level, where they were first saved; the machines are grouped.
        public UnitBalanceAsset Worker, Melee, Ranged, Air, Tank, Artillery, Fighter;
        public BuildingBalanceAsset Supply, Factory, Headquarters, Research, Extractor;
        public FactionAssets Resonance=new FactionAssets();
        public EconomyBalanceAsset Economy;
        public MatchBalance Snapshot()
        {
            var expedition=new FactionAssets{Worker=Worker,Melee=Melee,Ranged=Ranged,Air=Air,Supply=Supply,Factory=Factory,Headquarters=Headquarters,Research=Research,Extractor=Extractor,Tank=Tank,Artillery=Artillery,Fighter=Fighter};
            if(!expedition.Complete||expedition.Extractor==null||expedition.Tank==null||expedition.Artillery==null||expedition.Fighter==null||Resonance==null||!Resonance.Complete||Resonance.Relay==null||Resonance.Leaper==null||Economy==null)
                throw new InvalidOperationException("Balance catalog has missing references: run Order/Create Missing Balance Assets.");
            return new MatchBalance{Expedition=Roster(expedition,false),Resonance=Roster(Resonance,true),
                StartingMetal=Mathf.Max(0,Economy.StartingMetal),StartingPlasma=Mathf.Max(0,Economy.StartingPlasma),MetalDeposit=Mathf.Max(1,Economy.MetalDeposit),PlasmaDeposit=Mathf.Max(1,Economy.PlasmaDeposit)};
        }
        static FactionBalance Roster(FactionAssets a,bool machines)=>new FactionBalance{NaniteMining=machines,SelfBuilding=machines,Network=machines,Mends=machines,Repairs=!machines,
            Buildable=machines?new[]{0,1,FactionBalance.Relay,FactionBalance.Main,4}:new[]{0,1,FactionBalance.Main,3,4},Research=machines?4:3,Extractor=machines?-1:4,
            Units=machines?new[]{a.Worker.Snapshot(),a.Melee.Snapshot(),a.Ranged.Snapshot(),a.Air.Snapshot(),a.Leaper.Snapshot()}:new[]{a.Worker.Snapshot(),a.Melee.Snapshot(),a.Ranged.Snapshot(),a.Air.Snapshot(),null,a.Tank.Snapshot(),a.Artillery.Snapshot(),a.Fighter.Snapshot()},
            Buildings=machines?new[]{a.Supply.Snapshot(),a.Factory.Snapshot(),a.Headquarters.Snapshot(),a.Relay.Snapshot(),a.Research.Snapshot()}:new[]{a.Supply.Snapshot(),a.Factory.Snapshot(),a.Headquarters.Snapshot(),a.Research.Snapshot(),a.Extractor.Snapshot()}};
    }
}
