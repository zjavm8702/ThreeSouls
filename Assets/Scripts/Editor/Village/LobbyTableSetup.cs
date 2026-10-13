using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 가져온 로비 테이블을 검증하고 테이블에 지정한 리소스의 Addressables 주소를 등록합니다.
    /// </summary>
    public static class LobbyTableSetup
    {
        [MenuItem("Tools/Three Souls/Data/Validate & Register Lobby Tables")]
        public static void RegisterResources()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                throw new InvalidOperationException("로비 테이블 등록은 Edit 모드에서 실행하세요.");
            }
            LobbyTableValidation.Validate();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var tableGroup = settings.FindGroup("ScriptableObjectDatas")
                ?? throw new InvalidOperationException("ScriptableObjectDatas 그룹이 필요합니다.");
            var imageGroup = settings.FindGroup("Images")
                ?? throw new InvalidOperationException("Images 그룹이 필요합니다.");
            var resourceGroup = settings.FindGroup("LobbyResources")
                ?? settings.CreateGroup("LobbyResources", false, false, false, imageGroup.Schemas);
            var types = LobbyTables.GetTableTypes();
            var resources = new HashSet<string>(StringComparer.Ordinal);
            var equipment = LobbyTables.Get<EquipmentTableDataTable>().Items;
            for (var i = 0; i < equipment.Length; i++)
            {
                resources.Add(equipment[i].IconKey);
            }
            var heroes = LobbyTables.Get<LobbyHeroTableDataTable>().Items;
            for (var i = 0; i < heroes.Length; i++)
            {
                LobbyResourceCatalog.GetSprite(heroes[i].PortraitImagePath, heroes[i].PortraitSpriteName);
                LobbyResourceCatalog.GetAnimator(heroes[i].VillageAnimatorPath);
                resources.Add(heroes[i].PortraitImagePath);
                resources.Add(heroes[i].VillageAnimatorPath);
            }
            var potions = LobbyTables.Get<ConsumableTableDataTable>().Items;
            for (var i = 0; i < potions.Length; i++)
            {
                LobbyResourceCatalog.GetSprite(potions[i].IconPath, potions[i].IconSpriteName);
                resources.Add(potions[i].IconPath);
            }
            var paths = new string[resources.Count];
            resources.CopyTo(paths);
            for (var i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(paths[i]) == true || AssetDatabase.LoadMainAssetAtPath(paths[i]) == null)
                {
                    throw new InvalidOperationException("테이블 리소스 경로 누락: " + paths[i]);
                }
                var guid = AssetDatabase.AssetPathToGUID(paths[i]);
                var existing = settings.FindAssetEntry(guid, true);
                if (existing != null && existing.address != paths[i])
                {
                    throw new InvalidOperationException("기존 주소와 테이블 경로가 다릅니다: " + paths[i] + "/" + existing.address);
                }
            }
            for (var i = 0; i < types.Length; i++)
            {
                var path = LobbyTables.GetAddress(types[i]);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), tableGroup).address = path;
            }
            for (var i = 0; i < paths.Length; i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(paths[i]);
                if (settings.FindAssetEntry(guid, true) == null)
                {
                    settings.CreateOrMoveEntry(guid, resourceGroup).address = paths[i];
                }
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            ThreeSouls.Editor.AddressableKeysGenerator.Generate();
        }
    }
}
