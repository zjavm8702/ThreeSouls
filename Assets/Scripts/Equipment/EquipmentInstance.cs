using System;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 식별자와 추첨된 능력치를 생성 시 확정하고 이후 변경을 막는 장비입니다.
    /// </summary>
    public sealed class EquipmentInstance
    {
        public string InstanceId { get; }
        public string ItemKey { get; }
        public float AttackDmg { get; }
        public float MaxHP { get; }
        public float AttackSpeedBonus { get; }
        public EquipmentGrowthData Growth { get; }

        public EquipmentInstance(
            string instanceId,
            string itemKey,
            float attackDmg,
            float maxHP,
            float attackSpeedBonus,
            EquipmentGrowthData growth = null)
        {
            if (string.IsNullOrWhiteSpace(instanceId) == true || string.IsNullOrWhiteSpace(itemKey) == true)
            {
                throw new ArgumentException("장비 식별자와 아이템 키가 필요합니다.");
            }

            if (IsValidStat(attackDmg) == false || IsValidStat(maxHP) == false || IsValidStat(attackSpeedBonus) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(attackDmg), "장비 능력치는 유한한 0 이상의 값이어야 합니다.");
            }

            InstanceId = instanceId;
            ItemKey = itemKey;
            AttackDmg = attackDmg;
            MaxHP = maxHP;
            AttackSpeedBonus = attackSpeedBonus;
            Growth = growth ?? new EquipmentGrowthData();
        }

        private static bool IsValidStat(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false && value >= 0;
        }
    }
}
