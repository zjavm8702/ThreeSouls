using ThreeSouls.Enumeration.AddressableKey;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using UnityEngine;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 생성된 로비 테이블의 주소와 필수 행 조회를 제공합니다.
    /// </summary>
    public static class LobbyTables
    {
        private static readonly IReadOnlyDictionary<Type, ScriptableObjectDatas> TableKeys =
            new Dictionary<Type, ScriptableObjectDatas>
            {
                [typeof(CampaignStageTableDataTable)] = ScriptableObjectDatas.CampaignStageTableData,
                [typeof(CampaignEquipmentLootTableDataTable)] = ScriptableObjectDatas.CampaignEquipmentLootTableData,
                [typeof(CampaignRuneLootTableDataTable)] = ScriptableObjectDatas.CampaignRuneLootTableData,
                [typeof(EquipmentUpgradeTableDataTable)] = ScriptableObjectDatas.EquipmentUpgradeTableData,
                [typeof(EquipmentGrowthRuleTableDataTable)] = ScriptableObjectDatas.EquipmentGrowthRuleTableData,
                [typeof(EquipmentDismantleTableDataTable)] = ScriptableObjectDatas.EquipmentDismantleTableData,
                [typeof(ConsumableTableDataTable)] = ScriptableObjectDatas.ConsumableTableData,
                [typeof(SupplyRuleTableDataTable)] = ScriptableObjectDatas.SupplyRuleTableData,
                [typeof(AutoHealTableDataTable)] = ScriptableObjectDatas.AutoHealTableData,
                [typeof(LobbyRuleTableDataTable)] = ScriptableObjectDatas.LobbyRuleTableData,
                [typeof(LobbyTestStartTableDataTable)] = ScriptableObjectDatas.LobbyTestStartTableData,
                [typeof(LobbyStartingEquipmentTableDataTable)] = ScriptableObjectDatas.LobbyStartingEquipmentTableData,
                [typeof(LobbyHeroTableDataTable)] = ScriptableObjectDatas.LobbyHeroTableData,
                [typeof(VillagePartyTableDataTable)] = ScriptableObjectDatas.VillagePartyTableData,
                [typeof(LobbyOptionTableDataTable)] = ScriptableObjectDatas.LobbyOptionTableData,
                [typeof(EquipmentShopTableDataTable)] = ScriptableObjectDatas.EquipmentShopTableData,
                [typeof(VillageShopTableDataTable)] = ScriptableObjectDatas.VillageShopTableData,
                [typeof(HeroGrowthRuleTableDataTable)] = ScriptableObjectDatas.HeroGrowthRuleTableData,
                [typeof(HeroStatUpgradeTableDataTable)] = ScriptableObjectDatas.HeroStatUpgradeTableData,
                [typeof(HeroGrowthStartTableDataTable)] = ScriptableObjectDatas.HeroGrowthStartTableData,
                [typeof(RuneTableDataTable)] = ScriptableObjectDatas.RuneTableData,
                [typeof(StoryQuestTableDataTable)] = ScriptableObjectDatas.StoryQuestTableData,
                [typeof(RuneShopRuleTableDataTable)] = ScriptableObjectDatas.RuneShopRuleTableData,
                [typeof(RuneUpgradeTableDataTable)] = ScriptableObjectDatas.RuneUpgradeTableData,
                [typeof(SanctuaryRuneRuleTableDataTable)] = ScriptableObjectDatas.SanctuaryRuneRuleTableData,
                [typeof(SkillTableDataTable)] = ScriptableObjectDatas.SkillTableData,
                [typeof(DungeonMapTableDataTable)] = ScriptableObjectDatas.DungeonMapTableData,
                [typeof(DungeonStageSelectionTableDataTable)] = ScriptableObjectDatas.DungeonStageSelectionTableData,
                [typeof(CharacterTableDataTable)] = ScriptableObjectDatas.CharacterTableData,
                [typeof(StringTableDataTable)] = ScriptableObjectDatas.StringTableData,
                [typeof(EquipmentTableDataTable)] = ScriptableObjectDatas.EquipmentTableData,
            };

        public static T Get<T>() where T : ScriptableObject
        {
            var address = GetAddress(typeof(T));
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(address)
                    ?? throw new InvalidOperationException("로비 테이블을 생성해야 합니다: " + address);
            }
#endif
            return AddressableManager.Get().LoadAsset<T>(address)
                ?? throw new InvalidOperationException("로비 테이블을 선행 로드해야 합니다: " + address);
        }

        public static string GetAddress(Type tableType)
        {
            if (TableKeys.TryGetValue(tableType, out var key) == false)
            {
                throw new ArgumentException("등록되지 않은 로비 테이블 타입입니다: " + tableType, nameof(tableType));
            }

            return key.ToAddress();
        }

        public static Type[] GetTableTypes()
        {
            return new[] { typeof(CampaignStageTableDataTable), typeof(CampaignEquipmentLootTableDataTable), typeof(CampaignRuneLootTableDataTable), typeof(EquipmentUpgradeTableDataTable), typeof(EquipmentGrowthRuleTableDataTable),
                typeof(EquipmentDismantleTableDataTable), typeof(ConsumableTableDataTable),
                typeof(SupplyRuleTableDataTable), typeof(AutoHealTableDataTable), typeof(LobbyRuleTableDataTable),
                typeof(LobbyTestStartTableDataTable), typeof(LobbyStartingEquipmentTableDataTable),
                typeof(LobbyHeroTableDataTable), typeof(VillagePartyTableDataTable), typeof(LobbyOptionTableDataTable),
                typeof(EquipmentShopTableDataTable), typeof(VillageShopTableDataTable),
                typeof(HeroGrowthRuleTableDataTable), typeof(HeroStatUpgradeTableDataTable),
                typeof(HeroGrowthStartTableDataTable), typeof(RuneTableDataTable),
                typeof(StoryQuestTableDataTable), typeof(RuneShopRuleTableDataTable),
                typeof(RuneUpgradeTableDataTable), typeof(SanctuaryRuneRuleTableDataTable),
                typeof(SkillTableDataTable), typeof(DungeonMapTableDataTable),
                typeof(DungeonStageSelectionTableDataTable) };
        }

        public static IEnumerator PreLoad(AddressableManager assets)
        {
            var types = GetTableTypes();
            yield return assets.PreLoadBatch<ScriptableObject>(types.Select(GetAddress), false);
        }

        public static LobbyRuleTableDataRow GetRules()
        {
            return Get<LobbyRuleTableDataTable>().Items.Single(x => x.Key == "TestVillage");
        }

        public static SupplyRuleTableDataRow GetSupplyRules()
        {
            return Get<SupplyRuleTableDataTable>().Items.Single(x => x.Key == "LobbySupply");
        }

        public static VillagePartyTableDataRow GetVillageRules()
        {
            return Get<VillagePartyTableDataTable>().Items.Single(x => x.Key == "TestVillage");
        }

        public static string GetText(string key)
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<StringTableDataTable>(ScriptableObjectDatas.StringTableData.ToAddress())
                    .Items.Single(x => x.StrKey == key).KrValue;
            }
#endif
            return TableManager.Get().GetStringTableDataTable().Single(x => x.StrKey == key).KrValue;
        }

        public static CharacterTableDataRow GetCharacter(string key)
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                return Get<CharacterTableDataTable>().Items.FirstOrDefault(x => x.StrKey == key);
            }
#endif
            return TableManager.Get().GetCharacterTableDataTable(key);
        }

        public static EquipmentTableDataRow GetEquipment(string key)
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                return Get<EquipmentTableDataTable>().Items.FirstOrDefault(x => x.ItemKey == key);
            }
#endif
            return TableManager.Get().GetEquipmentTableData(key);
        }

        public static LobbyOptionTableDataRow GetOption(string category, string key)
        {
            return Get<LobbyOptionTableDataTable>().Items.Single(x => x.Category == category && x.OptionKey == key);
        }
    }
}
