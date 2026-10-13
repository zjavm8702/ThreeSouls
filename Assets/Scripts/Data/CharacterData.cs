using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using UnityEngine;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 한 번의 스폰에 속한 공격과 이동 수치를 보관합니다. 현재 체력은 EntityHealth가 소유합니다.
    /// </summary>
    public sealed class CharacterCombatData
    {
        public string CharacterKey { get; }
        public CharacterType CharacterType { get; }
        public CharacterRole CharacterRole { get; }
        public AttackType CharacterAttackType { get; }
        public float CharAttackDmg { get; private set; }
        public float CharAttackInterval { get; private set; }
        public float CharMoveSpeed { get; }
        public float CharRunningSpeed { get; }
        public float CharDistanceToTarget { get; }
        public bool CharIsRunningPlay { get; }
        public float CharDetectDelay { get; }
        public float CharAttackRange { get; }
        public float CharTargetSearchRadius { get; }
        public float CriticalChance { get; private set; }
        public float CriticalMultiplier { get; private set; } = 1;

        internal CharacterCombatData(CharacterTableDataRow character, ThreeSoulsRuleDataRow rule)
        {
            CharacterKey = character.StrKey;
            CharacterType = character.CharacterType;
            CharacterRole = character.CharacterRole;
            CharacterAttackType = character.AttackType;
            SetAttackStats(character.AttackDmg, rule.BaseAttackInterval / character.AttackSpeedMultiplier);
            CharMoveSpeed = Mathf.Max(1, character.MoveSpeed);
            CharRunningSpeed = character.RunningSpeed;
            CharDistanceToTarget = Mathf.Max(0, character.DistanceToTarget);
            CharIsRunningPlay = character.IsRunningPlay;
            CharDetectDelay = character.DetectDelay;
            CharAttackRange = character.AttackRange;
            CharTargetSearchRadius = character.TargetSearchRadius;
        }


        internal void SetAttackStats(float attackDamage, float attackInterval)
        {
            CharAttackDmg = Mathf.Max(0, attackDamage);
            CharAttackInterval = Mathf.Max(0.01f, attackInterval);
        }

        internal void SetCriticalStats(float chance, float multiplier)
        {
            if (float.IsNaN(chance) == true || chance < 0 || chance > 1 ||
                float.IsNaN(multiplier) == true || float.IsInfinity(multiplier) == true || multiplier < 1)
            {
                throw new System.ArgumentOutOfRangeException(nameof(chance));
            }

            CriticalChance = chance;
            CriticalMultiplier = multiplier;
        }
    }
}
