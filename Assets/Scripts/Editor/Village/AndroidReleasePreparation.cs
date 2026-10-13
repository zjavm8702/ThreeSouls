using System;
using System.Reflection;
using System.Text.RegularExpressions;
using ThreeSouls.Persistence;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    /// <summary>Local Android release settings and public Google identifier synchronization.</summary>
    public static class AndroidReleasePreparation
    {
        public const string ProfilePath = "Assets/Settings/Build Profiles/Android Google Play Release.asset";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [MenuItem("Tools/Three Souls/Release/Apply Android Release Settings")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode only");
            BuildProfile.SetActiveBuildProfile(null);
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
            // Minimal stripping protects reflection, pooled UI and authentication plugin types.
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Minimal);
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildAppBundle = true;

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
            if (profile == null)
            {
                var source = "Assets/Settings/Build Profiles/Android\u2122.asset";
                if (!AssetDatabase.CopyAsset(source, ProfilePath)) throw new InvalidOperationException("Android build profile copy failed");
                profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
                profile.name = "Android Google Play Release";
            }
            // Unity 6.5 exposes profile creation, but Player Settings overrides are editor-internal.
            // Copy through the same method used by the Build Profiles window, never edit YAML.
            var create = typeof(BuildProfile).GetMethod("CreatePlayerSettingsFromGlobal", Members);
            if (create == null) throw new NotSupportedException("Player Settings override API changed");
            create.Invoke(profile, null);
            BuildProfile.SetActiveBuildProfile(profile);
            var platform = typeof(BuildProfile).GetProperty("platformBuildProfile", Members).GetValue(profile);
            Set(platform, "development", false);
            Set(platform, "allowDebugging", false);
            Set(platform, "connectProfiler", false);
            Set(platform, "buildAppBundle", true);
            var compression = platform.GetType().GetProperty("compressionType", Members);
            compression.SetValue(platform, Enum.Parse(compression.PropertyType, "Lz4HC"));
            var symbols = platform.GetType().GetProperty("debugSymbolLevel", Members);
            symbols.SetValue(platform, Enum.Parse(symbols.PropertyType, "SymbolTable"));
            typeof(BuildProfile).GetMethod("SerializePlayerSettings", Members).Invoke(profile, null);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("Android release profile applied. Final package/company, Google console IDs and upload signing still require configuration.");
        }

        private static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Members);
            if (property == null || !property.CanWrite) throw new NotSupportedException("Build Profile property missing: " + name);
            property.SetValue(target, value);
        }

        [MenuItem("Tools/Three Souls/Release/Sync Google Play Public IDs")]
        public static void SyncGoogle()
        {
            var config = GameServicesConfig.Load();
            if (config == null || !Regex.IsMatch(config.GoogleGameId ?? "", "^[0-9]+$") ||
                !Regex.IsMatch(config.GoogleWebClientId ?? "", "^[0-9]+-[a-zA-Z0-9]+\\.apps\\.googleusercontent\\.com$") ||
                !config.GoogleWebClientId.StartsWith(config.GoogleGameId + "-", StringComparison.Ordinal))
                throw new InvalidOperationException("Enter matching Google Play Game ID and OAuth Web Client ID in GameServicesConfig first.");
            if (!GooglePlayGames.Editor.GPGSAndroidSetupUI.PerformSetup(config.GoogleWebClientId, config.GoogleGameId, null))
                throw new InvalidOperationException("Google Play Games plugin setup failed");
            AssetDatabase.SaveAssets();
            Debug.Log("Google Play public IDs synchronized. Configure the Google provider secret in Unity Dashboard and test sign-in on Android.");
        }
    }
}
