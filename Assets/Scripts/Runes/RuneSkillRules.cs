using System.Collections.Generic;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Runes
{
    /// <summary>
    /// 원본 스킬 행을 수정하지 않고 전투와 HUD에서 공유할 룬 적용 수치를 복사합니다.
    /// </summary>
    public static class RuneSkillRules
    {
        public static IReadOnlyList<SkillTableDataRow> CreateSkills(IReadOnlyList<SkillTableDataRow> source, RuneCombatSnapshot snapshot)
        {
            var rows = new SkillTableDataRow[source.Count];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = source[i];
                var bonus = snapshot.GetSkill(row.Key);
                rows[i] = new SkillTableDataRow
                {
                    Key = row.Key,
                    HeroKey = row.HeroKey,
                    InitialDelay = row.InitialDelay * bonus.CooldownMultiplier,
                    Cooldown = row.Cooldown * bonus.CooldownMultiplier,
                    CastRange = row.CastRange * bonus.CastRangeMultiplier,
                    HitDelay = row.HitDelay,
                    DamageMultiplier = row.DamageMultiplier * bonus.DamageMultiplier,
                    IsArea = row.IsArea,
                    CenterOnCaster = row.CenterOnCaster,
                    Radius = row.Radius * bonus.RadiusMultiplier,
                    EffectOffset = row.EffectOffset,
                    SkillIconKey = row.SkillIconKey,
                    AttackClipKey = row.AttackClipKey,
                    EffectPrefabKey = row.EffectPrefabKey
                };
            }

            return System.Array.AsReadOnly(rows);
        }
    }
}
