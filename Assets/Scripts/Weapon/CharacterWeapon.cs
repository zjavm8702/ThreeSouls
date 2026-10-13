using ThreeSouls.Character;
using ThreeSouls.Enumeration;
using ThreeSouls.Enumeration.AddressableKey;
using System;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Weapon
{
    public class CharacterWeapon : MonoBehaviour
    {
        [Header("Projectile Muzzle")]
        [SerializeField] private Vector2 RightMuzzleOffset = new(0.48f, 0.45f);
        [SerializeField] private Vector2 DownMuzzleOffset = new(0.14f, 0.12f);
        [SerializeField] private Vector2 UpMuzzleOffset = new(0.14f, 0.85f);

        private BaseEntity Entity;
        private ICombatWorld CombatWorld;
        private IProjectileSpawner Projectiles;

        private void Awake()
        {
            Entity = GetComponent<BaseEntity>();

            if (Entity == null)
            {
                throw new InvalidOperationException($"CharacterWeapon의 BaseEntity 누락 : {name}");
            }
        }

        internal void Initialize(ICombatWorld world, IProjectileSpawner projectiles)
        {
            CombatWorld = world;
            Projectiles = projectiles;
        }

        internal void Clear()
        {
            CombatWorld = null;
            Projectiles = null;
        }

        private void Attack()
        {
            // 애니메이션 이벤트는 풀 반환 직후에도 전달될 수 있습니다.
            if (CombatWorld == null)
            {
                return;
            }

            var spawnEntity = Entity;

            if (CombatWorld.IsTargetInAttackRange(spawnEntity, spawnEntity.EnemyTarget) == false)
            {
                return;
            }

            var targetEntity = spawnEntity.EnemyTarget.GetComponent<BaseEntity>();

            if (targetEntity == null)
            {
                return;
            }

            switch (spawnEntity.CharacterData.CharacterAttackType)
            {
                case AttackType.Single:
                    SingleAttackByMeleeDealer(targetEntity);
                    break;
                case AttackType.SingleBow:
                    SingleAttackByRangedDealer(spawnEntity, targetEntity);
                    break;
                case AttackType.SingleStaff:
                    SingleAttackByRangedDealer(spawnEntity, targetEntity);
                    break;
                default:
                    throw new InvalidOperationException($"지원하지 않는 공격 타입 : {spawnEntity.CharacterData.CharacterAttackType}");
            }
        }

        private void SingleAttackByMeleeDealer(BaseEntity targetEntity)
        {
            ThreeSouls.Combat.CombatHitResolver.ApplyDamage(Entity, targetEntity, Entity.CharacterData.CharAttackDmg);
        }

        private void SingleAttackByRangedDealer(BaseEntity spawnEntity, BaseEntity targetEntity)
        {
            var name = GetSprLibraryName(spawnEntity.CharacterData.CharacterAttackType);
            var pos = GetProjectileSpawnPosition(spawnEntity);
            Projectiles.SpawnProjectile(null, pos, name, spawnEntity, targetEntity);
        }

        /// <summary>
        /// 방향별 무기 앞 오프셋을 캐릭터 크기에 맞춰 실제 물리 위치에 더합니다.
        /// 왼쪽 공격은 오른쪽 무기 위치를 좌우 반전합니다.
        /// </summary>
        private Vector3 GetProjectileSpawnPosition(BaseEntity source)
        {
            var direction = source.GetFacingDirection();
            var offset = direction == Vector2.up ? UpMuzzleOffset :
                direction == Vector2.down ? DownMuzzleOffset : RightMuzzleOffset;

            if (direction == Vector2.left)
            {
                offset.x = -offset.x;
            }

            return (Vector3)source.GetWorldPosition() + source.transform.TransformVector(offset);
        }

        private SpriteLibrarys GetSprLibraryName(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.SingleBow:
                    return SpriteLibrarys.ArcherArrow;
                case AttackType.SingleStaff:
                    return SpriteLibrarys.WizardMagicBean;
                default:
                    throw new InvalidOperationException($"지원하지 않는 원거리 공격 타입 : {attackType}");
            }
        }
    }
}
