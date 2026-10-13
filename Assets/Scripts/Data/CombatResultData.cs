using System;
using System.Collections.Generic;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 완료 시 확정된 던전 보상과 전투 정보를 읽기 전용으로 제공합니다.
    /// </summary>
    public sealed class CombatResultData
    {
        public int RegionNumber { get; }
        public int StageNumber { get; }
        public int Gold { get; }
        public float ElapsedSeconds { get; }
        public int ItemCount { get; }
        public int HeroExperience { get; }
        public int GrowthStones { get; }
        public int EnhancementStones { get; }
        public string RuneReward { get; }

        private readonly IReadOnlyList<CombatResultItem> Items;

        internal CombatResultData(int regionNumber, int stageNumber, int gold, float elapsedSeconds,
            Dictionary<string, int> items, int heroExperience = 0, int growthStones = 0, int enhancementStones = 0, string runeReward = "")
        {
            RegionNumber = regionNumber;
            StageNumber = stageNumber;
            Gold = gold;
            ElapsedSeconds = elapsedSeconds;
            HeroExperience = heroExperience;
            GrowthStones = growthStones;
            EnhancementStones = enhancementStones;
            RuneReward = runeReward;

            var snapshot = new CombatResultItem[items.Count];
            var index = 0;
            var totalCount = 0;
            using var iterator = items.GetEnumerator();

            while (iterator.MoveNext() == true)
            {
                var entry = iterator.Current;
                snapshot[index++] = new CombatResultItem(entry.Key, entry.Value);
                totalCount += entry.Value;
            }

            ItemCount = totalCount;
            Items = Array.AsReadOnly(snapshot);
        }

        public IReadOnlyList<CombatResultItem> GetItems()
        {
            return Items;
        }
    }
}
