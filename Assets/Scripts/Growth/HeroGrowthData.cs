using System;
using System.Collections.Generic;

namespace ThreeSouls.Growth
{
    public enum HeroStat
    {
        Attack,
        MaxHealth,
        AttackSpeed,
        CriticalChance,
        CriticalDamage,
        Luck
    }

    /// <summary>
    /// 영웅별 레벨, 현재 레벨 경험치와 능력치 강화 단계를 소유합니다.
    /// </summary>
    public sealed partial class HeroGrowthData
    {
        public string HeroKey { get; }
        public int Level { get; private set; }
        public int Experience { get; private set; }
        private readonly int MaximumLevel;
        private readonly int[] Ranks = new int[6];

        internal HeroGrowthData(string heroKey, int level, int experience, int maximumLevel)
        {
            if (string.IsNullOrWhiteSpace(heroKey) == true || level < 1 || level > maximumLevel || experience < 0)
            {
                throw new ArgumentException("영웅의 초기 성장 데이터를 검증해야 합니다.");
            }

            HeroKey = heroKey;
            Level = level;
            Experience = experience;
            MaximumLevel = maximumLevel;
        }

        public int GetRank(HeroStat stat)
        {
            if (Enum.IsDefined(typeof(HeroStat), stat) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(stat));
            }

            return Ranks[(int)stat];
        }

        internal void IncreaseRank(HeroStat stat, int limit)
        {
            var rank = GetRank(stat);
            if (rank >= limit)
            {
                throw new InvalidOperationException("능력치 강화 한도를 검증한 뒤 적용해야 합니다.");
            }

            Ranks[(int)stat] = rank + 1;
        }

        internal void ApplyExperience(int level, int experience)
        {
            if (level < Level || level > MaximumLevel || experience < 0 ||
                (level == MaximumLevel && experience != 0))
            {
                throw new InvalidOperationException("경험치와 레벨 상한을 검증한 뒤 적용해야 합니다.");
            }

            Level = level;
            Experience = experience;
        }
    }

    /// <summary>
    /// 사용자의 고정 영웅 성장 데이터에 대한 소유권을 보관합니다.
    /// </summary>
    public sealed partial class HeroGrowthCollection
    {
        private readonly Dictionary<string, HeroGrowthData> Heroes = new();

        public HeroGrowthData FindHero(string key)
        {
            return key != null && Heroes.TryGetValue(key, out var hero) == true ? hero : null;
        }

        internal void Add(HeroGrowthData hero)
        {
            Heroes.Add(hero.HeroKey, hero);
        }
    }
}
