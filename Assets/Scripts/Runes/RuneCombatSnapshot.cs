using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using UnityEngine;

namespace ThreeSouls.Runes
{
    public readonly struct RuneSkillBonuses
    {
        public readonly float DamageMultiplier;
        public readonly float CooldownMultiplier;
        public readonly float RadiusMultiplier;
        public readonly float CastRangeMultiplier;

        internal RuneSkillBonuses(float damage, float cooldown, float radius, float castRange = 1)
        {
            DamageMultiplier = damage;
            CooldownMultiplier = cooldown;
            RadiusMultiplier = radius;
            CastRangeMultiplier = castRange;
        }
    }

    /// <summary>
    /// 전투 준비 시 룬과 성소 규칙을 복사하여 도중의 테이블 변경과 분리합니다.
    /// </summary>
    public sealed class RuneCombatSnapshot
    {
        private readonly Dictionary<string, RuneSkillBonuses> Skills = new(StringComparer.Ordinal);
        public float EquipmentDropMultiplier { get; }
        public float GoldMultiplier { get; }
        public float FutureGemMultiplier { get; }
        public float DebuffRatio { get; }

        internal RuneCombatSnapshot(RuneCollection collection)
        {
            var rule = RuneRules.GetSanctuaryRule();
            EquipmentDropMultiplier = 1 + rule.EquipmentDropBonus + GetShrineGain(collection, RuneEffect.EquipmentDrop);
            GoldMultiplier = 1 + rule.GoldBonus + GetShrineGain(collection, RuneEffect.Gold);
            FutureGemMultiplier = 1 + rule.GemBonus;
            DebuffRatio = Mathf.Max(rule.MinimumDebuffRatio, rule.DebuffRatio - GetShrineGain(collection, RuneEffect.DebuffWard));
            var rows = LobbyTables.Get<SkillTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                var key = collection.GetEquipped("Skill/" + rows[i].Key);
                var definition = key == null ? null : RuneRules.GetDefinition(key);
                var gain = definition == null ? 0 : definition.GainPerRank * collection.GetRank(key);
                Skills.Add(rows[i].Key, new RuneSkillBonuses(
                    definition?.Effect == RuneEffect.Damage || definition?.Effect == RuneEffect.Momentum ? 1 + gain : 1,
                    definition?.Effect == RuneEffect.Cooldown || definition?.Effect == RuneEffect.Momentum ? 1 - Mathf.Min(rule.MaximumCooldownReduction, definition.Effect == RuneEffect.Momentum ? gain * .5f : gain) : 1,
                    definition?.Effect == RuneEffect.Area ? 1 + gain : definition?.Effect == RuneEffect.Reach && rows[i].IsArea ? 1 + gain * .5f : 1,
                    definition?.Effect == RuneEffect.Reach ? 1 + gain : 1));
            }
        }

        public RuneSkillBonuses GetSkill(string skillKey)
        {
            return Skills.TryGetValue(skillKey, out var bonus) == true ? bonus :
                throw new ArgumentException("전투 스냅샷에 없는 스킬입니다.", nameof(skillKey));
        }

        private static float GetShrineGain(RuneCollection collection, RuneEffect effect)
        {
            var key = collection.GetEquipped("Sanctuary/" + effect);
            return key == null ? 0 : RuneRules.GetDefinition(key).GainPerRank * collection.GetRank(key);
        }

        public static int CalculateGold(int amount, float multiplier)
        {
            if (amount < 0 || float.IsNaN(multiplier) == true || float.IsInfinity(multiplier) == true || multiplier < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            }

            return (int)Math.Min(int.MaxValue, Math.Floor(amount * (double)multiplier + 0.000001));
        }
    }
}
