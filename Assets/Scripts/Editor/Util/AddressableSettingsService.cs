#if UNITY_EDITOR
using System;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace ThreeSouls.Editor
{
    public static class AddressableSettingsService
    {
        public static AddressableAssetSettings GetSettings()
        {
            return AddressableAssetSettingsDefaultObject.Settings;
        }

        public static bool HasProfile(string profileName, AddressableAssetSettings settings = null)
        {
            settings = settings ? settings : GetSettings();

            if (settings == null || string.IsNullOrWhiteSpace(profileName))
            {
                return false;
            }

            var profileNames = settings.profileSettings.GetAllProfileNames();
            return profileNames?.Find(name =>
                string.Equals(name, profileName, StringComparison.Ordinal)) != null;
        }
    }
}
#endif
