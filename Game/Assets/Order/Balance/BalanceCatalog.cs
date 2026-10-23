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
        public BuildingBalanceAsset Supply, Factory, Headquarters;
        public bool Complete=>Worker!=null&&Melee!=null&&Ranged!=null&&Air!=null&&Supply!=null&&Factory!=null&&Headquarters!=null;
    }
    public sealed class BalanceCatalog : ScriptableObject
    {
        // The humans' references stay at the top level, where they were first saved; the machines are grouped.
        public UnitBalanceAsset Worker, Melee, Ranged, Air;
        public BuildingBalanceAsset Supply, Factory, Headquarters;
        public FactionAssets Resonance=new FactionAssets();
        public EconomyBalanceAsset Economy;
        public MatchBalance Snapshot()
        {
            var expedition=new FactionAssets{Worker=Worker,Melee=Melee,Ranged=Ranged,Air=Air,Supply=Supply,Factory=Factory,Headquarters=Headquarters};
            if(!expedition.Complete||Resonance==null||!Resonance.Complete||Economy==null)
                throw new InvalidOperationException("Balance catalog has missing references: run Order/Create Missing Balance Assets.");
            return new MatchBalance{Expedition=Roster(expedition,false),Resonance=Roster(Resonance,true),
                StartingMetal=Mathf.Max(0,Economy.StartingMetal),StartingPlasma=Mathf.Max(0,Economy.StartingPlasma),MetalDeposit=Mathf.Max(1,Economy.MetalDeposit),PlasmaDeposit=Mathf.Max(1,Economy.PlasmaDeposit)};
        }
        static FactionBalance Roster(FactionAssets a,bool machines)=>new FactionBalance{NaniteMining=machines,SelfBuilding=machines,
            Units=new[]{a.Worker.Snapshot(),a.Melee.Snapshot(),a.Ranged.Snapshot(),a.Air.Snapshot()},Buildings=new[]{a.Supply.Snapshot(),a.Factory.Snapshot(),a.Headquarters.Snapshot()}};
    }
}
