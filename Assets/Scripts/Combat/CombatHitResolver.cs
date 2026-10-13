using ThreeSouls.Character;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 유효한 명중의 치명타를 판정하고 확정 피해량을 체력 담당자에게 전달합니다.
    /// </summary>
    public static class CombatHitResolver
    {
        public static void ApplyDamage(BaseEntity source, BaseEntity target, float damage, bool isSkill = false)
        {
            if (source == null || target == null || source.CharHealth.IsAlive == false ||
                target.CharHealth.IsAlive == false || target.CanTakeDamage() == false)
            {
                return;
            }

            var stats = source.CharacterData;
            var critical = false;
            if (stats.CriticalChance > 0)
            {
                var roll = UnityEngine.Random.value;
                critical = stats.CriticalChance == 1 || roll < stats.CriticalChance;
                damage = CombatCriticalRules.Calculate(damage, stats.CriticalChance,
                    stats.CriticalMultiplier, roll);
            }

            target.CharHealth.ApplyDamage(source, new CombatDamageInfo(damage, critical, isSkill));
        }
    }
}
