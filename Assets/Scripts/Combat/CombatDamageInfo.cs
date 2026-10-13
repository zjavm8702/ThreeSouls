namespace ThreeSouls.Combat
{
    /// <summary>One resolved hit, shared by HP, text and impact feedback.</summary>
    public readonly struct CombatDamageInfo
    {
        public readonly float Amount;
        public readonly bool IsCritical;
        public readonly bool IsSkill;
        public CombatDamageInfo(float amount, bool isCritical = false, bool isSkill = false)
        { Amount = amount; IsCritical = isCritical; IsSkill = isSkill; }
    }
}
