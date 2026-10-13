using System;
using ThreeSouls.Data;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Character
{
    public class EntityHealth : MonoBehaviour
    {
        public delegate void OnHealthChangeDelegate(
            bool takeDamage,
            float maxHealth,
            float currentHealth,
            float damage,
            BaseEntity sourceEntity);

        private OnHealthChangeDelegate onHealthChangeEventHandler = null;
        public event OnHealthChangeDelegate OnHealthChangeEventHandler
        {
            add
            {
                onHealthChangeEventHandler -= value;
                onHealthChangeEventHandler += value;
            }
            remove
            {
                onHealthChangeEventHandler -= value;
            }
        }

        public bool IsAlive { get; private set; } = true;
        public ThreeSouls.Combat.CombatDamageInfo LastHit { get; private set; }

        private BaseEntity Entity = null;
        private float CurrentHealth = 0;
        private float MaxHealth = 0;
       
        private void Awake()
        {
            Entity = GetComponent<BaseEntity>();

            if (Entity == null)
            {
                throw new InvalidOperationException($"EntityHealth의 BaseEntity 누락 : {name}");
            }
        }

        internal void Initialize(float maxHealth)
        {
            MaxHealth = Mathf.Max(0, maxHealth);
            CurrentHealth = MaxHealth;
            IsAlive = true;
            LastHit = default;
            onHealthChangeEventHandler?.Invoke(false, MaxHealth, CurrentHealth, 0, null);
        }

        internal void UpdateMaximumHealth(float maxHealth)
        {
            float ratio = MaxHealth > 0 ? CurrentHealth / MaxHealth : 0;
            MaxHealth = Mathf.Max(0, maxHealth);
            CurrentHealth = IsAlive == true ? Mathf.Clamp(ratio * MaxHealth, 0, MaxHealth) : 0;
            onHealthChangeEventHandler?.Invoke(false, MaxHealth, CurrentHealth, 0, null);
        }

        /// <summary>
        /// 새 구독자가 최초 표시를 구성할 수 있도록 현재 체력 상태를 제공합니다.
        /// </summary>
        public void GetHealth(out float maximum, out float current)
        {
            maximum = MaxHealth;
            current = CurrentHealth;
        }

        public bool TryHeal(float amount)
        {
            if (float.IsNaN(amount) == true || float.IsInfinity(amount) == true || amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }
            if (IsAlive == false || CurrentHealth >= MaxHealth)
            {
                return false;
            }
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            onHealthChangeEventHandler?.Invoke(false, MaxHealth, CurrentHealth, 0, null);
            return true;
        }

        /// <summary>
        /// 일반 공격과 스킬의 명시적 피해량을 같은 HP, 피격 UI와 사망 이벤트 경로로 적용합니다.
        /// </summary>
        public void ApplyDamage(BaseEntity sourceEntity, float damage)
        {
            ApplyDamage(sourceEntity, new ThreeSouls.Combat.CombatDamageInfo(damage));
        }

        public void ApplyDamage(BaseEntity sourceEntity, ThreeSouls.Combat.CombatDamageInfo hit)
        {
            var damage = hit.Amount;
            var entity = Entity;

            if (float.IsNaN(damage) == true || float.IsInfinity(damage) == true || damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "피해량은 유한한 음이 아닌 값이어야 합니다.");
            }

            if (sourceEntity == null || sourceEntity.CharHealth.IsAlive == false || damage == 0)
            {
                return;
            }

            if (IsAlive == false || entity.CanTakeDamage() == false)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            LastHit = hit;

            var isFatalDamage = CurrentHealth <= 0;
            IsAlive = isFatalDamage == false;
            onHealthChangeEventHandler?.Invoke(true, MaxHealth, CurrentHealth, damage, sourceEntity);

            ThreeSoulsLog.Info($"{gameObject.name} : [HealthPoint : <color=yellow>{CurrentHealth}</color>]");

            if (isFatalDamage == true)
            {
                EventUtility.TriggerEvent(this, new Event_CharacterHealthDepleted(entity));
            }
        }
    }
}
