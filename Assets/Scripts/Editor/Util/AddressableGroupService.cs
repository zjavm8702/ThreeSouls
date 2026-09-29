#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace ThreeSouls.Editor
{
    public static class AddressableGroupService
    {
        private static readonly Dictionary<string, string> DependencyGroupContainer = new(StringComparer.OrdinalIgnoreCase)
            {
                { ".mat", "Material" },
                { ".fbx", "FBX" },
                { ".controller", "Animator" },
                { ".png", "Texture" },
                { ".prefab", "Prefab" },
                { ".shader", "Shader" }
            };

        public static List<AddressableAssetGroup> GetAllGroups(AddressableAssetSettings settings = null)
        {
            settings = settings ? settings : AddressableSettingsService.GetSettings();
            return settings != null ? settings.groups : new List<AddressableAssetGroup>();
        }

        public static bool TryGetDependencyGroupName(string assetPath, out string groupName)
        {
            groupName = string.Empty;

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            return DependencyGroupContainer.TryGetValue(Path.GetExtension(assetPath), out groupName);
        }

        public static AddressableAssetEntry FindEntryInGroup(AddressableAssetGroup group, string assetPath)
        {
            if (group == null || string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return string.IsNullOrEmpty(guid) ? null : group.GetAssetEntry(guid);
        }

        public static AddressableAssetEntry CreateOrMoveEntry(AddressableAssetGroup group, string assetPath, AddressableAssetSettings settings = null)
        {
            settings = settings ? settings : AddressableSettingsService.GetSettings();

            if (settings == null || group == null || string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return string.IsNullOrEmpty(guid) ? null : settings.CreateOrMoveEntry(guid, group);
        }

        public static List<AddressableAssetEntry> GetEntries(AddressableAssetGroup group)
        {
            return group != null
                ? new List<AddressableAssetEntry>(group.entries)
                : new List<AddressableAssetEntry>();
        }

        public static List<AddressableAssetEntry> GetAllAssets(AddressableAssetGroup group)
        {
            List<AddressableAssetEntry> addressableAssetEntries = new List<AddressableAssetEntry>();
            group.GatherAllAssets(addressableAssetEntries, includeSelf: false, recurseAll: true, includeSubObjects: false);
            return addressableAssetEntries;
        }

        public static AddressableAssetGroup FindOrCreateGroup(string groupName, List<AddressableAssetGroupSchema> schemasToCopy, 
            bool setAsDefaultGroup = false, 
            bool readOnly = false,
            bool postEvent = true,
            AddressableAssetSettings settings = null)
        {
            settings = settings ? settings : AddressableSettingsService.GetSettings();

            if (settings == null || string.IsNullOrWhiteSpace(groupName))
            {
                return null;
            }

            return settings.FindGroup(groupName) ?? settings.CreateGroup(
                groupName,
                setAsDefaultGroup,
                readOnly,
                postEvent,
                schemasToCopy ?? new List<AddressableAssetGroupSchema>());
        }
    }
}
#endif
