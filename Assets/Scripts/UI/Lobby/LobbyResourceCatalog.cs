using System;
using System.Collections;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 테이블의 이미지와 Controller 주소를 로드하고 표시 리소스를 조회합니다.
    /// </summary>
    public static class LobbyResourceCatalog
    {
        public static Sprite GetSprite(string path, string spriteName = null)
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                var resources = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                for (var i = 0; i < resources.Length; i++)
                {
                    if (resources[i] is Sprite sprite && (string.IsNullOrEmpty(spriteName) == true || sprite.name == spriteName))
                    {
                        return sprite;
                    }
                }
                throw new InvalidOperationException("로비 Sprite 누락: " + path + "/" + spriteName);
            }
#endif
            var address = string.IsNullOrEmpty(spriteName) == true ? path : path + "[" + spriteName + "]";
            return AddressableManager.Get().LoadAsset<Sprite>(address)
                ?? throw new InvalidOperationException("로비 Sprite 선행 로드 누락: " + address);
        }

        public static RuntimeAnimatorController GetAnimator(string path)
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path)
                    ?? throw new InvalidOperationException("마을 Controller 누락: " + path);
            }
#endif
            return AddressableManager.Get().LoadAsset<RuntimeAnimatorController>(path)
                ?? throw new InvalidOperationException("마을 Controller 선행 로드 누락: " + path);
        }

        public static string[] GetSpriteAddresses(EquipmentTableDataTable equipment)
        {
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < equipment.Items.Length; i++)
            {
                addresses.Add(equipment.Items[i].IconKey);
            }
            var heroes = LobbyTables.Get<LobbyHeroTableDataTable>().Items;
            for (var i = 0; i < heroes.Length; i++)
            {
                addresses.Add(heroes[i].PortraitImagePath + "[" + heroes[i].PortraitSpriteName + "]");
            }
            var potions = LobbyTables.Get<ConsumableTableDataTable>().Items;
            for (var i = 0; i < potions.Length; i++)
            {
                addresses.Add(potions[i].IconPath + "[" + potions[i].IconSpriteName + "]");
            }
            var result = new string[addresses.Count];
            addresses.CopyTo(result);
            return result;
        }

        public static IEnumerator PreLoad(AddressableManager assets, EquipmentTableDataTable equipment)
        {
            var sprites = GetSpriteAddresses(equipment);
            yield return assets.PreLoadBatch<Sprite>(sprites, false);
            var heroes = LobbyTables.Get<LobbyHeroTableDataTable>().Items;
            var controllers = new string[heroes.Length];
            for (var i = 0; i < heroes.Length; i++) controllers[i] = heroes[i].VillageAnimatorPath;
            yield return assets.PreLoadBatch<RuntimeAnimatorController>(controllers, false);
        }
    }
}
