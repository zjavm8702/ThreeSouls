using ThreeSouls.Character;
using ThreeSouls.Enumeration;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Weapon
{
    public class CharacterWeapon : MonoBehaviour
    {
        private void Attack()
        {
            var combatManager = GameCombatManager.Get();
            var spawnEntity = combatManager.GetCharacterInstance(gameObject);

            if (spawnEntity == null)
            {
                ThreeSoulsLog.Error($"Not Found SpawnEntity {gameObject}");
                return;
            }

            if (combatManager.IsTargetInAttackRange(spawnEntity, spawnEntity.EnemyTarget) == false)
            {
                return;
            }

            var targetEntity = combatManager.GetCharacterInstance(spawnEntity.EnemyTarget);

            if (targetEntity == null)
            {
                return;
            }

            spawnEntity.SetSpriteRendererFlip(
                spawnEntity.CurrentWorldPosition.x > targetEntity.CurrentWorldPosition.x);

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
                    ThreeSoulsLog.Error($"Unsupported Attack Type {spawnEntity.CharacterData.CharacterAttackType}");
                    return;
                
            }
        }

        private void SingleAttackByMeleeDealer(BaseEntity targetEntity)
        {
            targetEntity.CharHealth.OnDecreaseHealthPoints(gameObject);
        }

        private void SingleAttackByRangedDealer(BaseEntity spawnEntity, BaseEntity targetEntity)
        {
            var name = GetSprLibraryName(spawnEntity.CharacterData.CharacterAttackType);
            var pos = new Vector3(spawnEntity.transform.position.x, spawnEntity.transform.position.y + 0.5f, 0);
            GameCombatManager.Get().SpawnProjectile(null, pos, name, spawnEntity, targetEntity);
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
                    ThreeSoulsLog.Error($"Unsupported Attack Type {attackType}");
                    return default;
            }
        }
    }
}
