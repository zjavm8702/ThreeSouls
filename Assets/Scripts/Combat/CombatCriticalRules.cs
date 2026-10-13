using System;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 일반 공격과 피해 스킬에 공통으로 적용할 치명타 피해를 계산합니다.
    /// </summary>
    public static class CombatCriticalRules
    {
        public static float Calculate(float damage, float chance, float multiplier, float roll)
        {
            if (float.IsNaN(damage) == true || float.IsInfinity(damage) == true || damage < 0 ||
                float.IsNaN(chance) == true || chance < 0 || chance > 1 ||
                float.IsNaN(multiplier) == true || float.IsInfinity(multiplier) == true || multiplier < 1 ||
                float.IsNaN(roll) == true || roll < 0 || roll > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "치명타 계산 입력이 잘못되었습니다.");
            }

            var result = chance > 0 && (chance == 1 || roll < chance) ? damage * multiplier : damage;
            if (float.IsInfinity(result) == true)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "치명타 피해량 상한을 초과했습니다.");
            }

            return result;
        }
    }
}
