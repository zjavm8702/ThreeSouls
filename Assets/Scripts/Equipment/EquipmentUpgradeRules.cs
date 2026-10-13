using System;
using System.Linq;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 강화 비용과 슬롯별 보정 및 분해 보상을 테이블로 계산합니다.
    /// </summary>
    public static class EquipmentUpgradeRules
    {
        public static int GetMaxLevel()
        {
            return LobbyTables.GetRules().EquipmentMaxLevel;
        }

        public static bool TryGetCost(int currentLevel, out int gold, out int stones)
        {
            gold = 0;
            stones = 0;
            if (currentLevel < 0 || currentLevel >= GetMaxLevel())
            {
                return false;
            }
            var row = LobbyTables.Get<EquipmentUpgradeTableDataTable>().Items.Single(x => x.FromLevel == currentLevel);
            gold = row.GoldCost;
            stones = row.StoneCost;
            return true;
        }

        public static EquipmentBonuses Calculate(EquipmentInstance item, EquipSlot slot, int level)
        {
            if (item == null || level < 0 || level > GetMaxLevel())
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            var row = LobbyTables.Get<EquipmentGrowthRuleTableDataTable>().Items.Single(x => x.EquipSlot == slot);
            var speed = item.AttackSpeedBonus;
            if (row.RequirePositiveBaseSpeed == false || speed > 0)
            {
                speed += row.SpeedBonusPerLevel * level;
            }
            return new EquipmentBonuses(item.AttackDmg * (1 + row.AttackRatePerLevel * level),
                item.MaxHP * (1 + row.HPRatePerLevel * level), speed);
        }

        public static long GetDismantleStones(ItemRarity rarity, int investedStones)
        {
            if (investedStones < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(investedStones));
            }
            var row = LobbyTables.Get<EquipmentDismantleTableDataTable>().Items.Single(x => x.Rarity == rarity);
            var reward = row.BaseStoneReward + decimal.Floor(investedStones * (decimal)row.InvestedStoneReturnRate);
            return checked((long)reward);
        }

        public static float GetDismantleReturnRate(ItemRarity rarity)
        {
            return LobbyTables.Get<EquipmentDismantleTableDataTable>().Items.Single(x => x.Rarity == rarity).InvestedStoneReturnRate;
        }
    }
}
