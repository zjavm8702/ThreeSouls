#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreeSouls.Editor
{
    public static class AddressableAssetMover
    {
        public static string GetAssetName(Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? asset.name : Path.GetFileNameWithoutExtension(path);
        }

        public static string MoveAsset(Object asset, string destinationDirectory)
        {
            if (asset == null || string.IsNullOrWhiteSpace(destinationDirectory))
            {
                return string.Empty;
            }

            destinationDirectory = destinationDirectory.Replace('\\', '/').TrimEnd('/');
            if (EnsureAssetDirectory(destinationDirectory) == false)
            {
                Debug.LogError($"유효하지 않은 에셋 폴더입니다: {destinationDirectory}");
                return string.Empty;
            }

            var sourcePath = AssetDatabase.GetAssetPath(asset);
            var assetName = $"{GetAssetName(asset)}{Path.GetExtension(sourcePath)}";
            var destinationPath = $"{destinationDirectory}/{assetName}";
            var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);

            if (string.IsNullOrEmpty(error) == false)
            {
                Debug.LogError(error);
                return string.Empty;
            }

            return destinationPath;
        }

        private static bool EnsureAssetDirectory(string directory)
        {
            var parts = directory.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                return false;
            }

            var parent = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                {
                    continue;
                }

                var current = $"{parent}/{parts[i]}";
                if (AssetDatabase.IsValidFolder(current) == false)
                {
                    AssetDatabase.CreateFolder(parent, parts[i]);
                }

                parent = current;
            }

            return true;
        }
    }
}
#endif
