using System;
using System.Collections.Generic;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 승리 전 지급하지 않는 던전 골드와 수집한 장비 수량을 보관합니다.
    /// </summary>
    internal sealed class CombatRewardSummary
    {
        private int Gold;
        private readonly Dictionary<string, int> Items = new();

        internal void Clear()
        {
            Gold = 0;
            Items.Clear();
        }

        internal void AddGold(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Gold = (int)Math.Min(int.MaxValue, (long)Gold + amount);
        }

        internal int GetPendingGold()
        {
            return Gold;
        }

        internal void AddItem(string itemKey)
        {
            if (string.IsNullOrWhiteSpace(itemKey) == true)
            {
                throw new ArgumentException("수집한 장비 키가 필요합니다.", nameof(itemKey));
            }

            Items.TryGetValue(itemKey, out var count);
            Items[itemKey] = checked(count + 1);
        }

        internal CombatResultData CreateResult(int regionNumber, int stageNumber, float elapsedSeconds, int grantedGold,
            int heroExperience = 0, int growthStones = 0, int enhancementStones = 0, string runeReward = "")
        {
            return new CombatResultData(regionNumber, stageNumber, grantedGold, elapsedSeconds, Items, heroExperience, growthStones, enhancementStones, runeReward);
        }
    }
}
