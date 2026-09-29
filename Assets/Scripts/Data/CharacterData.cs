using System;
using ThreeSouls.Enumeration;
using UnityEngine;

namespace ThreeSouls.Data
{
    [Serializable]
    public class CharacterCombatData
    {
        public CharacterType CharacterType { get; }
        public CharacterRole CharacterRole { get; }
        public AttackType CharacterAttackType { get; }

        /// <summary>
        /// 캐릭터 HP
        /// </summary>
        private float CharHP;
        public float CharHealthPoint
        {
            get => CharHP;
            set => CharHP = Mathf.Max(0, value);
        }

        /// <summary>
        /// 캐릭터 데미지
        /// </summary>
        private float CharDmg;
        public float CharAttackDmg
        {
            get => CharDmg;
            set => CharDmg = Mathf.Max(0, value);
        }

        /// <summary>
        /// 캐릭터 공격 딜레이
        /// </summary>
        private float AttackInterval;
        public float CharAttackInterval
        {
            get => AttackInterval;
            set => AttackInterval = Mathf.Max(value, 0.01f);
        }

        /// <summary>
        /// 
        /// </summary>
        private float MoveSpeed;
        public float CharMoveSpeed
        { 
            get => MoveSpeed;
            set => MoveSpeed = Mathf.Max(value, 1f);
        }

        /// <summary>
        /// 
        /// </summary>
        private float RunningSpeed;
        public float CharRunningSpeed
        { 
            get => RunningSpeed;
            set => RunningSpeed = value;
        }

        /// <summary>
        /// 
        /// </summary>
        private float DistanceToTarget;
        public float CharDistanceToTarget
        { 
            get => DistanceToTarget;
            set => DistanceToTarget = Mathf.Max(value, 0);
        }

        /// <summary>
        /// 
        /// </summary>
        private bool IsRunningPlay;
        public bool CharIsRunningPlay
        { 
            get => IsRunningPlay;
            set => IsRunningPlay = value;
        }

        /// <summary>
        /// 
        /// </summary>
        private float DetectDelay;
        public float CharDetectDelay
        { 
            get => DetectDelay;
            set => DetectDelay = value;
        }

        public CharacterCombatData(CharacterType charType, CharacterRole charRole, AttackType charAttackType)
        {
            CharacterAttackType = charAttackType;
            CharacterType = charType;
            CharacterRole = charRole;
        }
    }
}
