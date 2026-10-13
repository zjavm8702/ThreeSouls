// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    public sealed class GameSpriteAtlasPrebuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10002;
        public void OnPreprocessBuild(BuildReport report) => Prepare();

        public static string[] Prepare()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled) throw new BuildFailedException("Sprite packing disabled");
            const string output = "Assets/SpriteAtlases";
            if (!AssetDatabase.IsValidFolder(output)) AssetDatabase.CreateFolder("Assets", "SpriteAtlases");
            var groups = new SortedDictionary<string, List<UnityEngine.Object>>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles("Assets/Images", "*.png", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite) continue;
                var sourceChanged = importer.textureCompression != TextureImporterCompression.Uncompressed;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var androidSource = importer.GetPlatformTextureSettings("Android");
                if (androidSource.overridden && (androidSource.format != TextureImporterFormat.RGBA32
                    || androidSource.textureCompression != TextureImporterCompression.Uncompressed))
                {
                    androidSource.format = TextureImporterFormat.RGBA32;
                    androidSource.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SetPlatformTextureSettings(androidSource); sourceChanged = true;
                }
                if (sourceChanged) importer.SaveAndReimport();
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                if (sprites.Length == 0) throw new BuildFailedException("Sprite has no valid slices: " + path);
                var relative = path.Substring("Assets/Images/".Length);
                var name = Path.GetFileNameWithoutExtension(path);
                string group;
                if (relative.StartsWith("UIIcon/Skills/Archive/")) group = "UI_SkillArchive";
                else if (relative.StartsWith("UIIcon/Skills/")) group = "UI_Skills";
                else if (relative.StartsWith("UIIcon/") || relative.StartsWith("UIIMage/") || relative.StartsWith("EquipMent/")) group = "UI_Common";
                else if (relative.StartsWith("Map/StagePreviews/")) group = "Map_Stages_" + name.Split('_')[1];
                else if (relative.StartsWith("Map/Dungeons/")) group = "Map_Original_" + name;
                else if (relative.StartsWith("Map/")) group = "Map_" + relative.Split('/')[1];
                else if (relative.StartsWith("Character/Hero/")) group = "Hero_" + name;
                else if (relative.StartsWith("Character/Monster/")) group = "World_" + name;
                else if (relative.StartsWith("Character/NPC/Portraits/")) group = "UI_Portraits";
                else if (relative.StartsWith("Character/NPC/")) group = "World_NPC";
                else
                {
                    group = "World_" + Path.GetDirectoryName(relative).Replace('\\', '_').Replace('/', '_');
                    importer.GetSourceTextureWidthAndHeight(out var width, out var height);
                    if ((long)width * height > 1024 * 1024) group += "_" + name;
                }
                group = System.Text.RegularExpressions.Regex.Replace(group, "[^a-zA-Z0-9_]", "_");
                if (!groups.TryGetValue(group, out var packables)) groups[group] = packables = new List<UnityEngine.Object>();
                // A texture packable includes every sliced sprite while preserving sprite IDs.
                packables.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("Addressables settings missing");
            // GameSpriteAtlasContentBuild owns the content build before player packaging.
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            var atlasGroup = settings.FindGroup("SpriteAtlases") ?? settings.CreateGroup("SpriteAtlases", false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = atlasGroup.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            schema.BuildPath.SetVariableByName(settings, "Local.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
            var paths = new List<string>();
            foreach (var pair in groups)
            {
                var tag = "ThreeSoulsAtlas_" + pair.Key;
                var path = output + "/" + tag + ".spriteatlasv2";
                var current = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(path);
                var sameContent = current != null && current.GetPackables().Select(AssetDatabase.GetAssetPath).OrderBy(p => p)
                    .SequenceEqual(pair.Value.Select(AssetDatabase.GetAssetPath).OrderBy(p => p));
                if (!sameContent)
                {
                    var asset = new SpriteAtlasAsset { name = tag };
                    asset.Add(pair.Value.ToArray());
                    SpriteAtlasAsset.Save(asset, path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                var changed = !sameContent || importer.includeInBuild;
                importer.includeInBuild = false;
                var packing = importer.packingSettings;
                changed |= packing.enableRotation || packing.enableTightPacking || packing.padding != 4 || !packing.enableAlphaDilation;
                packing.enableRotation = false; packing.enableTightPacking = false; packing.padding = 4;
                packing.enableAlphaDilation = true; importer.packingSettings = packing;
                var texture = importer.textureSettings;
                changed |= texture.filterMode != (pair.Key.StartsWith("UI_Skill") ? FilterMode.Bilinear : FilterMode.Point)
                    || texture.generateMipMaps || texture.readable || !texture.sRGB;
                texture.filterMode = pair.Key.StartsWith("UI_Skill") ? FilterMode.Bilinear : FilterMode.Point;
                texture.generateMipMaps = false; texture.readable = false; texture.sRGB = true; importer.textureSettings = texture;
                foreach (var platform in new[] { "DefaultTexturePlatform", "Android", "Standalone", "iPhone" })
                {
                    var options = importer.GetPlatformSettings(platform);
                    changed |= options.overridden != (platform != "DefaultTexturePlatform") || options.maxTextureSize != 2048
                        || options.format != TextureImporterFormat.RGBA32 || options.textureCompression != TextureImporterCompression.Uncompressed;
                    options.name = platform; options.overridden = platform != "DefaultTexturePlatform";
                    options.maxTextureSize = 2048; options.format = TextureImporterFormat.RGBA32;
                    options.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SetPlatformSettings(options);
                }
                if (changed) importer.SaveAndReimport();
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), atlasGroup);
                entry.address = tag; entry.SetLabel("ThreeSouls.SpriteAtlas", true, true);
                paths.Add(path);
            }
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            foreach (var group in settings.groups.Where(g => g != null))
            {
                AssetDatabase.SaveAssetIfDirty(group);
                foreach (var item in group.Schemas) AssetDatabase.SaveAssetIfDirty(item);
            }
            return paths.ToArray();
        }
    }
}
