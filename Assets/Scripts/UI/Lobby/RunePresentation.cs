using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Runes;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 룬의 현재·다음 효과와 스킬 원본 비교를 플레이어용 문장으로 구성합니다.
    /// </summary>
    public static class RunePresentation
    {
        public static string Describe(RuneTableDataRow row, int rank)
        {
            if (row.Enabled == false)
            {
                return "보석 보상 시스템 준비 중\n제작·강화·장착은 추후 제공됩니다. 현재 비용이 들지 않습니다.";
            }

            var displayedRank = System.Math.Max(1, rank);
            var gain = row.GainPerRank * displayedRank;
            var rule = RuneRules.GetSanctuaryRule();
            if (row.Category == RuneCategory.Sanctuary)
            {
                return row.Effect switch
                {
                    RuneEffect.EquipmentDrop => $"성소 장비 드롭 확률\n기본 +{rule.EquipmentDropBonus:P0} → 장착 시 +{rule.EquipmentDropBonus + gain:P0}\n기존 특별 배율·행운과 곱연산, 확률 상한 100%",
                    RuneEffect.Gold => $"성소 몬스터 골드 보상\n기본 +{rule.GoldBonus:P0} → 장착 시 +{rule.GoldBonus + gain:P0}\n몬스터별 소수점 버림 · 승리 정산 때 지급",
                    _ => $"성소 무작위 부대 디버프 완화\n기본 -{rule.DebuffRatio:P0} → 장착 시 -{UnityEngine.Mathf.Max(rule.MinimumDebuffRatio, rule.DebuffRatio - gain):P0}\n공격력·공격속도·최대 체력 중 하나 / 성소 전투 종료 시 해제"
                };
            }

            var rows = LobbyTables.Get<SkillTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                var skill = rows[i];
                if (skill.Key != row.SkillKey)
                {
                    continue;
                }

                return row.Effect switch
                {
                    RuneEffect.Momentum => $"스킬 피해 +{gain:P1} · 쿨타임 -{gain * .5f:P1}\n피해와 순환을 함께 높입니다.\n피해·쿨타임 특화 룬과 같은 슬롯을 사용합니다.",
                    RuneEffect.Reach => $"스킬 사거리 +{gain:P0}" + (skill.IsArea ? $" · 반경 +{gain * .5f:P0}" : "") + "\n더 먼 적에게 스킬을 사용합니다.\n피해·쿨타임은 기본값을 유지합니다.",
                    RuneEffect.Damage => $"스킬 피해 배율\n공격력 ×{skill.DamageMultiplier:0.##} → ×{skill.DamageMultiplier * (1 + gain):0.##}\n쿨타임 {skill.Cooldown:0.##}초 · 기본 공격 유지",
                    RuneEffect.Cooldown => $"스킬 순환 속도\n쿨타임 {skill.Cooldown:0.##}초 → {skill.Cooldown * (1 - UnityEngine.Mathf.Min(rule.MaximumCooldownReduction, gain)):0.##}초\n첫 사용 딜레이에도 적용 · 피해 배율 ×{skill.DamageMultiplier:0.##}",
                    _ => $"스킬 범위 반경\n{skill.Radius:0.##} → {skill.Radius * (1 + gain):0.##}\n연출과 명중 범위 함께 확장 · 피해 배율 ×{skill.DamageMultiplier:0.##}"
                };
            }

            throw new System.InvalidOperationException("룬 표시 대상 스킬이 없습니다.");
        }

        public static string GetName(string key)
        {
            return key == null ? "미장착" : RuneRules.GetDefinition(key).DisplayName;
        }
    }
}
