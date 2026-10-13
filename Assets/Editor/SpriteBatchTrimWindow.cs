using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThreeSouls.Utility;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ThreeSouls.Editor
{
    public sealed class SpriteBatchTrimWindow : EditorWindow
    {
        private Texture2D texture;
        private int alphaThreshold = 32;
        private int padding = 1;
        private Vector2 uniformPivotValue = new Vector2(0.5f, 0f);
        private bool uniformPivotOnTrim;
        private string result = "";
        private readonly Dictionary<string, string> backups = new();

        [MenuItem("Tools/Three Souls/Sprites/Batch Trim")]
        public static void Open() => GetWindow<SpriteBatchTrimWindow>("일괄 Sprite Trim");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("먼저 Automatic Slice하세요. 기존 이름·ID와 PNG를 유지하며 전체 Trim 또는 피봇만 일괄 변경합니다.", MessageType.Info);
            texture = (Texture2D)EditorGUILayout.ObjectField("이미지", texture, typeof(Texture2D), false);
            alphaThreshold = EditorGUILayout.IntSlider("알파 기준", alphaThreshold, 1, 255);
            padding = EditorGUILayout.IntSlider("여백 (픽셀)", padding, 0, 8);
            EditorGUILayout.Space();
            uniformPivotValue = EditorGUILayout.Vector2Field("일괄 피봇 (0~1)", uniformPivotValue);
            uniformPivotValue = new Vector2(Mathf.Clamp01(uniformPivotValue.x), Mathf.Clamp01(uniformPivotValue.y));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bottom Center")) uniformPivotValue = new Vector2(0.5f, 0f);
            if (GUILayout.Button("Center")) uniformPivotValue = new Vector2(0.5f, 0.5f);
            if (GUILayout.Button("Bottom Left")) uniformPivotValue = Vector2.zero;
            EditorGUILayout.EndHorizontal();
            uniformPivotOnTrim = EditorGUILayout.Toggle("Trim 시에도 일괄 피봇 적용", uniformPivotOnTrim);
            EditorGUILayout.HelpBox("피봇 값은 각 잘린 사각형 기준입니다. Bottom Center=(0.5,0). 실제 발밑을 자동 인식하지 않으므로 눕는 동작이나 무기 위치에 따른 흔들림은 남을 수 있습니다.", MessageType.None);
            EditorGUILayout.HelpBox("기본 32로 시작하세요. 높일수록 희미한 픽셀을 무시하지만 가는 무기·외곽선이 영역 밖으로 잘릴 수 있습니다. 여백 1~2픽셀 권장. 떨어져 있던 무기 조각을 합치거나 피봇을 발밑으로 맞추는 기능은 아닙니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                using (new EditorGUI.DisabledScope(texture == null))
                {
                    if (GUILayout.Button("이 이미지 미리 계산")) Run(() => Process(new[] { texture }, false));
                    if (GUILayout.Button("이 이미지 전체 Trim")) Run(() => Process(new[] { texture }, true));
                    if (GUILayout.Button("이 이미지 피봇만 미리 계산")) Run(() => Process(new[] { texture }, false, false));
                    if (GUILayout.Button("이 이미지 피봇만 일괄 변경")) Run(() => Process(new[] { texture }, true, false));
                }
                if (GUILayout.Button("Project에서 선택한 이미지 미리 계산")) Run(() => Process(Selection.objects.OfType<Texture2D>().ToArray(), false));
                if (GUILayout.Button("Project에서 선택한 이미지 모두 Trim")) Run(() => Process(Selection.objects.OfType<Texture2D>().ToArray(), true));
                if (GUILayout.Button("Project에서 선택한 이미지 피봇 미리 계산")) Run(() => Process(Selection.objects.OfType<Texture2D>().ToArray(), false, false));
                if (GUILayout.Button("Project에서 선택한 이미지 피봇만 일괄 변경")) Run(() => Process(Selection.objects.OfType<Texture2D>().ToArray(), true, false));
                using (new EditorGUI.DisabledScope(backups.Count == 0))
                    if (GUILayout.Button("마지막 작업 복원")) Run(Restore);
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { result = ex.Message; ThreeSoulsLog.Error("일괄 Trim: " + ex); }
        }

        private sealed class Plan
        {
            public string Path;
            public ISpriteEditorDataProvider Provider;
            public SpriteRect[] Rects;
            public readonly HashSet<SpriteRect> Changes = new();
            public int Changed => Changes.Count;
            public int RectChanged;
            public int PivotChanged;
            public int Empty;
        }

        private void Process(Texture2D[] textures, bool apply, bool trim = true)
        {
            if (textures.Length == 0) throw new InvalidOperationException("PNG 이미지를 하나 이상 선택하세요.");
            var plans = textures.Distinct().Select(t => BuildPlanForOperation(t, trim, !trim || uniformPivotOnTrim)).ToArray();
            if (apply && plans.Any(p => p.Changed > 0))
            {
                backups.Clear();
                var backupDir = Path.GetFullPath("Temp/CodexBackups/SpriteBatchTrim/" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backupDir);
                foreach (var plan in plans.Where(p => p.Changed > 0))
                {
                    var backup = Path.Combine(backupDir, Guid.NewGuid().ToString("N") + ".meta");
                    File.Copy(plan.Path + ".meta", backup);
                    backups.Add(plan.Path, backup);
                }
                try
                {
                    foreach (var plan in plans.Where(p => p.Changed > 0))
                    {
                        plan.Provider.SetSpriteRects(plan.Rects);
                        plan.Provider.Apply();
                        AssetImporter.GetAtPath(plan.Path).SaveAndReimport();
                    }
                }
                catch
                {
                    RestoreFiles();
                    throw;
                }
            }
            result = (apply ? "작업 완료" : "미리 계산 — 아직 변경하지 않았습니다") + "\n" +
                string.Join("\n", plans.Select(p => $"{Path.GetFileName(p.Path)}: 전체 {p.Rects.Length}, 영역 변경 {p.RectChanged}, 피봇 변경 {p.PivotChanged}, 기준 이상 픽셀 없음 {p.Empty} (기존 영역 유지)"));
        }

        private Plan BuildPlan(Texture2D selected)
        {
            return BuildPlanForOperation(selected, true, uniformPivotOnTrim);
        }

        private Plan BuildPlanForOperation(Texture2D selected, bool trim, bool setUniformPivot)
        {
            var path = AssetDatabase.GetAssetPath(selected);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Assets 폴더의 PNG만 지원합니다.");
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Multiple)
                throw new InvalidOperationException(path + ": Sprite / Multiple로 설정하고 먼저 Slice하세요.");
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("Sprite 데이터 공급자가 없습니다: " + path);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            if (rects.Length == 0) throw new InvalidOperationException(path + ": 자른 영역이 없습니다. 먼저 Automatic Slice하세요.");
            var plan = new Plan { Path = path, Provider = provider, Rects = rects };
            if (!trim)
            {
                ApplyUniformPivot(plan);
                return plan;
            }
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(raw, File.ReadAllBytes(path))) throw new InvalidOperationException("PNG를 읽을 수 없습니다: " + path);
                var pixels = raw.GetPixels32();
                foreach (var sprite in rects)
                {
                    var old = sprite.rect;
                    var x0 = Mathf.Clamp(Mathf.FloorToInt(old.xMin), 0, raw.width);
                    var y0 = Mathf.Clamp(Mathf.FloorToInt(old.yMin), 0, raw.height);
                    var x1 = Mathf.Clamp(Mathf.CeilToInt(old.xMax), 0, raw.width);
                    var y1 = Mathf.Clamp(Mathf.CeilToInt(old.yMax), 0, raw.height);
                    var minX = x1; var minY = y1; var maxX = -1; var maxY = -1;
                    for (var y = y0; y < y1; y++)
                        for (var x = x0; x < x1; x++)
                        {
                            if (pixels[y * raw.width + x].a < alphaThreshold) continue;
                            minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                            maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                        }
                    if (maxX < minX) { plan.Empty++; continue; }
                    minX = Mathf.Max(x0, minX - padding); minY = Mathf.Max(y0, minY - padding);
                    maxX = Mathf.Min(x1 - 1, maxX + padding); maxY = Mathf.Min(y1 - 1, maxY + padding);
                    var next = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                    if (next == old) continue;
                    // Cropping must not move the previous pivot in texture coordinates.
                    var anchor = old.position + Vector2.Scale(old.size, sprite.pivot);
                    sprite.rect = next;
                    sprite.alignment = SpriteAlignment.Custom;
                    sprite.pivot = new Vector2((anchor.x - next.x) / next.width, (anchor.y - next.y) / next.height);
                    plan.RectChanged++;
                    plan.Changes.Add(sprite);
                }
            }
            finally { DestroyImmediate(raw); }
            if (setUniformPivot) ApplyUniformPivot(plan);
            return plan;
        }

        private void ApplyUniformPivot(Plan plan)
        {
            if (float.IsNaN(uniformPivotValue.x) || float.IsNaN(uniformPivotValue.y) ||
                float.IsInfinity(uniformPivotValue.x) || float.IsInfinity(uniformPivotValue.y))
                throw new InvalidOperationException("유효한 피봇 X, Y 값을 입력하세요.");
            var pivot = new Vector2(Mathf.Clamp01(uniformPivotValue.x), Mathf.Clamp01(uniformPivotValue.y));
            foreach (var sprite in plan.Rects)
            {
                if (sprite.pivot == pivot && sprite.alignment == SpriteAlignment.Custom) continue;
                sprite.pivot = pivot;
                sprite.alignment = SpriteAlignment.Custom;
                plan.PivotChanged++;
                plan.Changes.Add(sprite);
            }
        }

        private void RestoreFiles()
        {
            foreach (var pair in backups)
            {
                File.Copy(pair.Value, pair.Key + ".meta", true);
                AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate);
            }
        }

        private void Restore()
        {
            if (!EditorUtility.DisplayDialog("Trim 복원", "이후 변경한 이미지 Import 설정도 마지막 Trim 이전으로 되돌아갑니다. 복원할까요?", "복원", "취소")) return;
            RestoreFiles();
            backups.Clear();
            result = "마지막 작업 전 자르기 영역과 Import 설정을 복원했습니다.";
        }
    }
}
