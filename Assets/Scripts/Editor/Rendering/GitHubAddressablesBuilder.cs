using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    [CreateAssetMenu(menuName = "Addressables/Content Builders/Three Souls GitHub Publisher")]
    public sealed class GitHubAddressablesBuilder : BuildScriptPackedMode
    {
        public override string Name => "Three Souls Packed Build + GitHub Publish";
        public static bool Publishing => Upload != null;
        private static Process Upload;
        private static Task<string> Output, Errors;

        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput input)
        {
            if (Publishing) throw new InvalidOperationException("Addressables publishing is already in progress");
            GameSpriteAtlasPrebuild.Prepare();
            var result = base.BuildDataImplementation<TResult>(input);
            if (result != null && string.IsNullOrEmpty(result.Error)) Publish(input.AddressableSettings);
            return result;
        }

        private static void Publish(AddressableAssetSettings settings)
        {
            var profiles = settings.profileSettings;
            var target = EditorUserBuildSettings.activeBuildTarget.ToString();
            var load = profiles.EvaluateString(settings.activeProfileId, profiles.GetValueByName(settings.activeProfileId, "Remote.LoadPath"));
            // Verification/offline profiles may build successfully but must never deploy.
            if (load.TrimEnd('/') != "https://zjavm8702.github.io/AddressableAssetsData/" + target)
            { UnityEngine.Debug.Log("Addressables publish skipped: this profile uses another load path."); return; }
            var source = Path.GetFullPath(profiles.EvaluateString(settings.activeProfileId, profiles.GetValueByName(settings.activeProfileId, "Remote.BuildPath")));
            var snapshot = Path.GetFullPath("Library/AddressablesPublish/Staging/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshot);
            foreach (var file in Directory.GetFiles(source))
                if (Array.IndexOf(new[] { ".bundle", ".json", ".hash" }, Path.GetExtension(file)) >= 0)
                    File.Copy(file, Path.Combine(snapshot, Path.GetFileName(file)));
            var script = Path.GetFullPath("scripts/Publish-Addressables.ps1");
            var info = new ProcessStartInfo("powershell.exe")
            {
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" -SourceDirectory \"" + snapshot + "\" -Target " + target,
                WorkingDirectory = Directory.GetCurrentDirectory(), UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            Upload = Process.Start(info);
            Output = Upload.StandardOutput.ReadToEndAsync(); Errors = Upload.StandardError.ReadToEndAsync();
            EditorApplication.update += Observe;
            UnityEngine.Debug.Log("Addressables built successfully. GitHub upload started; credentials use the existing Git credential manager.");
        }

        private static void Observe()
        {
            if (!Upload.HasExited || !Output.IsCompleted || !Errors.IsCompleted) return;
            EditorApplication.update -= Observe;
            var log = Output.Result + "\n" + Errors.Result;
            Directory.CreateDirectory("Library/AddressablesPublish");
            File.WriteAllText("Library/AddressablesPublish/LastPublish.log", log);
            if (Upload.ExitCode == 0) UnityEngine.Debug.Log("Addressables GitHub upload completed. " + log);
            else UnityEngine.Debug.LogError("Addressables built, but GitHub upload failed. See Library/AddressablesPublish/LastPublish.log. " + log);
            Upload.Dispose(); Upload = null;
        }
    }
}
