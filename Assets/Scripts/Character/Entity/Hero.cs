using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Character
{
    /// <summary>
    /// 영웅의 장비 변경 반영과 스킬 시전 행동을 담당합니다.
    /// </summary>
    public class Hero : BaseEntity
    {
        public bool IsCastingSkill { get; private set; }

        private IHeroEquipmentSource EquipmentSource;
        private IHeroGrowthSource GrowthSource;
        private CharacterTableDataRow BaseStats;
        private ThreeSoulsRuleDataRow BaseRule;
        private ThreeSouls.Runes.SanctuaryDebuff SanctuaryDebuff;
        private float SanctuaryDebuffRatio;

        internal void SetSanctuaryDebuff(ThreeSouls.Runes.SanctuaryDebuff debuff, float ratio)
        {
            if (EquipmentSource == null || System.Enum.IsDefined(typeof(ThreeSouls.Runes.SanctuaryDebuff), debuff) == false ||
                float.IsNaN(ratio) == true || float.IsInfinity(ratio) == true || ratio < 0 || ratio >= 1)
            {
                throw new System.InvalidOperationException("연결된 영웅에게 유효한 성소 디버프를 적용하세요.");
            }

            SanctuaryDebuff = debuff;
            SanctuaryDebuffRatio = ratio;
            ApplyEquipment(false);
        }

        /// <summary>
        /// 스폰 시 현재 장비를 한 번 적용하고 이후 변경을 구독합니다.
        /// </summary>
        internal void BindEquipment(IHeroEquipmentSource source, IHeroGrowthSource growthSource,
            CharacterTableDataRow character, ThreeSoulsRuleDataRow rule)
        {
            UnbindEquipment();
            EquipmentSource = source ?? throw new System.ArgumentNullException(nameof(source));
            GrowthSource = growthSource ?? throw new System.ArgumentNullException(nameof(growthSource));
            BaseStats = character;
            BaseRule = rule;
            IsCastingSkill = false;
            ApplyEquipment(true);
            EquipmentSource.OnEquipmentChangedEventHandler += OnEquipmentChanged;
            GrowthSource.OnHeroGrowthChangedEventHandler += OnGrowthChanged;
        }

        private void OnGrowthChanged(string heroKey)
        {
            if (heroKey == CharacterData.CharacterKey)
            {
                ApplyEquipment(false);
            }
        }

        private void OnEquipmentChanged(string heroKey)
        {
            if (heroKey == CharacterData.CharacterKey)
            {
                ApplyEquipment(false);
            }
        }

        private void ApplyEquipment(bool spawning)
        {
            var bonus = EquipmentSource.GetEquipmentBonuses(BaseStats);
            var growth = GrowthSource.GetBonuses(BaseStats);
            var attackMultiplier = SanctuaryDebuff == ThreeSouls.Runes.SanctuaryDebuff.Attack ? 1 - SanctuaryDebuffRatio : 1;
            var speedMultiplier = SanctuaryDebuff == ThreeSouls.Runes.SanctuaryDebuff.AttackSpeed ? 1 - SanctuaryDebuffRatio : 1;
            CharacterData.SetAttackStats((BaseStats.AttackDmg + bonus.AttackDmg + growth.Attack) * attackMultiplier,
                EquipmentRules.GetAttackInterval(BaseRule.BaseAttackInterval, BaseStats.AttackSpeedMultiplier,
                    bonus.AttackSpeedBonus + growth.AttackSpeed) / speedMultiplier);
            CharacterData.SetCriticalStats(growth.CriticalChance, growth.CriticalMultiplier);
            var maxHealth = BaseStats.MaxHP + bonus.MaxHP + growth.MaxHealth;
            if (SanctuaryDebuff == ThreeSouls.Runes.SanctuaryDebuff.MaxHealth)
            {
                maxHealth *= 1 - SanctuaryDebuffRatio;
            }

            if (spawning == true)
            {
                CharHealth.Initialize(maxHealth);
            }
            else
            {
                CharHealth.UpdateMaximumHealth(maxHealth);
            }
        }

        /// <summary>
        /// 기본 전투 행동을 유지하며 같은 영웅의 스킬 중복 시전만 제한합니다.
        /// </summary>
        public void BeginSkillCast()
        {
            IsCastingSkill = true;
        }

        /// <summary>
        /// 스킬 연출 종료 시 일반 공격과 이동을 변경하지 않고 시전 제한을 해제합니다.
        /// </summary>
        public void EndSkillCast()
        {
            IsCastingSkill = false;
        }

        /// <summary>
        /// 피격 시작부터 클립 종료 이벤트까지 영웅의 추가 피해를 차단합니다.
        /// </summary>
        internal override bool CanTakeDamage()
        {
            return IsAnimationPlay(AnimatorStateParams.Hit) == false;
        }

        public override void Despawn()
        {
            IsCastingSkill = false;
            UnbindEquipment();
            base.Despawn();
        }

        private void UnbindEquipment()
        {
            if (GrowthSource != null)
            {
                GrowthSource.OnHeroGrowthChangedEventHandler -= OnGrowthChanged;
                GrowthSource = null;
            }

            if (EquipmentSource != null)
            {
                EquipmentSource.OnEquipmentChangedEventHandler -= OnEquipmentChanged;
                EquipmentSource = null;
            }

            BaseStats = null;
            BaseRule = null;
            SanctuaryDebuff = ThreeSouls.Runes.SanctuaryDebuff.None;
            SanctuaryDebuffRatio = 0;
        }

        protected override void OnDisable()
        {
            UnbindEquipment();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            UnbindEquipment();
            base.OnDestroy();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var position = new Vector2(transform.position.x, transform.position.y + 0.5f);
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(position, Vector3.forward, Constants.CIRCLE_RADIUS_HERO);
        }
#endif
    }
}
