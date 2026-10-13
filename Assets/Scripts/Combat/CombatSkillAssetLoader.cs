using System;
using System.Collections;
using System.Collections.Generic;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 스킬 테이블의 리소스 키를 Addressables 주소로 변환하고 전투 전에 로드합니다.
    /// </summary>
    internal static class CombatSkillAssetLoader
    {
        internal static IEnumerator PreLoad(IReadOnlyList<SkillTableDataRow> skills, AddressableManager assets)
        {
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                yield return assets.AddressableLoadAssetAsync<Sprite>(GetIconAddress(skill), false);
                yield return assets.AddressableLoadAssetAsync<AnimationClip>(GetAttackClipAddress(skill), false);
                yield return assets.AddressableLoadAssetAsync<GameObject>(GetEffectPrefabAddress(skill), false);
            }
        }

        internal static string GetIconAddress(SkillTableDataRow skill)
        {
            return ParseKey<Images>(skill.SkillIconKey).ToAddress();
        }

        internal static string GetAttackClipAddress(SkillTableDataRow skill)
        {
            return ParseKey<Animations>(skill.AttackClipKey).ToAddress();
        }

        internal static string GetEffectPrefabAddress(SkillTableDataRow skill)
        {
            return ParseKey<Prefabs>(skill.EffectPrefabKey).ToAddress();
        }

        private static T ParseKey<T>(string key) where T : struct, Enum
        {
            if (Enum.TryParse<T>(key, false, out var value) == false || Enum.IsDefined(typeof(T), value) == false)
            {
                throw new InvalidOperationException($"스킬 테이블의 {typeof(T).Name} 키를 확인하세요 : {key}");
            }

            return value;
        }
    }
}
