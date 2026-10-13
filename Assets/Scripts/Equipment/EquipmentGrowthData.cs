using System;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 장비의 원본 추첨값과 분리된 강화 단계, 투자량과 잠금 상태를 보호합니다.
    /// </summary>
    public sealed class EquipmentGrowthData
    {
        public int Level { get; private set; }
        public int InvestedStones { get; private set; }
        public bool Locked { get; private set; }

        public EquipmentGrowthData()
        {
        }

        internal EquipmentGrowthData(int level, int investedStones, bool locked, int maximumLevel)
        {
            if (level < 0 || level > maximumLevel || investedStones < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            Level = level;
            InvestedStones = investedStones;
            Locked = locked;
        }

        internal void Upgrade(int stones, int maximumLevel)
        {
            if (Level >= maximumLevel || stones <= 0 || (long)InvestedStones + stones > int.MaxValue)
            {
                throw new InvalidOperationException("검증된 다음 강화 단계와 투자량이 필요합니다.");
            }

            Level++;
            InvestedStones += stones;
        }

        internal void SetLocked(bool locked)
        {
            Locked = locked;
        }
    }
}
