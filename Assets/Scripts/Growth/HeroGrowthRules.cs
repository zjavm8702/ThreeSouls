using System;
using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Growth
{
    /// <summary>
    /// 성장 테이블의 계약, 레벨별 한도, 강화 비용과 실제 증가량을 계산합니다.
    /// </summary>
    public static class HeroGrowthRules
    {
        private enum GainMode
        {
            BaseRatio,
            Flat
        }

        public static HeroGrowthRuleTableDataRow GetRule()
        {
            return LobbyTables.Get<HeroGrowthRuleTableDataTable>().Items.Single(x => x.Key == "HeroGrowth");
        }

        public static HeroStatUpgradeTableDataRow GetStat(HeroStat stat)
        {
            return LobbyTables.Get<HeroStatUpgradeTableDataTable>().Items.Single(x => x.StatKey == stat.ToString());
        }

        public static int GetExperienceRequired(int level)
        {
            var rule = GetRule();
            return level >= rule.MaxLevel ? 0 : checked(rule.ExperienceBase + (level - 1) * rule.ExperiencePerLevel);
        }

        public static int GetRankLimit(HeroStat stat, int level)
        {
            var row = GetStat(stat);
            if (level < row.UnlockLevel)
            {
                return 0;
            }

            return (int)Math.Min(row.MaximumRank,
                row.InitialRankLimit + (long)(level - row.UnlockLevel) * row.RankLimitPerLevel);
        }

        public static float GetGain(HeroStat stat, CharacterTableDataRow hero)
        {
            var row = GetStat(stat);
            if (GetGainMode(row) == GainMode.BaseRatio)
            {
                return (stat == HeroStat.Attack ? hero.AttackDmg : hero.MaxHP) * row.GainPerRank;
            }

            return row.GainPerRank;
        }

        internal static HeroUpgradeQuote GetQuote(HeroGrowthData hero, HeroStat stat)
        {
            var row = GetStat(stat);
            var rank = hero.GetRank(stat);
            return new HeroUpgradeQuote(hero.HeroKey, stat, rank,
                checked(row.BaseGoldCost + row.GoldCostPerRank * rank),
                checked(row.BaseStoneCost + rank / row.RanksPerStoneStep * row.StoneCostPerStep));
        }

        public static HeroGrowthBonuses Calculate(CharacterTableDataRow hero, HeroGrowthData growth)
        {
            var rule = GetRule();
            return new HeroGrowthBonuses(
                GetGain(HeroStat.Attack, hero) * growth.GetRank(HeroStat.Attack),
                GetGain(HeroStat.MaxHealth, hero) * growth.GetRank(HeroStat.MaxHealth),
                GetGain(HeroStat.AttackSpeed, hero) * growth.GetRank(HeroStat.AttackSpeed),
                rule.BaseCriticalChance + GetGain(HeroStat.CriticalChance, hero) * growth.GetRank(HeroStat.CriticalChance),
                rule.BaseCriticalMultiplier + GetGain(HeroStat.CriticalDamage, hero) * growth.GetRank(HeroStat.CriticalDamage),
                GetGain(HeroStat.Luck, hero) * growth.GetRank(HeroStat.Luck));
        }

        public static float GetLuckDropMultiplier(float averageLuck)
        {
            if (Finite(averageLuck) == false || averageLuck < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(averageLuck));
            }

            var rule = GetRule();
            return 1 + Math.Min(rule.MaximumLuckDropBonus, Math.Max(0, averageLuck) * rule.LuckDropBonusPerPoint);
        }

        public static void Validate()
        {
            var rule = GetRule();
            Require(rule.MaxLevel >= 1 && rule.MaxLevel <= 1000 && rule.ExperienceBase > 0 &&
                rule.ExperiencePerLevel >= 0 &&
                (long)rule.ExperienceBase + (long)rule.ExperiencePerLevel * rule.MaxLevel <= int.MaxValue,
                "레벨/경험치");
            Require(Finite(rule.BaseCriticalChance) == true && rule.BaseCriticalChance >= 0 && rule.BaseCriticalChance <= 1 &&
                Finite(rule.BaseCriticalMultiplier) == true && rule.BaseCriticalMultiplier >= 1 &&
                Finite(rule.LuckDropBonusPerPoint) == true && rule.LuckDropBonusPerPoint >= 0 &&
                Finite(rule.MaximumLuckDropBonus) == true && rule.MaximumLuckDropBonus >= 0 &&
                rule.MaximumLuckDropBonus <= 10, "치명타/행운");
            Require(rule.VictoryExperience > 0 && rule.ExperiencePerStage >= 0 &&
                rule.VictoryGrowthStones >= 0 && rule.StagesPerBonusStone > 0 &&
                rule.TestStartingGrowthStones >= 0, "보상/테스트 재화");
            var rows = LobbyTables.Get<HeroStatUpgradeTableDataTable>().Items;
            Require(rows.Length == 6, "능력치 수");
            for (var i = 0; i < 6; i++)
            {
                var stat = (HeroStat)i;
                var row = GetStat(stat);
                Require(row.UnlockLevel >= 1 && row.UnlockLevel <= rule.MaxLevel &&
                    row.InitialRankLimit > 0 && row.RankLimitPerLevel > 0 &&
                    row.MaximumRank >= row.InitialRankLimit && row.MaximumRank <= 10000, "해금/한도 " + stat);
                var baseRatio = stat == HeroStat.Attack || stat == HeroStat.MaxHealth;
                Require(GetGainMode(row) == (baseRatio == true ? GainMode.BaseRatio : GainMode.Flat) &&
                    Finite(row.GainPerRank) == true && row.GainPerRank > 0,
                    "증가량 " + stat);
                Require(row.BaseGoldCost > 0 && row.GoldCostPerRank >= 0 && row.BaseStoneCost > 0 &&
                    row.RanksPerStoneStep > 0 && row.StoneCostPerStep >= 0 &&
                    (long)row.BaseGoldCost + (long)row.GoldCostPerRank * row.MaximumRank <= int.MaxValue &&
                    (long)row.BaseStoneCost + (long)(row.MaximumRank / row.RanksPerStoneStep) * row.StoneCostPerStep <= int.MaxValue,
                    "비용 " + stat);
            }

            Require(rule.BaseCriticalChance + GetStat(HeroStat.CriticalChance).GainPerRank *
                GetStat(HeroStat.CriticalChance).MaximumRank <= 1, "치명타 확률 상한");
            var starts = LobbyTables.Get<HeroGrowthStartTableDataTable>().Items;
            var heroes = LobbyTables.Get<LobbyHeroTableDataTable>().Items;
            Require(starts.Length == heroes.Length, "고정 영웅 수");
            for (var i = 0; i < heroes.Length; i++)
            {
                var start = starts.Single(x => x.HeroKey == heroes[i].HeroKey);
                Require(start.InitialLevel >= 1 && start.InitialLevel <= rule.MaxLevel && start.InitialExperience >= 0 &&
                    (start.InitialLevel == rule.MaxLevel ? start.InitialExperience == 0 :
                        start.InitialExperience < GetExperienceRequired(start.InitialLevel)), "영웅 초기값");
            }
        }

        private static bool Finite(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false;
        }

        private static GainMode GetGainMode(HeroStatUpgradeTableDataRow row)
        {
            if (Enum.TryParse(row.GainMode, out GainMode mode) == false || Enum.IsDefined(typeof(GainMode), mode) == false)
            {
                throw new InvalidOperationException("지원하지 않는 능력치 증가량 방식: " + row.GainMode);
            }

            return mode;
        }

        private static void Require(bool valid, string name)
        {
            if (valid == false)
            {
                throw new InvalidOperationException("영웅 성장 테이블 오류: " + name);
            }
        }
    }
}
