// [UNITY-SKILL:SPRITEATLAS]
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ThreeSouls.EditorTools
{
    // Content must exist before the player packages StreamingAssets. Postprocess verifies
    // the result rather than rebuilding content too late to enter the finished player.
    public sealed class GameSpriteAtlasContentBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -10001;
        private static bool Built;
        public void OnPreprocessBuild(BuildReport report)
        {
            Built = false;
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result == null || !string.IsNullOrEmpty(result.Error)) throw new BuildFailedException(result?.Error ?? "Addressables build failed");
            Built = true;
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (!Built) throw new BuildFailedException("Atlas Addressables content was not built before packaging");
            Built = false;
        }
    }
}
