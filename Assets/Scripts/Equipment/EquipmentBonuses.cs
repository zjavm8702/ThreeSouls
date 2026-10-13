namespace ThreeSouls.Data
{
    /// <summary>
    /// 영웅에게 적용할 장착 장비의 합산 능력치입니다.
    /// </summary>
    public readonly struct EquipmentBonuses
    {
        public float AttackDmg { get; }
        public float MaxHP { get; }
        public float AttackSpeedBonus { get; }

        public EquipmentBonuses(float damage, float hp, float speed)
        {
            AttackDmg = damage;
            MaxHP = hp;
            AttackSpeedBonus = speed;
        }
    }

}
