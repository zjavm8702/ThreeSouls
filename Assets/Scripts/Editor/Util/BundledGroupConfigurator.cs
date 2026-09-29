#if UNITY_EDITOR
using System;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace ThreeSouls.Editor
{
    public static class BundledGroupConfigurator
    {
        public static void AddBaseSchemas(AddressableAssetGroup group, bool postEvent = true)
        {
            if (group == null)
            {
                return;
            }

            if (group.HasSchema<BundledAssetGroupSchema>() == false)
            {
                group.AddSchema<BundledAssetGroupSchema>(postEvent);
            }

            if (group.HasSchema<ContentUpdateGroupSchema>() == false)
            {
                group.AddSchema<ContentUpdateGroupSchema>(postEvent);
            }
        }

        public static void AddSchema<T>(T schema, AddressableAssetGroup group, bool postEvent = true) where T : AddressableAssetGroupSchema
        {
            if (group == null || schema == null || group.HasSchema(schema.GetType()))
            {
                return;
            }

            group.AddSchema(schema, postEvent);
        }

        public static void ConfigureAllRemoteGroups(Action<bool, string> resultCallback = null, AddressableAssetSettings settings = null)
        {
            foreach (var group in AddressableGroupService.GetAllGroups(settings))
            {
                if (group == null || group.HasSchema<PlayerDataGroupSchema>())
                {
                    continue;
                }

                ConfigureRemoteGroup(group, resultCallback, settings);
            }
        }

        public static void ConfigureRemoteGroup(string groupName, Action<bool, string> resultCallback = null, AddressableAssetSettings settings = null)
        {
            settings = settings ? settings : AddressableSettingsService.GetSettings();
            ConfigureRemoteGroup(settings?.FindGroup(groupName), resultCallback, settings);
        }

        public static void ConfigureRemoteGroup(AddressableAssetGroup group, Action<bool, string> resultCallback = null, AddressableAssetSettings settings = null)
        {
            settings = settings ? settings : AddressableSettingsService.GetSettings();

            if (group == null || settings == null)
            {
                resultCallback?.Invoke(false, "Group or AddressableAssetSettings is null");
                return;
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                resultCallback?.Invoke(false, "BundledAssetGroupSchema does not exist");
                return;
            }

            var buildPathChanged = schema.BuildPath.SetVariableById(
                settings,
                settings.RemoteCatalogBuildPath.Id);
            var loadPathChanged = schema.LoadPath.SetVariableById(
                settings,
                settings.RemoteCatalogLoadPath.Id);

            if (buildPathChanged == false || loadPathChanged == false)
            {
                resultCallback?.Invoke(false, "Failed to change the remote build or load path");
                return;
            }

            ConfigureAdvancedOptions(group, resultCallback);
        }

        public static void ConfigureAdvancedOptions(AddressableAssetGroup group, Action<bool, string> resultCallback = null)
        {
            var schema = group?.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                resultCallback?.Invoke(false, "BundledAssetGroupSchema does not exist");
                return;
            }

            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZMA;
            schema.IncludeInBuild = true;
            schema.ForceUniqueProvider = false;
            schema.UseAssetBundleCache = true;
            schema.UseAssetBundleCrcForCachedBundles = true;
            schema.UseAssetBundleCrc = true;
            schema.UseUnityWebRequestForLocalBundles = false;
            schema.Timeout = 0;
            schema.RedirectLimit = -1;
            schema.RetryCount = 0;
            schema.IncludeAddressInCatalog = true;
            schema.IncludeGUIDInCatalog = false;
            schema.IncludeLabelsInCatalog = false;
            schema.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.FullPath;
            schema.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash;
            schema.AssetBundledCacheClearBehavior =
                BundledAssetGroupSchema.CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
            schema.AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;

            resultCallback?.Invoke(true, group.name);
        }
    }
}
#endif
