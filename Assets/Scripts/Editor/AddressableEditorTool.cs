#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AutoGroupGenerator;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ThreeSouls.Editor
{
    /// <summary>
    /// Auto Group Generator가 만든 레이아웃을 에셋의 부모 폴더 기준으로 다시 구성한다.
    ///
    /// - Assets/Textures/Character/Body.png -> Auto_Textures_Character
    /// - Packages/com.unity.render-pipelines.core/... -> Auto_Packages_com_unity_render_pipelines_core
    /// - 이미 Addressable 폴더 아래에 포함된 파일은 별도 엔트리로 다시 만들지 않는다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ParentFolderAutoGroupOutputRule",
        menuName = "Addressables/Auto Group Generator/Rules/Parent Folder Output Rule")]
    public sealed class ParentFolderAutoGroupOutputRule : OutputRule
    {
        [Header("Group Naming")]
        [SerializeField]
        private string m_GroupNamePrefix = "Auto_";

        [SerializeField]
        [Tooltip("Packages 에셋을 깊은 폴더별로 나누지 않고 패키지 하나당 한 그룹으로 묶습니다.")]
        private bool m_GroupPackageAssetsByPackageRoot = true;

        [Header("Folder Entry Protection")]
        [SerializeField]
        [Tooltip("Addressable 폴더 아래 파일을 생성 레이아웃에서 제외합니다.")]
        private bool m_ProtectRegisteredFolderChildren = true;

        [SerializeField]
        [Tooltip("이전 자동 생성에서 명시적 엔트리로 풀린 폴더 하위 파일을 제거합니다. 파일 자체는 삭제하지 않습니다.")]
        private bool m_RemoveExistingExplicitFolderChildren = true;

        [SerializeField]
        [Tooltip("Addressables Groups 창의 Missing Reference 그룹 참조를 정리합니다.")]
        private bool m_RemoveMissingGroupReferences = true;

        protected override bool DoesMatchSelectionCriteria(GroupLayout groupLayout)
        {
            return groupLayout != null;
        }

        public override void Refine()
        {
            AddressableAssetSettings settings = AddressableSettingsService.GetSettings();

            if (settings == null)
            {
                throw new InvalidOperationException("AddressableAssetSettings를 찾을 수 없습니다.");
            }

            // 이 옵션이 켜져 있으면 레이아웃에 폴더 GUID가 없다는 이유로
            // 보존하려는 폴더 엔트리를 Cleanup 단계에서 다시 삭제한다.
            if (m_DataContainer.Settings.RemoveUnnecessaryEntries)
            {
                throw new InvalidOperationException(
                    "Parent Folder Output Rule을 사용할 때는 Auto Group Generator Settings의 " +
                    "Remove Unnecessary Entries 옵션을 꺼야 합니다.");
            }

            if (m_RemoveMissingGroupReferences)
            {
                RemoveMissingGroupReferences(settings);
            }

            HashSet<string> registeredFolders = CollectRegisteredAddressableFolders(settings);

            if (m_ProtectRegisteredFolderChildren && m_RemoveExistingExplicitFolderChildren)
            {
                RemoveExplicitFolderChildren(settings, registeredFolders);
            }

            Dictionary<string, GroupLayout> layoutsByParentFolder = BuildParentFolderLayouts(registeredFolders);

            m_DataContainer.GroupLayout = layoutsByParentFolder;
            m_Selection = layoutsByParentFolder.Values.ToList();

            // OutputRule에 지정한 Addressable Group Template을 새 레이아웃에 적용한다.
            base.Refine();

            Debug.Log(
                $"[ParentFolderAutoGroup] 생성 레이아웃 {layoutsByParentFolder.Count}개, " +
                $"보호 중인 Addressable 폴더 {registeredFolders.Count}개");
        }

        private Dictionary<string, GroupLayout> BuildParentFolderLayouts(HashSet<string> registeredFolders)
        {
            var result = new Dictionary<string, GroupLayout>(StringComparer.Ordinal);
            var sourceLayouts = m_DataContainer.GroupLayout.Values.Where(layout => layout != null).OrderBy(layout => layout.Name, StringComparer.Ordinal);

            foreach (GroupLayout sourceLayout in sourceLayouts)
            {
                var nodes = sourceLayout.Nodes.Where(node => node != null).OrderBy(node => node.AssetPath, StringComparer.Ordinal);

                foreach (AssetNode node in nodes)
                {
                    var assetPath = NormalizePath(node.AssetPath);

                    if (string.IsNullOrEmpty(assetPath))
                    {
                        continue;
                    }

                    if (m_ProtectRegisteredFolderChildren && IsChildOfAnyFolder(assetPath, registeredFolders))
                    {
                        continue;
                    }

                    var groupName = GetParentFolderGroupName(assetPath);

                    if (string.IsNullOrEmpty(groupName))
                    {
                        continue;
                    }

                    if (result.TryGetValue(groupName, out GroupLayout targetLayout) == false)
                    {
                        targetLayout = new GroupLayout
                        {
                            Name = groupName,
                            Nodes = new HashSet<AssetNode>(),
                            Sources = new HashSet<AssetNode>()
                        };

                        result.Add(groupName, targetLayout);
                    }

                    targetLayout.Nodes.Add(node);
                    targetLayout.Sources.UnionWith(sourceLayout.Sources);
                }
            }

            foreach (GroupLayout layout in result.Values)
            {
                layout.HashOfSources = CalculateStableSourceHash(layout.Sources);
            }

            return result;
        }

        private static HashSet<string> CollectRegisteredAddressableFolders(AddressableAssetSettings settings)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (AddressableAssetGroup group in AddressableGroupService.GetAllGroups(settings))
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in AddressableGroupService.GetEntries(group))
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    var assetPath = NormalizePath(entry.AssetPath);

                    if (string.IsNullOrEmpty(assetPath) == false &&
                        AssetDatabase.IsValidFolder(assetPath))
                    {
                        result.Add(assetPath.TrimEnd('/'));
                    }
                }
            }

            return result;
        }

        private static void RemoveExplicitFolderChildren(AddressableAssetSettings settings, HashSet<string> registeredFolders)
        {
            if (registeredFolders.Count == 0)
            {
                return;
            }

            var entriesToRemove = new List<AddressableAssetEntry>();

            foreach (AddressableAssetGroup group in AddressableGroupService.GetAllGroups(settings))
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in AddressableGroupService.GetEntries(group))
                {
                    if (entry == null || entry.IsFolder)
                    {
                        continue;
                    }

                    var assetPath = NormalizePath(entry.AssetPath);

                    if (IsChildOfAnyFolder(assetPath, registeredFolders))
                    {
                        entriesToRemove.Add(entry);
                    }
                }
            }

            foreach (AddressableAssetEntry entry in entriesToRemove)
            {
                // Addressable 등록만 해제한다. 프로젝트 에셋 파일은 삭제되지 않는다.
                settings.RemoveAssetEntry(entry.guid, postEvent: true);
            }

            if (entriesToRemove.Count > 0)
            {
                Debug.Log($"[ParentFolderAutoGroup] 폴더가 이미 포함하는 명시적 하위 엔트리 " + $"{entriesToRemove.Count}개를 정리했습니다.");
            }
        }

        private string GetParentFolderGroupName(string assetPath)
        {
            const string assetsPrefix = "Assets/";
            const string packagesPrefix = "Packages/";

            string relativeParent;

            if (assetPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var relativeAssetPath = assetPath.Substring(assetsPrefix.Length);
                var separatorIndex = relativeAssetPath.LastIndexOf('/');

                relativeParent = separatorIndex >= 0? relativeAssetPath.Substring(0, separatorIndex) : "AssetsRoot";
            }
            else if (assetPath.StartsWith(packagesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var parts = assetPath.Split('/');

                if (m_GroupPackageAssetsByPackageRoot && parts.Length >= 2)
                {
                    relativeParent = $"Packages/{parts[1]}";
                }
                else
                {
                    var separatorIndex = assetPath.LastIndexOf('/');

                    relativeParent = separatorIndex >= 0 ? assetPath.Substring(0, separatorIndex) : "Packages";
                }
            }
            else
            {
                return string.Empty;
            }

            return $"{m_GroupNamePrefix}{SanitizeGroupName(relativeParent)}";
        }

        private static bool IsChildOfAnyFolder(string assetPath, IEnumerable<string> folderPaths)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            foreach (string folderPath in folderPaths)
            {
                if (assetPath.Length <= folderPath.Length)
                {
                    continue;
                }

                if (assetPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase) &&
                    assetPath[folderPath.Length] == '/')
                {
                    return true;
                }
            }

            return false;
        }

        private static string SanitizeGroupName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed";
            }

            var builder = new StringBuilder(value.Length);
            var previousWasSeparator = false;

            foreach (char character in value)
            {
                var isAllowed = char.IsLetterOrDigit(character) || character == '-' || character == '_';

                if (isAllowed)
                {
                    builder.Append(character);
                    previousWasSeparator = false;
                }
                else if (previousWasSeparator == false)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            return builder.ToString().Trim('_');
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }

        private static int CalculateStableSourceHash(IEnumerable<AssetNode> sources)
        {
            unchecked
            {
                int hash = 17;

                foreach (string guid in sources
                             .Where(source => source != null)
                             .Select(source => source.Guid.ToString())
                             .OrderBy(value => value, StringComparer.Ordinal))
                {
                    foreach (char character in guid)
                    {
                        hash = hash * 31 + character;
                    }
                }

                return hash;
            }
        }

        private static void RemoveMissingGroupReferences(AddressableAssetSettings settings)
        {
            var removedCount = 0;

            for (int index = settings.groups.Count - 1; index >= 0; index--)
            {
                if (settings.groups[index] != null)
                {
                    continue;
                }

                settings.groups.RemoveAt(index);
                removedCount++;
            }

            if (removedCount == 0)
            {
                return;
            }

            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.GroupRemoved,
                null,
                postEvent: true,
                settingsModified: true);

            Debug.Log(
                $"[ParentFolderAutoGroup] Missing Reference 그룹 참조 " +
                $"{removedCount}개를 정리했습니다.");
        }
    }
}
#endif
