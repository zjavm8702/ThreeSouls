using System;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 장비 정의 검증, 사용 제한과 능력치 추첨을 처리합니다.
    /// </summary>
    public static class EquipmentRules
    {
        private static bool IsFinite(float value) => float.IsNaN(value) == false && float.IsInfinity(value) == false;

        private static bool IsValidRange(float min, float max) => IsFinite(min) == true && IsFinite(max) == true && min >= 0 && max >= min;

        public static bool IsValidDefinition(EquipmentTableDataRow row)
        {
            return row != null &&
                string.IsNullOrWhiteSpace(row.ItemKey) == false &&
                Enum.IsDefined(typeof(EquipSlot), row.EquipSlot) == true &&
                Enum.IsDefined(typeof(ItemRarity), row.Rarity) == true &&
                IsValidRange(row.AttackDmgMin, row.AttackDmgMax) == true &&
                IsValidRange(row.MaxHPMin, row.MaxHPMax) == true &&
                IsValidRange(row.AttackSpeedBonusMin, row.AttackSpeedBonusMax) == true;
        }

        public static bool CanEquip(EquipmentTableDataRow row, CharacterTableDataRow hero)
        {
            if (IsValidDefinition(row) == false || hero == null || hero.CharacterType != CharacterType.Hero)
            {
                return false;
            }

            var allowed = row.AllowedCharacterKeys;

            return allowed == null || allowed.Length == 0 || Array.IndexOf(allowed, hero.StrKey) >= 0;
        }

        private static float RollValue(float min, float max, Func<double> random)
        {
            double value = random();

            if (double.IsNaN(value) == true || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(random));
            }

            return (float)Math.Round(min + (max - min) * value, 3, MidpointRounding.AwayFromZero);
        }

        public static EquipmentInstance Roll(EquipmentTableDataRow row, Func<double> random,
            EquipmentGrowthData growth = null)
        {
            if (IsValidDefinition(row) == false)
            {
                throw new ArgumentException("Invalid equipment definition.");
            }

            return new EquipmentInstance(
                Guid.NewGuid().ToString("N"),
                row.ItemKey,
                RollValue(row.AttackDmgMin, row.AttackDmgMax, random),
                RollValue(row.MaxHPMin, row.MaxHPMax, random),
                RollValue(row.AttackSpeedBonusMin, row.AttackSpeedBonusMax, random), growth);
        }

        public static float GetAttackInterval(float baseInterval, float baseMultiplier, float bonus)
        {
            return Math.Max(0.01f, baseInterval / Math.Max(0.01f, baseMultiplier * (1 + bonus)));
        }
    }
}
