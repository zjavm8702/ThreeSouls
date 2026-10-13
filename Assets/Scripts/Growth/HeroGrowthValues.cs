namespace ThreeSouls.Growth
{
    /// <summary>
    /// 승리 시 확정된 영웅당 경험치와 사용자 성장석 보상입니다.
    /// </summary>
    public readonly struct HeroGrowthReward
    {
        public readonly int Experience;
        public readonly int Stones;

        internal HeroGrowthReward(int experience, int stones)
        {
            Experience = experience;
            Stones = stones;
        }
    }

    /// <summary>
    /// 영웅이 전투에 적용할 성장 보너스의 읽기 전용 값입니다.
    /// </summary>
    public readonly struct HeroGrowthBonuses
    {
        public readonly float Attack;
        public readonly float MaxHealth;
        public readonly float AttackSpeed;
        public readonly float CriticalChance;
        public readonly float CriticalMultiplier;
        public readonly float Luck;

        internal HeroGrowthBonuses(float attack, float health, float speed, float criticalChance,
            float criticalMultiplier, float luck)
        {
            Attack = attack;
            MaxHealth = health;
            AttackSpeed = speed;
            CriticalChance = criticalChance;
            CriticalMultiplier = criticalMultiplier;
            Luck = luck;
        }
    }

    /// <summary>
    /// 확인창이 표시한 강화 단계와 비용을 거래 시 다시 검증하기 위한 견적입니다.
    /// </summary>
    public readonly struct HeroUpgradeQuote
    {
        public readonly string HeroKey;
        public readonly HeroStat Stat;
        public readonly int Rank;
        public readonly int Gold;
        public readonly int Stones;

        internal HeroUpgradeQuote(string key, HeroStat stat, int rank, int gold, int stones)
        {
            HeroKey = key;
            Stat = stat;
            Rank = rank;
            Gold = gold;
            Stones = stones;
        }
    }
}
