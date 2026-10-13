using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThreeSouls.Utility;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace ThreeSouls.Editor
{
    public sealed class SpriteLibrarySpriteReplacerWindow : EditorWindow
    {
        [SerializeField] private SpriteLibraryAsset library;
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private Texture2D targetTexture;
        [SerializeField] private string sourcePrefix = "";
        [SerializeField] private string targetPrefix = "";
        private readonly List<Replacement> rows = new();
        private Vector2 scroll;
        private string message = "";
        private string backupPath;
        private string backupAssetPath;

        private sealed class Replacement
        {
            public string Category;
            public string Label;
            public Sprite Before;
            public Sprite After;
        }

        [MenuItem("Tools/Three Souls/Sprite Library/Replace Sheet Sprites")]
        public static void Open()
        {
            GetWindow<SpriteLibrarySpriteReplacerWindow>("Sprite Library 교체");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("선택한 Sprite Library의 A 이미지 연결만 B로 교체합니다. 카테고리·라벨과 원본 이미지는 유지합니다. B는 Sprite Editor에서 먼저 잘라주세요.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            library = (SpriteLibraryAsset)EditorGUILayout.ObjectField("Sprite Library", library, typeof(SpriteLibraryAsset), false);
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField("원본 이미지 A", sourceTexture, typeof(Texture2D), false);
            targetTexture = (Texture2D)EditorGUILayout.ObjectField("새 이미지 B", targetTexture, typeof(Texture2D), false);
            sourcePrefix = EditorGUILayout.TextField("A 이름 접두사", sourcePrefix);
            targetPrefix = EditorGUILayout.TextField("B 이름 접두사", targetPrefix);
            if (EditorGUI.EndChangeCheck()) { rows.Clear(); message = ""; }
            EditorGUILayout.HelpBox("이름이 같으면 바로 매칭합니다. Archer_Idle_Up_0 → Knight_Idle_Up_0라면 접두사를 Archer_ / Knight_로 입력하세요. 이름이나 배치가 다르면 미리보기에서 직접 수정할 수 있습니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("교체 미리보기")) Run(BuildPreview);
                if (rows.Count > 0)
                {
                    var matched = rows.Count(r => IsTargetSprite(r.After));
                    EditorGUILayout.LabelField($"대상 {rows.Count}개 / 연결 가능 {matched}개 / 미매칭 {rows.Count - matched}개");
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    foreach (var row in rows)
                    {
                        EditorGUILayout.LabelField(row.Category + " / " + row.Label, EditorStyles.boldLabel);
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField("현재", row.Before, typeof(Sprite), false);
                        row.After = (Sprite)EditorGUILayout.ObjectField("교체 후", row.After, typeof(Sprite), false);
                        if (row.After != null && !IsTargetSprite(row.After))
                            EditorGUILayout.HelpBox("B 이미지에서 잘린 스프라이트만 선택할 수 있습니다.", MessageType.Warning);
                    }
                    EditorGUILayout.EndScrollView();
                    using (new EditorGUI.DisabledScope(matched != rows.Count))
                        if (GUILayout.Button($"{rows.Count}개 연결 교체 (자동 백업)")) Run(ApplyReplacement);
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath)))
                    if (GUILayout.Button("마지막 교체 복원")) Run(RestoreBackup);
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                message = ex.Message;
                ThreeSoulsLog.Error("Sprite Library 교체: " + ex);
            }
        }

        private string ValidateInputs()
        {
            if (library == null || sourceTexture == null || targetTexture == null)
                throw new InvalidOperationException("Sprite Library와 이미지 A, B를 모두 선택하세요.");
            if (sourceTexture == targetTexture)
                throw new InvalidOperationException("A와 B는 서로 다른 이미지여야 합니다.");
            var path = AssetDatabase.GetAssetPath(library);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".spriteLib", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Assets 폴더의 .spriteLib 에셋을 선택하세요.");
            if (Sprites(targetTexture).Length == 0)
                throw new InvalidOperationException("B에 잘린 스프라이트가 없습니다. Sprite (2D and UI) / Multiple로 설정하고 먼저 Slice하세요.");
            return path;
        }

        private static Sprite[] Sprites(Texture2D texture)
        {
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture)).OfType<Sprite>().ToArray();
        }

        private bool IsTargetSprite(Sprite sprite)
        {
            return sprite != null && AssetDatabase.GetAssetPath(sprite) == AssetDatabase.GetAssetPath(targetTexture);
        }

        private void BuildPreview()
        {
            ValidateInputs();
            rows.Clear();
            var targets = Sprites(targetTexture).GroupBy(s => s.name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            var sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            foreach (var category in library.GetCategoryNames())
            foreach (var label in library.GetCategoryLabelNames(category))
            {
                var sprite = library.GetSprite(category, label);
                if (sprite == null || AssetDatabase.GetAssetPath(sprite) != sourcePath) continue;
                var name = sprite.name;
                if (!string.IsNullOrEmpty(sourcePrefix) && name.StartsWith(sourcePrefix, StringComparison.Ordinal))
                    name = targetPrefix + name.Substring(sourcePrefix.Length);
                Sprite next = null;
                if (targets.TryGetValue(name, out var candidates) && candidates.Length == 1) next = candidates[0];
                rows.Add(new Replacement { Category = category, Label = label, Before = sprite, After = next });
            }
            message = rows.Count == 0 ? "이 라이브러리에 A 이미지의 연결이 없습니다." : "미매칭 항목을 확인하세요. 모든 항목이 B에 연결되어야 적용할 수 있습니다.";
        }

        private static SerializedProperty Entry(SerializedObject source, string category, string label)
        {
            var categories = source.FindProperty("m_Library");
            if (categories == null) throw new InvalidOperationException("지원하지 않는 Sprite Library 저장 형식입니다.");
            for (var i = 0; i < categories.arraySize; i++)
            {
                var cat = categories.GetArrayElementAtIndex(i);
                if (cat.FindPropertyRelative("m_Name").stringValue != category) continue;
                var entries = cat.FindPropertyRelative("m_OverrideEntries");
                for (var j = 0; j < entries.arraySize; j++)
                {
                    var entry = entries.GetArrayElementAtIndex(j);
                    if (entry.FindPropertyRelative("m_Name").stringValue == label) return entry;
                }
            }
            throw new InvalidOperationException($"소스 항목이 없습니다: {category}/{label}. 상속 항목은 라이브러리 에디터에서 로컬 오버라이드를 먼저 생성하세요.");
        }

        private void ApplyReplacement()
        {
            var path = ValidateInputs();
            if (rows.Count == 0 || rows.Any(r => !IsTargetSprite(r.After)))
                throw new InvalidOperationException("미리보기의 모든 항목을 B 스프라이트와 연결하세요.");
            foreach (var row in rows)
                if (library.GetSprite(row.Category, row.Label) != row.Before)
                    throw new InvalidOperationException("미리보기 이후 라이브러리가 변경됐습니다. 미리보기를 다시 실행하세요.");

            var loaded = InternalEditorUtility.LoadSerializedFileAndForget(path);
            try
            {
                if (loaded.Length != 1 || loaded[0].GetType().Name != "SpriteLibrarySourceAsset")
                    throw new InvalidOperationException("지원하지 않는 Sprite Library 소스 형식입니다.");
                var serialized = new SerializedObject(loaded[0]);
                foreach (var row in rows)
                {
                    var entry = Entry(serialized, row.Category, row.Label);
                    entry.FindPropertyRelative("m_SpriteOverride").objectReferenceValue = row.After;
                    if (!entry.FindPropertyRelative("m_FromMain").boolValue)
                        entry.FindPropertyRelative("m_Sprite").objectReferenceValue = row.After;
                }
                var categories = serialized.FindProperty("m_Library");
                for (var i = 0; i < categories.arraySize; i++)
                {
                    var category = categories.GetArrayElementAtIndex(i);
                    var entries = category.FindPropertyRelative("m_OverrideEntries");
                    var count = 0;
                    for (var j = 0; j < entries.arraySize; j++)
                    {
                        var entry = entries.GetArrayElementAtIndex(j);
                        if (!entry.FindPropertyRelative("m_FromMain").boolValue ||
                            entry.FindPropertyRelative("m_Sprite").objectReferenceValue != entry.FindPropertyRelative("m_SpriteOverride").objectReferenceValue)
                            count++;
                    }
                    category.FindPropertyRelative("m_EntryOverrideCount").intValue = count;
                }
                serialized.FindProperty("m_ModificationHash").longValue = DateTime.UtcNow.Ticks;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var directory = Path.GetFullPath("Temp/CodexBackups/SpriteLibraryReplacement");
                Directory.CreateDirectory(directory);
                backupPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + "_" + Guid.NewGuid().ToString("N") + ".spriteLib");
                backupAssetPath = path;
                File.Copy(path, backupPath, false);
                try
                {
                    InternalEditorUtility.SaveToSerializedFileAndForget(loaded, path, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var saved = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(path);
                    foreach (var row in rows)
                        if (saved == null || saved.GetSprite(row.Category, row.Label) != row.After)
                            throw new InvalidOperationException("저장 후 검증에 실패했습니다. 원본 연결을 복원합니다.");
                    library = saved;
                }
                catch
                {
                    File.Copy(backupPath, path, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    throw;
                }
                var appliedCount = rows.Count;
                rows.Clear();
                message = $"{appliedCount}개 연결을 교체했습니다. 카테고리·라벨은 유지됐습니다. 백업: {backupPath}";
                ThreeSoulsLog.Info(message);
            }
            finally
            {
                foreach (var obj in loaded) if (obj != null) Object.DestroyImmediate(obj);
            }
        }

        private void RestoreBackup()
        {
            if (!EditorUtility.DisplayDialog("마지막 교체 복원", "교체 이후 라이브러리를 수정했다면 그 수정도 되돌아갑니다. 마지막 교체 직전으로 복원할까요?", "복원", "취소")) return;
            File.Copy(backupPath, backupAssetPath, true);
            AssetDatabase.ImportAsset(backupAssetPath, ImportAssetOptions.ForceUpdate);
            library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(backupAssetPath);
            rows.Clear();
            message = "마지막 교체 직전의 라이브러리 연결을 복원했습니다.";
            backupPath = null;
        }
    }
}
