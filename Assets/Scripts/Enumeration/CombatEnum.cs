namespace ThreeSouls.Enumeration
{
    public enum CombatRoundPhase
    {
        Preparing,
        RoundCombat,
        HolyPlaceFormation,
        HolyPlaceCombat,
        DepartureFormation,
        Completed,
        Defeated,
        RallyFormation,
        TravelToHolyPlace,
        HolyPlaceCleanup,
        HolyPlaceWaiting
    }

    public enum MovePathResult
    {
        Pending,
        Found,
        NotFound
    }

    public enum CombatState
    {
        Wait = 0,
        MoveToDestination = 1,
        DetectTarget = 2,
        MoveToTarget = 3,
        Attack = 4,
        Dead = 5,
        None = 6
    }
}
