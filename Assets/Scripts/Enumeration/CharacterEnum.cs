namespace ThreeSouls.Enumeration
{
    public enum AnimatorStateParams
    {
        Idle,
        Walking,
        Running,
        Attack,
        Dead,
        Hit,
    }

    public enum ProjectileType
    {
        Arrow,
    }

    public enum CharacterParticleType
    {
        Move,
        Jump
    }

    public enum CharacterType
    {
        Hero,
        Monster
    }

    public enum RewardType
    {
        Gold = 0,
        Item = 1,
    }

    public enum AttackType
    {
        Single = 0,
        SingleBow = 1,
        SingleStaff = 2,
    }

    public enum CharacterRole
    {
        Tank = 0,
        RangedDealer = 1,
        MeleeDealer = 2,
        MonsterMeleeDealer =3,
    }

    public enum ClassType
    { 
        Knight,
        Archer,
        Wizard,
    }
}