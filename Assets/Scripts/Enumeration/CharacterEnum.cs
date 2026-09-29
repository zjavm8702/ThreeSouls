namespace ThreeSouls.Enumeration
{
    public enum AnimatorStateParams
    {
        Idle,
        Ready,
        Walking,
        Running,
        Attack,
        Dead,
        Hit,
    }

    public enum CharacterAnimatorParams
    {
        Action,
        Idle,
        Ready,
        Walking,
        Running,
        Moving,
        GetDown,
        GetUp,
        Jumping,
        Falling,
        Landed,
        Blocking,
        Crawling,
        Climbing,
        Grounded,
        Attack,
        Push,
        Jab,
        Slash,
        Shot,
        Fire1H,
        Fire2H,
        Hit,
        Heal,
        Dead
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
}