using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using ThreeSouls.Weapon;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Character
{
    /// <summary>
    /// 영웅과 몬스터의 공통 전투 행동과 캐릭터별 전투 상태를 제공합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public abstract class BaseEntity : MonoBehaviour, ICombatStateProvider
    {
        public GameObject EnemyTarget { get; private set; }
        public CharacterCombatData CharacterData { get; private set; }
        public EntityHealth CharHealth { get; private set; }

        [SerializeField] private Material HitFlashMaterial;

        private Rigidbody2D CharRigid2D;
        private CircleCollider2D CharMoveCollider;
        private SpriteLibrary CharSpriteLibrary;
        private CharacterWeapon CharWeapon;
        private CombatController CharCombatController;
        private EntityAnimation Animation;
        private IReadOnlyList<GameObject> EnemySpawnTargets = Array.Empty<GameObject>();

        protected ICombatWorld CombatWorld;

        protected virtual void Awake()
        {
            CharRigid2D = GetRequiredComponent<Rigidbody2D>();
            CharMoveCollider = GetRequiredComponent<CircleCollider2D>();
            CharCombatController = GetRequiredComponent<CombatController>();
            CharHealth = GetRequiredComponent<EntityHealth>();
            CharWeapon = GetRequiredComponent<CharacterWeapon>();
            CharSpriteLibrary = GetComponentInChildren<SpriteLibrary>();
            
            var animator = GetRequiredComponent<Animator>();
            var renderer = GetComponentInChildren<SpriteRenderer>();

            if (CharSpriteLibrary == null || renderer == null)
            {
                throw new InvalidOperationException($"캐릭터 SpriteLibrary 또는 SpriteRenderer 누락 : {name}");
            }

            Animation = new EntityAnimation(animator, renderer, GetComponentInChildren<ParticleSystemRenderer>(true), HitFlashMaterial);

            CharHealth.OnHealthChangeEventHandler += OnHealthChangeEventHandler;
        }

        public virtual void SetUp(CharacterTableDataRow character, ThreeSoulsRuleDataRow rule, SpriteLibraryAsset spriteLibrary, ICombatWorld combatWorld, IProjectileSpawner projectiles)
        {
            CombatWorld = combatWorld;

            CharCombatController.Initialize(this, CombatWorld);
            CharWeapon.Initialize(CombatWorld, projectiles);

            CharacterData = new CharacterCombatData(character, rule);
            Animation.ValidateRunningSupport(CharacterData.CharacterType == CharacterType.Hero);
            
            CharSpriteLibrary.spriteLibraryAsset = spriteLibrary;
            
            EnemyTarget = null;
            EnemySpawnTargets = Array.Empty<GameObject>();
            
            StopMove();
            
            CharRigid2D.position = transform.position;
            
            Animation.Reset();
            
            CharHealth.Initialize(character.MaxHP);
            
            CharCombatController.Play();
        }

        public virtual void Despawn()
        {
            CharCombatController.Stop();
            CharWeapon.Clear();
            Animation.ClearHitFeedback();
            EnemyTarget = null;
            EnemySpawnTargets = Array.Empty<GameObject>();

            StopMove();
        }

        public IReadOnlyList<GameObject> GetEnemySpawnTargets()
        {
            return EnemySpawnTargets;
        }

        internal void SetEnemySpawnTargets(IReadOnlyList<GameObject> targets)
        {
            EnemySpawnTargets = targets;
        }

        public void SetEnemyTarget(GameObject target)
        {
            EnemyTarget = target;
        }

        public Vector2 GetWorldPosition()
        {
            return CharRigid2D.position;
        }

        public float GetMoveRadius()
        {
            var scale = transform.lossyScale;

            return CharMoveCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        public Vector2 GetMoveOffset()
        {
            return transform.TransformVector(CharMoveCollider.offset);
        }

        public void MovePosition(Vector2 position)
        {
            CharRigid2D.MovePosition(position);
        }

        public void StopMove()
        {
            CharRigid2D.linearVelocity = Vector2.zero;
            CharRigid2D.angularVelocity = 0;

            CharRigid2D.MovePosition(CharRigid2D.position);
        }

        public void ChangeState(CombatState state)
        {
            CharCombatController.ChangeState(state);
        }

        /// <summary>
        /// 강제 이동이 진행 중인지 판단하여 일반 AI 갱신과 분리합니다.
        /// </summary>
        public virtual bool IsForcedMovementActive()
        {
            return false;
        }

        /// <summary>
        /// 강제 이동이 이번 물리 갱신을 처리했으면 일반 이동을 건너뜁니다.
        /// </summary>
        public virtual bool TryUpdateForcedMovement(float deltaTime)
        {
            return false;
        }

        public void SetAnimator(AnimatorStateParams parameter, bool value)
        {
            Animation.SetAnimator(parameter, value);
        }

        public bool IsAnimationPlay(AnimatorStateParams parameter)
        {
            return Animation.IsAnimationPlay(parameter);
        }

        public void PlayAnimation(AnimatorStateParams parameter, int layer = 0, float normalizedTime = 0)
        {
            Animation.PlayAnimation(parameter, layer, normalizedTime);
        }

        public float GetPlayAnimationNormalizedTime(AnimatorStateParams parameter)
        {
            return Animation.GetNormalizedTime(parameter);
        }

        public void SetMovementEffect(bool play)
        {
            Animation.SetMovementEffect(play);
        }

        public void SetDirection(Vector2 direction)
        {
            Animation.SetDirection(direction);
        }

        public Vector2 GetFacingDirection()
        {
            return Animation.GetFacingDirection();
        }

        public Bounds GetSpriteBounds()
        {
            return Animation.GetSpriteBounds();
        }

        /// <summary>
        /// 캐릭터별 행동 규칙에 따라 추가 피해를 받을 수 있는지 판단합니다.
        /// </summary>
        internal virtual bool CanTakeDamage()
        {
            return true;
        }

        protected virtual void OnHealthChangeEventHandler(
            bool takeDamage,
            float maxHealth,
            float currentHealth,
            float damage,
            BaseEntity sourceEntity)
        {
            if (takeDamage == false)
            {
                return;
            }

            Animation.PlayHitFeedback(CharHealth.LastHit);

            if (CharHealth.IsAlive == true)
            {
                Animation.BeginHitAnimation();
            }

            CombatWorld.SpawnDamageText(transform.position, CharHealth.LastHit);
        }

        protected virtual void OnDisable()
        {
            Animation?.ClearHitFeedback();
        }

        protected virtual void OnDestroy()
        {
            Animation?.ClearHitFeedback();

            if (CharHealth != null)
            {
                CharHealth.OnHealthChangeEventHandler -= OnHealthChangeEventHandler;
            }
        }

        /// <summary>
        /// 피격 클립 종료 이벤트에서 피격 상태를 해제하여 기존 행동으로 돌아갑니다.
        /// </summary>
        private void OnHitAnimationCompleted()
        {
            Animation.SetAnimator(AnimatorStateParams.Hit, false);
        }

        private T GetRequiredComponent<T>() where T : Component
        {
            if (TryGetComponent<T>(out var component) == false)
            {
                throw new InvalidOperationException($"캐릭터 필수 컴포넌트 누락 : {name}, {typeof(T).Name}");
            }

            return component;
        }
    }
}
