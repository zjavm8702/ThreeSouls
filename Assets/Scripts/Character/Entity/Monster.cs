using System;
using ThreeSouls.Data.Generated;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Character
{
    /// <summary>
    /// 몬스터의 피격 반응과 스폰별 보상 상태를 연결하며 이동 연출과 보상 계산은 담당자에게 위임합니다.
    /// </summary>
    public class Monster : BaseEntity
    {
        internal bool IsSpecialReward { get; private set; }
        private bool RewardGranted;
        private ICombatRewardWorld RewardWorld;
        private EntityKnockback Knockback;

        public override void SetUp(CharacterTableDataRow character, ThreeSoulsRuleDataRow rule, SpriteLibraryAsset spriteLibrary, ICombatWorld combatWorld, IProjectileSpawner projectiles)
        {
            Knockback?.Clear();
            Knockback = new EntityKnockback(this, combatWorld);
            RewardGranted = false;
            IsSpecialReward = false;
            base.SetUp(character, rule, spriteLibrary, combatWorld, projectiles);
        }

        internal void BindRewardWorld(ICombatRewardWorld rewardWorld)
        {
            RewardWorld = rewardWorld ?? throw new ArgumentNullException(nameof(rewardWorld));
        }

        protected override void OnHealthChangeEventHandler(
            bool takeDamage,
            float maxHealth,
            float currentHealth,
            float damage,
            BaseEntity sourceEntity)
        {
            base.OnHealthChangeEventHandler(takeDamage, maxHealth, currentHealth, damage, sourceEntity);

            if (takeDamage == false)
            {
                return;
            }

            if (CharHealth.IsAlive == false)
            {
                Knockback.Clear();
                RewardWorld.SpawnRewardText(this);

                return;
            }

            var direction = GetWorldPosition() - sourceEntity.GetWorldPosition();

            if (direction.sqrMagnitude == 0)
            {
                direction = sourceEntity.GetFacingDirection();
            }

            Knockback.Begin(direction);
        }

        public override bool IsForcedMovementActive()
        {
            return Knockback.IsActive;
        }

        public override bool TryUpdateForcedMovement(float deltaTime)
        {
            return Knockback.TryUpdate(deltaTime);
        }

        public override void Despawn()
        {
            Knockback.Clear();
            base.Despawn();
            RewardWorld = null;
        }

        protected override void OnDisable()
        {
            Knockback?.Clear();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Knockback?.Clear();
            base.OnDestroy();
        }

        internal void SetSpecialReward(bool special)
        {
            IsSpecialReward = special;
        }

        internal bool TryMarkRewardGranted()
        {
            if (RewardGranted == true)
            {
                return false;
            }

            RewardGranted = true;

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var position = new Vector2(transform.position.x, transform.position.y + 0.5f);
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(position, Vector3.forward, Constants.CIRCLE_RADIUS_MONSTER);
        }
#endif
    }
}
