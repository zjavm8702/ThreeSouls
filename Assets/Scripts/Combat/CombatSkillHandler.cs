using System;
using System.Collections.Generic;
using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using System.Linq;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 스킬 테이블을 바탕으로 자동 발동, 타깃 선택, 피해와 연출 풀의 수명을 관리합니다.
    /// </summary>
    internal sealed class CombatSkillHandler
    {
        private ICombatSkillWorld World;
        private IReadOnlyList<SkillTableDataRow> Skills = Array.Empty<SkillTableDataRow>();
        private readonly Dictionary<string, float> ReadyTimes = new();
        private readonly Dictionary<string, ComponentPool<CombatSkillEffect>> Pools = new();
        private readonly Dictionary<string, AnimationClip> AttackClips = new();
        private readonly Dictionary<string, Sprite> Icons = new();
        private readonly List<ActiveCast> ActiveCasts = new();
        private ThreeSouls.Runes.RuneCombatSnapshot Runes;

        /// <summary>
        /// 시전 당시 캐릭터 데이터와 타깃을 보관해 풀 재사용된 객체에 피해를 주지 않습니다.
        /// </summary>
        private sealed class ActiveCast
        {
            public SkillTableDataRow Skill;
            public Hero Source;
            public CharacterCombatData SourceData;
            public BaseEntity Target;
            public CharacterCombatData TargetData;
            public Vector2 Center;
            public float Damage;
            public CombatSkillEffect Effect;
        }

        /// <summary>
        /// 새 전투의 스킬 목록과 시작 딜레이를 준비합니다.
        /// </summary>
        public void Initialize(ICombatSkillWorld world, IReadOnlyList<SkillTableDataRow> skills, AddressableManager assets,
            ThreeSouls.Runes.RuneCombatSnapshot runes = null)
        {
            Release();
            World = world ?? throw new ArgumentNullException(nameof(world));
            Runes = runes;
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            if (runes != null)
            {
                Skills = ThreeSouls.Runes.RuneSkillRules.CreateSkills(skills, runes);
            }

            for (int i = 0; i < Skills.Count; i++)
            {
                var skill = Skills[i];
                if (skill == null || string.IsNullOrEmpty(skill.Key) == true ||
                    string.IsNullOrEmpty(skill.HeroKey) == true ||
                    IsValidNumber(skill.InitialDelay) == false || IsValidNumber(skill.Cooldown) == false ||
                    IsValidNumber(skill.CastRange) == false || IsValidNumber(skill.HitDelay) == false ||
                    IsValidNumber(skill.DamageMultiplier) == false || IsValidNumber(skill.Radius) == false ||
                    skill.Cooldown <= 0 || skill.CastRange <= 0 || skill.IsArea == true && skill.Radius <= 0)
                {
                    throw new InvalidOperationException("SkillTableData의 키와 전투 수치를 확인하세요.");
                }

                var icon = assets.LoadAsset<Sprite>(CombatSkillAssetLoader.GetIconAddress(skill));
                var clip = assets.LoadAsset<AnimationClip>(CombatSkillAssetLoader.GetAttackClipAddress(skill));
                var prefab = assets.LoadAsset<GameObject>(CombatSkillAssetLoader.GetEffectPrefabAddress(skill));

                if (icon == null || clip == null || prefab == null ||
                    prefab.TryGetComponent<CombatSkillEffect>(out var effect) == false || skill.HitDelay > clip.length)
                {
                    throw new InvalidOperationException($"스킬 리소스 로드 또는 명중 시점을 확인하세요 : {skill.Key}");
                }

                ReadyTimes.Add(skill.Key, Time.time + skill.InitialDelay);
                AttackClips.Add(skill.Key, clip);
                Icons.Add(skill.Key, icon);
                var pool = new ComponentPool<CombatSkillEffect>();
                pool.CreatePool(effect, 0);
                Pools.Add(skill.Key, pool);
            }
        }

        /// <summary>
        /// 등록된 스킬 정보를 복사 없이 조회합니다.
        /// </summary>
        public IReadOnlyList<SkillTableDataRow> GetSkills()
        {
            return Skills;
        }

        /// <summary>
        /// 준비 단계에서 로드한 스킬 아이콘을 표시 담당자에게 제공합니다.
        /// </summary>
        public Sprite GetSkillIcon(string key)
        {
            return Icons[key];
        }

        private static bool IsValidNumber(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false && value >= 0;
        }

        /// <summary>
        /// 카메라 인트로와 에셋 준비가 끝난 실제 전투 시작 시점부터 첫 사용 딜레이를 계산합니다.
        /// </summary>
        public void StartCombat()
        {
            for (int i = 0; i < Skills.Count; i++)
            {
                ReadyTimes[Skills[i].Key] = Time.time + Skills[i].InitialDelay;
            }
        }

        /// <summary>
        /// 시작 딜레이와 재사용 대기 시간을 게임 시간으로 계산합니다.
        /// </summary>
        public float GetCooldownRemaining(string key)
        {
            return ReadyTimes.TryGetValue(key, out var readyTime) == true ? Mathf.Max(0, readyTime - Time.time) : 0;
        }

        /// <summary>
        /// 전투 중 사거리 안의 적이 있을 때 준비된 스킬을 자동으로 사용합니다.
        /// 사망하거나 재사용된 시전자의 진행 중 연출은 즉시 취소합니다.
        /// </summary>
        public void Update()
        {
            for (int i = ActiveCasts.Count - 1; i >= 0; i--)
            {
                var cast = ActiveCasts[i];
                if (IsSameAliveEntity(cast.Source, cast.SourceData) == false)
                {
                    FinishCast(cast);
                }
            }

            if (IsCombatPhase() == false)
            {
                return;
            }

            for (int i = 0; i < Skills.Count; i++)
            {
                TryUseSkill(Skills[i].Key);
            }
        }

        /// <summary>
        /// 쿨타임, 시전자와 타깃을 검사하고 피해 시점 및 풀 반환을 연결합니다.
        /// </summary>
        public bool TryUseSkill(string key)
        {
            if (IsCombatPhase() == false || GetCooldownRemaining(key) > 0)
            {
                return false;
            }

            SkillTableDataRow skill = null;
            for (int i = 0; i < Skills.Count; i++)
            {
                if (Skills[i].Key == key)
                {
                    skill = Skills[i];
                    break;
                }
            }

            if (skill == null)
            {
                return false;
            }

            var source = FindSource(skill.HeroKey);
            if (source == null || source.IsCastingSkill == true)
            {
                return false;
            }

            var target = FindTarget(source, skill.CastRange);
            if (target == null)
            {
                return false;
            }

            var center = skill.CenterOnCaster == true ? source.GetWorldPosition() : target.GetWorldPosition();
            var effect = Pools[key].Spawn(null, center + skill.EffectOffset);
            var direction = target.GetWorldPosition() - source.GetWorldPosition();
            var angle = skill.IsArea == false ? Vector2.SignedAngle(new Vector2(1, -1), direction) : 0;
            var cast = new ActiveCast
            {
                Skill = skill,
                Source = source,
                SourceData = source.CharacterData,
                Target = target,
                TargetData = target.CharacterData,
                Center = center,
                Damage = source.CharacterData.CharAttackDmg * skill.DamageMultiplier,
                Effect = effect
            };

            ActiveCasts.Add(cast);
            ReadyTimes[key] = Time.time + skill.Cooldown;
            source.BeginSkillCast();
            ThreeSouls.Audio.GameAudio.Play(skill.HeroKey == "Key_night" ? ThreeSouls.Audio.SoundCue.Sword :
                skill.HeroKey == "Key_archer" ? ThreeSouls.Audio.SoundCue.Bow : ThreeSouls.Audio.SoundCue.Magic);
            effect.Play(AttackClips[key], skill.HitDelay, Quaternion.Euler(0, 0, angle),
                () => ApplyDamage(cast), () => FinishCast(cast),
                skill.IsArea == true && Runes != null ? Runes.GetSkill(key).RadiusMultiplier : 1);
            return true;
        }

        /// <summary>
        /// 생존 영웅 목록에서 스킬을 가진 시전자를 찾습니다.
        /// </summary>
        private Hero FindSource(string heroKey)
        {
            var heroes = World.GetAliveHeroes();
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i].CharacterData.CharacterKey == heroKey)
                {
                    return (Hero)heroes[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 장애물로 막히지 않은 사거리 안의 가장 가까운 생존 적을 선택합니다.
        /// </summary>
        private BaseEntity FindTarget(BaseEntity source, float range)
        {
            BaseEntity selected = null;
            var distance = range * range;
            var targets = source.GetEnemySpawnTargets();
            for (int i = 0; i < targets.Count; i++)
            {
                if (World.IsTargetAlive(targets[i]) == false)
                {
                    continue;
                }

                var target = World.GetCharacterInstance(targets[i]);
                var candidateDistance = Vector2.SqrMagnitude(target.GetWorldPosition() - source.GetWorldPosition());
                if (candidateDistance <= distance && World.CanMoveDirectly(source, target.GetWorldPosition()) == true)
                {
                    selected = target;
                    distance = candidateDistance;
                }
            }

            return selected;
        }

        /// <summary>
        /// 단일기는 시전 때 선택한 적 하나, 범위기는 명중 시점의 반경 안 적에게 피해를 줍니다.
        /// 처치 이벤트가 대상 목록을 바꿀 수 있으므로 범위 대상은 먼저 복사합니다.
        /// </summary>
        private void ApplyDamage(ActiveCast cast)
        {
            if (IsSameAliveEntity(cast.Source, cast.SourceData) == false)
            {
                return;
            }

            if (cast.Skill.IsArea == false)
            {
                if (IsSameAliveEntity(cast.Target, cast.TargetData) == true)
                {
                    CombatHitResolver.ApplyDamage(cast.Source, cast.Target, cast.Damage, true);
                }

                return;
            }

            var targets = cast.Source.GetEnemySpawnTargets().ToArray();
            for (int i = 0; i < targets.Length; i++)
            {
                if (World.IsTargetAlive(targets[i]) == false)
                {
                    continue;
                }

                var target = World.GetCharacterInstance(targets[i]);
                if (Vector2.SqrMagnitude(target.GetWorldPosition() - cast.Center) <= cast.Skill.Radius * cast.Skill.Radius &&
                    World.CanMoveDirectly(cast.Source, target.GetWorldPosition()) == true)
                {
                    CombatHitResolver.ApplyDamage(cast.Source, target, cast.Damage, true);
                }
            }
        }

        /// <summary>
        /// 실제 전투 및 성소 잔여 적 정리 단계에서만 새 스킬을 허용합니다.
        /// </summary>
        private bool IsCombatPhase()
        {
            return World != null && World.IsCombatActive() == true;
        }

        /// <summary>
        /// 같은 스폰 데이터의 살아 있는 캐릭터인지 확인합니다.
        /// </summary>
        private static bool IsSameAliveEntity(BaseEntity entity, CharacterCombatData data)
        {
            return entity != null && entity.gameObject.activeInHierarchy == true &&
                entity.CharacterData == data && entity.CharHealth.IsAlive == true;
        }

        /// <summary>
        /// 기본 전투 행동을 유지하며 스킬 시전 제한을 풀고 연출을 한 번만 반환합니다.
        /// </summary>
        private void FinishCast(ActiveCast cast)
        {
            if (ActiveCasts.Remove(cast) == false)
            {
                return;
            }

            cast.Effect.Cancel();
            if (cast.Source != null && cast.Source.CharacterData == cast.SourceData)
            {
                cast.Source.EndSkillCast();
            }

            Pools[cast.Skill.Key].Recycle(cast.Effect);
        }

        /// <summary>
        /// 전투 종료 시 콜백과 연출 인스턴스를 원본 참조보다 먼저 정리합니다.
        /// </summary>
        public void Release()
        {
            for (int i = ActiveCasts.Count - 1; i >= 0; i--)
            {
                FinishCast(ActiveCasts[i]);
            }

            using var pools = Pools.Values.GetEnumerator();
            while (pools.MoveNext() == true)
            {
                pools.Current.Release();
            }

            Pools.Clear();
            AttackClips.Clear();
            Icons.Clear();
            ReadyTimes.Clear();
            Skills = Array.Empty<SkillTableDataRow>();
            World = null;
            Runes = null;
        }
    }
}
