using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using ThreeSouls.Weapon;
using Sky.Utility;
using ThreeSouls.Data;
using UnityEngine;
using UnityEngine.U2D.Animation;
using System.Linq;
using ThreeSouls.Data.Generated;
using ThreeSouls.Global;
using System.Collections.Generic;
using System;
using ThreeSouls.Combat;

namespace ThreeSouls.Character
{
    public abstract class BaseEntity : MonoBehaviour, ICombatStateProvider
    {
        public abstract LayerMask EnemyLayerMask { get; }
        public AStarPathfinder AstarPathfinder { get; }
        public GameObject EnemyTarget { get; set; } = null;
        public GameObject[] EnemySpawnTargets { get; set; } = Array.Empty<GameObject>();
        public CharacterCombatData CharacterData { get; protected set; } = null;
        public Vector2 CurrentWorldPosition => transform.position;
        public Vector2[] CurrentDestinations { get; set; } = Array.Empty<Vector2>();
        public EntityHealth CharHealth { get; protected set; }
        public SpriteRenderer CharSpriteRenderer { get; protected set; } = null;

        protected SpriteLibrary CharSpriteLibrary = null;
        protected ParticleSystemRenderer MovementDustEffect = null;
        protected Animator CharAnimator = null;
        protected CharacterWeapon CharWeapon = null;
        protected CombatController CharCombatController  = null;
        protected Coroutine CoPlayCharCoroutine = null;

        public virtual void SetUp(
            CharacterTableDataRow charData,
            ThreeSoulsRuleDataRow ruleData,
            SpriteLibraryAsset sprLibraryAssetData,
            ICombatWorld combatWorld)
        {
            CharCombatController.Initialize(this, combatWorld);

            ResetData();

            CharSpriteLibrary.spriteLibraryAsset = sprLibraryAssetData;
            CharacterData = new CharacterCombatData(charData.CharacterType, charData.CharacterRole, charData.AttackType)
            {
                CharHealthPoint = charData.MaxHP,
                CharAttackDmg = charData.AttackDmg,
                CharAttackInterval = ruleData.BaseAttackInterval / charData.AttackSpeedMultiplier,
                CharMoveSpeed = charData.MoveSpeed,
                CharRunningSpeed = charData.RunningSpeed,
                CharDistanceToTarget = charData.DistanceToTarget,
                CharIsRunningPlay = charData.IsRunningPlay,
                CharDetectDelay = charData.DetectDelay
            };

            CharHealth.Initialize(CharacterData.CharHealthPoint);
        }

        public virtual void WaitCombat()
        {
          
        }
        
        protected virtual void Awake()
        {
            CharAnimator = GetComponent<Animator>();
            CharSpriteLibrary = GetComponentInChildren<SpriteLibrary>();
            CharSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            MovementDustEffect = GetComponentInChildren<ParticleSystemRenderer>(true);
            CharCombatController = GetComponent<CombatController>();
            CharHealth = GetComponent<EntityHealth>();
            CharWeapon = GetComponent<CharacterWeapon>();
            CharCombatController.IsPlay = false;
        }

        public virtual void Despawn()
        {
            CharCombatController.IsPlay = false;

            if (CoPlayCharCoroutine != null)
            {
                StopCoroutine(CoPlayCharCoroutine);
                CoPlayCharCoroutine = null;
            }
        }

        private void ResetData()
        {   
            EnemyTarget = null;
            EnemySpawnTargets = Array.Empty<GameObject>();
            CurrentDestinations = Array.Empty<Vector2>();
            CharCombatController.IsPlay = true;
            SetAnimator(AnimatorStateParams.Dead, false);
            SetAnimator(AnimatorStateParams.Attack, false);
            SetAnimator(AnimatorStateParams.Walking, false);
            SetAnimator(AnimatorStateParams.Running, false);
            SetAnimator(AnimatorStateParams.Hit, false);
            SetEffect(CombatState.MoveToTarget, false);
        }

        public virtual void SetAnimator(AnimatorStateParams animatorParam, bool flag)
        {
            CharAnimator.SetBool(CharacterUtility.GetCharacterAnimatorParametersHash(animatorParam), flag);
        }

        public virtual bool IsAnimationPlay(AnimatorStateParams animatorParam)
        {
            return CharAnimator.GetBool(CharacterUtility.GetCharacterAnimatorParametersHash(animatorParam));
        }

        public virtual void PlayAnimation(AnimatorStateParams animatorParam, int layer = 0, float normalizedTime = 0)
        {
            CharAnimator.Play(CharacterUtility.GetCharacterAnimatorParametersHash(animatorParam), layer, normalizedTime);
        }

        public virtual void SetEffect(CombatState state, bool play)
        {
            MovementDustEffect.gameObject.SetActive(play);
            MovementDustEffect.flip = play ? Vector2.right : Vector2.zero;
        }

        public virtual void ChangeState(CombatState state)
        {
            CharCombatController.ChangeState(state);
        }

        public virtual float GetPlayAnimationNormalizedTime(AnimatorStateParams animatorParam)
        {
            var currentState = CharAnimator.GetCurrentAnimatorStateInfo(0);
            
            if (currentState.shortNameHash == CharacterUtility.GetCharacterAnimatorParametersHash(animatorParam))
            {
                return currentState.normalizedTime;
            }

            return 0;
        }

        public void SetPosition(Vector3 pos)
        {
            transform.position = pos;
        }

        public void SetSpriteRendererFlip(bool flag)
        {
            var prevFlag = CharSpriteRenderer.flipX;

            if (prevFlag != flag)
            {
                CharSpriteRenderer.flipX = flag;
            }
        }
    }
}
