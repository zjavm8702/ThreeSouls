using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Growth;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 로비에서 기본, 성장 및 장비를 합산한 능력치를 같은 형식으로 표시합니다.
    /// </summary>
    public static class HeroGrowthPresentation
    {
        public static float GetValue(HeroStat stat, CharacterTableDataRow hero,
            EquipmentBonuses equipment, HeroGrowthBonuses growth)
        {
            return stat switch
            {
                HeroStat.Attack => hero.AttackDmg + equipment.AttackDmg + growth.Attack,
                HeroStat.MaxHealth => hero.MaxHP + equipment.MaxHP + growth.MaxHealth,
                HeroStat.AttackSpeed => equipment.AttackSpeedBonus + growth.AttackSpeed,
                HeroStat.CriticalChance => growth.CriticalChance,
                HeroStat.CriticalDamage => growth.CriticalMultiplier,
                _ => growth.Luck
            };
        }

        public static string Format(HeroStat stat, float value)
        {
            return stat == HeroStat.AttackSpeed || stat == HeroStat.CriticalChance || stat == HeroStat.CriticalDamage
                ? value.ToString("P1") : value.ToString("0.###");
        }
    }
}
