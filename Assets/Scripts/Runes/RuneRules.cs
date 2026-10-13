using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Runes
{
    /// <summary>
    /// 룬 테이블의 참조·수치 검증과 슬롯 및 강화 비용 계산을 담당합니다.
    /// </summary>
    public static class RuneRules
    {
        public static RuneTableDataRow GetDefinition(string key)
        {
            var rows = LobbyTables.Get<RuneTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Key == key)
                {
                    return rows[i];
                }
            }

            return null;
        }

        public static SanctuaryRuneRuleTableDataRow GetSanctuaryRule()
        {
            var rows = LobbyTables.Get<SanctuaryRuneRuleTableDataTable>().Items;
            if (rows.Length != 1 || rows[0].Key != "Sanctuary")
            {
                throw new InvalidOperationException("성소 룬 규칙은 Sanctuary 한 행이어야 합니다.");
            }

            return rows[0];
        }

        public static string GetSocket(RuneTableDataRow row)
        {
            return row.Category == RuneCategory.Sanctuary ? "Sanctuary/" + row.Effect : "Skill/" + row.SkillKey;
        }

        public static RuneUpgradeTableDataRow GetUpgrade(int rank)
        {
            var rows = LobbyTables.Get<RuneUpgradeTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Rank == rank)
                {
                    return rows[i];
                }
            }

            throw new InvalidOperationException("룬 강화 비용 누락: " + rank);
        }

        public static void Validate()
        {
            var rule = GetSanctuaryRule();
            if (rule.MaximumRuneRank < 1 || rule.MaximumRuneRank > 20 ||
                ValidRatio(rule.EquipmentDropBonus) == false || ValidRatio(rule.GoldBonus) == false ||
                ValidRatio(rule.GemBonus) == false || ValidRatio(rule.DebuffRatio) == false ||
                rule.DebuffRatio <= 0 || rule.DebuffRatio >= 1 ||
                ValidRatio(rule.MinimumDebuffRatio) == false || rule.MinimumDebuffRatio > rule.DebuffRatio ||
                ValidRatio(rule.MaximumCooldownReduction) == false || rule.MaximumCooldownReduction >= 1)
            {
                throw new InvalidOperationException("성소 룬 규칙의 범위와 유한한 수치를 확인하세요.");
            }

            var costs = LobbyTables.Get<RuneUpgradeTableDataTable>().Items;
            var ranks = new HashSet<int>();
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.Rank < 2 || cost.Rank > rule.MaximumRuneRank || ranks.Add(cost.Rank) == false ||
                    cost.Gold < 0 || cost.Stones < 0)
                {
                    throw new InvalidOperationException("룬 강화 단계·비용 오류");
                }
            }

            if (costs.Length != rule.MaximumRuneRank - 1)
            {
                throw new InvalidOperationException("모든 룬 단계의 강화 비용이 필요합니다.");
            }

            var definitions = LobbyTables.Get<RuneTableDataTable>().Items;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var shrineEffects = new HashSet<RuneEffect>();
            var skills = LobbyTables.Get<SkillTableDataTable>().Items;
            for (var i = 0; i < definitions.Length; i++)
            {
                var row = definitions[i];
                if (string.IsNullOrWhiteSpace(row.Key) == true || keys.Add(row.Key) == false ||
                    string.IsNullOrWhiteSpace(row.DisplayName) == true || ValidRatio(row.GainPerRank) == false ||
                    row.GainPerRank * rule.MaximumRuneRank > 1 || row.CraftGold < 0 || row.CraftStones < 0 ||
                    Enum.IsDefined(typeof(RuneCategory), row.Category) == false ||
                    Enum.IsDefined(typeof(RuneEffect), row.Effect) == false)
                {
                    throw new InvalidOperationException("룬 정의·중복 키·비용 오류: " + row?.Key);
                }

                if (row.Category == RuneCategory.Sanctuary)
                {
                    if (row.Effect > RuneEffect.DebuffWard || string.IsNullOrEmpty(row.SkillKey) == false ||
                        shrineEffects.Add(row.Effect) == false || row.Effect == RuneEffect.Gems && row.Enabled == true)
                    {
                        throw new InvalidOperationException("성소는 3개 보상과 수호룬을 사용하며 보석은 준비 중입니다.");
                    }

                    continue;
                }

                SkillTableDataRow skill = null;
                for (var j = 0; j < skills.Length; j++)
                {
                    if (skills[j].Key == row.SkillKey)
                    {
                        skill = skills[j];
                        break;
                    }
                }

                if (skill == null || row.Effect < RuneEffect.Damage ||
                    row.Effect == RuneEffect.Area && skill.IsArea == false)
                {
                    throw new InvalidOperationException("룬의 대상 스킬·효과 오류: " + row.Key);
                }
            }

            if (shrineEffects.Count != 4)
            {
                throw new InvalidOperationException("성소 버프 3개와 디버프 완화 룬이 필요합니다.");
            }
        }

        private static bool ValidRatio(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false && value >= 0 && value <= 1;
        }
    }
}
