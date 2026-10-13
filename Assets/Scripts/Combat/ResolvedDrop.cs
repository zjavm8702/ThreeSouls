using ThreeSouls.Enumeration;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 전투 드롭 확률과 수량 계산이 완료된 보상입니다.
    /// </summary>
    public readonly struct ResolvedDrop
    {
        public RewardType Type { get; }
        public string Key { get; }
        public int Amount { get; }

        public ResolvedDrop(RewardType type, string key, int amount)
        {
            Type = type;
            Key = key;
            Amount = amount;
        }
    }

}
