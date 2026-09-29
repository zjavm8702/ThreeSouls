using Sky.Utility;
using System;
using System.Collections;
using ThreeSouls.Data;
using ThreeSouls.Global;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Character
{
    public class EntityHealth : MonoBehaviour
    {
        [SerializeField] private Color OnDecreaseHealthColor;

        private readonly WaitForSeconds ColorSeconds = new(0.1f);
        private Coroutine CoHealthColorCoroutine = null;
        private Color InitColor;

        public bool IsAlive { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public event Action<float, float> HealthChanged;

        public void Initialize(float maxHealth)
        {
            MaxHealth = Mathf.Max(0, maxHealth);
            CurrentHealth = MaxHealth;
            IsAlive = true;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void OnDecreaseHealthPoints(GameObject source)
        {
            var sourceEntity = GameCombatManager.Get().GetCharacterInstance(source);
            var entity = GameCombatManager.Get().GetCharacterInstance(gameObject);

            if (entity == null || sourceEntity == null)
            {
                return;
            }

            if (entity.EnemyTarget == null)
            {
                ThreeSoulsLog.Info($"<color=yellow>{gameObject.name}</color>] EnemyTarget null");
                return;
            }

            var charBattleData = entity.CharacterData;

            if (IsAlive == false)
            {
                return;
            }

            charBattleData.CharHealthPoint = Mathf.Max(
                0,
                charBattleData.CharHealthPoint - sourceEntity.CharacterData.CharAttackDmg);
            CurrentHealth = charBattleData.CharHealthPoint;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            ThreeSoulsLog.Info($"{gameObject.name} : [HealthPoint : <color=yellow>{charBattleData.CharHealthPoint}</color>]");

            if (charBattleData.CharHealthPoint > 0)
            {
                entity.SetAnimator(Enumeration.AnimatorStateParams.Hit, true);
            }
            else
            {
                IsAlive = false;
                ClearCoroutine();

                entity.CharSpriteRenderer.material.SetColor(StringConstants.CHAR_TEXTURE_COLOR_V, InitColor);

                EventUtility.TriggerEvent(this, new Event_CharacterHealthDepleted(entity));
            }
        }

        private IEnumerator SetColor()
        {
            var entity = GameCombatManager.Get().GetCharacterInstance(gameObject);

            if (entity == null)
            {
                yield break;
            }

            var material = entity.CharSpriteRenderer.material;

            material.SetColor(StringConstants.CHAR_TEXTURE_COLOR_V, OnDecreaseHealthColor);
            yield return ColorSeconds;
            material.SetColor(StringConstants.CHAR_TEXTURE_COLOR_V, InitColor);
        }

        private void ClearCoroutine()
        {
            if (CoHealthColorCoroutine == null)
            {
                return;
            }

            StopCoroutine(CoHealthColorCoroutine);
            CoHealthColorCoroutine = null;
        }
    }
}
