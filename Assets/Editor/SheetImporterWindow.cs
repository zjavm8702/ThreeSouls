using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ThreeSouls.EditorTools.Sheets
{
    [Serializable]
    public sealed class SheetSource
    {
        public bool Enabled = true;
        public string Name = "Example";
        public string Spreadsheet = "1MIAskvGYRCR5Sng9lbNAeQAJbtTglY4ayluAAwKDXuA";
        public string Gid = "0";
        public string LocalCsv = "";
        public string UniqueKey = "";
    }

    [FilePath("UserSettings/ThreeSoulsSheetImporter.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class SheetImporterSettings : ScriptableSingleton<SheetImporterSettings>
    {
        public List<SheetSource> Sources = new List<SheetSource> { new SheetSource() };
        public void Persist()
        {
            Save(true); 
        }
    }

    public sealed class SheetImporterWindow : EditorWindow
    {
        private const string Root = "Assets/Generated/SheetData";
        private const string CodeDirectory = Root + "/Code";
        private const string EnumDirectory = CodeDirectory + "/Enums";
        private const string CsvDirectory = Root + "/Csv";
        private const string DataDirectory = Root + "/Data";
        private const string AssetDirectory = "Assets/ScriptableObjectDatas";
        private bool busy;
        private Vector2 scroll;
        [SerializeField] private string message = "테이블별 이름과 gid 또는 로컬 CSV를 등록하세요.";

        [MenuItem("Tools/Three Souls/Data/Google Sheets Importer")]
        public static void Open() { GetWindow<SheetImporterWindow>("Sheet Importer"); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("1행: 변수명 : 타입 / 2행: 설명 / 3행부터: 데이터\n로컬 CSV를 지정하면 구글 시트 대신 해당 파일을 읽습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(busy || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var settings = SheetImporterSettings.instance;
                EditorGUI.BeginChangeCheck();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                for (int i = 0; i < settings.Sources.Count; i++)
                {
                    SheetSource source = settings.Sources[i];
                    EditorGUILayout.BeginVertical("box");
                    source.Enabled = EditorGUILayout.Toggle("가져오기 대상", source.Enabled);
                    source.Name = EditorGUILayout.TextField("테이블명 (예: Hero)", source.Name);
                    source.Spreadsheet = EditorGUILayout.TextField("시트 URL 또는 ID", source.Spreadsheet);
                    source.Gid = EditorGUILayout.TextField("탭 gid", source.Gid);
                    source.UniqueKey = EditorGUILayout.TextField("고유 키 열 (선택)", source.UniqueKey);
                    source.LocalCsv = EditorGUILayout.TextField("로컬 CSV (선택)", source.LocalCsv);
                    if (GUILayout.Button("CSV 파일 선택"))
                    {
                        string path = EditorUtility.OpenFilePanel("CSV 선택", "", "csv");
                        if (!string.IsNullOrEmpty(path)) { source.LocalCsv = path; GUI.changed = true; }
                    }
                    bool remove = GUILayout.Button("목록에서 제거");
                    EditorGUILayout.EndVertical();
                    if (remove) { settings.Sources.RemoveAt(i--); GUI.changed = true; }
                }
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("테이블 추가"))
                {
                    settings.Sources.Add(new SheetSource { Name = "NewTable" }); GUI.changed = true;
                }
                if (EditorGUI.EndChangeCheck()) settings.Persist();
                if (GUILayout.Button("1. CSV 가져오기 + C# 생성")) ImportSources();
                if (GUILayout.Button("2. 컴파일 후 ScriptableObject + JSON 생성 / 갱신")) BuildAssets();
            }
            EditorGUILayout.HelpBox(message, MessageType.None);
        }

        private static List<SheetSource> GetSources()
        {
            var sources = SheetImporterSettings.instance.Sources.Where(s => s.Enabled).ToList();
            if (sources.Count == 0) throw new InvalidOperationException("가져올 테이블을 선택하세요.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SheetSource source in sources)
            {
                SheetTableCodec.ValidateName(source.Name);
                if (!names.Add(source.Name)) throw new FormatException("테이블 이름 중복: " + source.Name);
            }
            return sources;
        }

        private static string CsvPath(SheetSource s) { return CsvDirectory + "/" + s.Name + ".csv"; }
        private static string CodePath(SheetSource s) { return CodeDirectory + "/" + s.Name + "Table.cs"; }
        private static string AssetPath(SheetSource s) { return AssetDirectory + "/" + s.Name + ".asset"; }

        private static async Task<string> Fetch(SheetSource source)
        {
            if (!string.IsNullOrWhiteSpace(source.LocalCsv))
                return File.ReadAllText(source.LocalCsv, Encoding.UTF8);
            string id = source.Spreadsheet.Trim();
            if (id.StartsWith("https://docs.google.com/spreadsheets/d/", StringComparison.Ordinal))
            {
                var match = Regex.Match(id, @"/spreadsheets/d/([A-Za-z0-9_-]+)");
                if (!match.Success) throw new FormatException("시트 URL을 확인하세요.");
                id = match.Groups[1].Value;
            }
            if (!Regex.IsMatch(id, @"\A[A-Za-z0-9_-]+\z") || !Regex.IsMatch(source.Gid, @"\A[0-9]+\z"))
                throw new FormatException("시트 ID와 숫자 gid를 확인하세요.");
            string url = "https://docs.google.com/spreadsheets/d/" + id + "/export?format=csv&gid=" + source.Gid;
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 30;
                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Delay(50);
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException(source.Name + ": HTTP " + request.responseCode + " " + request.error +
                        "\n비공개 시트는 Google Sheets에서 CSV를 다운로드하여 로컬 CSV로 지정하세요.");
                string text = request.downloadHandler.text;
                string contentType = request.GetResponseHeader("Content-Type") ?? "";
                if (contentType.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0 || text.TrimStart().StartsWith("<"))
                    throw new IOException(source.Name + ": CSV 대신 로그인/HTML 페이지가 반환되었습니다. 로컬 CSV를 사용하세요.");
                return text;
            }
        }

        private async void ImportSources()
        {
            busy = true;
            // 다운로드 중 스크립트 리로드를 늦추고 finally에서 반드시 해제합니다.
            EditorApplication.LockReloadAssemblies();
            try
            {
                var sources = GetSources();
                var csvTexts = new List<string>();
                var codeTexts = new List<string>();
                var enums = new Dictionary<string, SheetTableCodec.EnumDefinition>(StringComparer.Ordinal);
                if (Directory.Exists(EnumDirectory))
                    foreach (string path in Directory.GetFiles(EnumDirectory, "*.cs"))
                    {
                        var definition = SheetTableCodec.ReadEnumCode(File.ReadAllText(path));
                        if (Path.GetFileNameWithoutExtension(path) != definition.Name)
                            throw new IOException("enum 파일명과 선언 이름이 다릅니다: " + path);
                        enums.Add(definition.Name, definition);
                    }
                foreach (SheetSource source in sources)
                {
                    message = source.Name + " 읽기 / 검증 중..."; Repaint();
                    string csv = await Fetch(source);
                    var table = SheetTableCodec.Read(source.Name, csv, source.UniqueKey, enums);
                    string code = SheetTableCodec.GenerateCode(table);
                    if (File.Exists(CodePath(source)) && !File.ReadAllText(CodePath(source)).StartsWith(SheetTableCodec.Marker, StringComparison.Ordinal))
                        throw new IOException("자동 생성 파일이 아니어서 덮어쓰지 않습니다: " + CodePath(source));
                    csvTexts.Add(csv); codeTexts.Add(code);
                }
                var outputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (SheetSource source in sources)
                    foreach (string suffix in new[] { "Row", "Payload", "Table" }) outputNames.Add(source.Name + suffix);
                foreach (var definition in enums.Values)
                    if (!outputNames.Add(definition.Name))
                        throw new FormatException("생성 타입 이름이 중복됩니다: " + definition.Name);
                // 전체 입력 검증 성공 후에만 출력합니다. I/O 오류까지 전체 롤백하는 트랜잭션은 아닙니다.
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("가져오는 동안 Play가 시작되어 저장을 중단했습니다. Edit 모드에서 다시 실행하세요.");
                Directory.CreateDirectory(CodeDirectory); Directory.CreateDirectory(CsvDirectory);
                Directory.CreateDirectory(EnumDirectory);
                foreach (var definition in enums.Values)
                    WriteChanged(EnumDirectory + "/" + definition.Name + ".cs", SheetTableCodec.GenerateEnumCode(definition));
                for (int i = 0; i < sources.Count; i++)
                {
                    string legacyCode = CodeDirectory + "/" + sources[i].Name + ".cs";
                    if (File.Exists(legacyCode))
                    {
                        if (File.Exists(CodePath(sources[i])) || !File.ReadAllText(legacyCode).StartsWith(SheetTableCodec.Marker, StringComparison.Ordinal))
                            throw new IOException("이전 생성 코드 이동 경로를 확인하세요: " + legacyCode);
                        string error = AssetDatabase.MoveAsset(legacyCode, CodePath(sources[i]));
                        if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    }
                    WriteChanged(CsvPath(sources[i]), csvTexts[i]);
                    WriteChanged(CodePath(sources[i]), codeTexts[i]);
                }
                message = sources.Count + "개 테이블 CSV/C# 및 enum 생성 완료. 컴파일이 끝나면 2번 버튼을 누르세요.";
                AssetDatabase.Refresh();
            }
            catch (Exception e) { message = e.Message; }
            finally { EditorApplication.UnlockReloadAssemblies(); busy = false; if (this != null) Repaint(); }
        }

        private sealed class PendingAsset
        {
            public SheetSource Source;
            public ScriptableObject Staged;
            public ScriptableObject Existing;
            public string Json;
            public string PreviousPath;
        }

        private static Type FindType(string fullName)
        {
            Type[] types = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).Where(t => t != null).ToArray();
            if (types.Length != 1) throw new InvalidOperationException("생성 타입을 찾을 수 없거나 중복됩니다: " + fullName + "\n1번 실행 후 Console의 컴파일 오류를 확인하세요.");
            return types[0];
        }

        private void BuildAssets()
        {
            var pending = new List<PendingAsset>();
            try
            {
                foreach (SheetSource source in GetSources())
                {
                    var table = SheetTableCodec.Read(source.Name, File.ReadAllText(CsvPath(source), Encoding.UTF8), source.UniqueKey);
                    if (!File.Exists(CodePath(source)) || File.ReadAllText(CodePath(source)) != SheetTableCodec.GenerateCode(table))
                        throw new InvalidOperationException(source.Name + ": CSV와 C# 구조가 다릅니다. 1번을 다시 실행하세요.");
                    string prefix = SheetTableCodec.GeneratedNamespace + "." + source.Name;
                    Type rowType = FindType(prefix + "Row");
                    Type tableType = FindType(prefix + "Table");
                    Type payloadType = FindType(prefix + "Payload");
                    FieldInfo[] fields = table.Columns.Select(c => rowType.GetField(c.Name)).ToArray();
                    for (int i = 0; i < fields.Length; i++)
                        if (fields[i] == null || fields[i].FieldType != table.Columns[i].Type)
                            throw new InvalidOperationException(source.Name + ": 컴파일된 필드가 CSV와 다릅니다. 컴파일 완료 여부를 확인하세요.");
                    if (rowType.GetFields(BindingFlags.Instance | BindingFlags.Public).Length != fields.Length)
                        throw new InvalidOperationException(source.Name + ": 이전 스키마가 로드되어 있습니다. 컴파일을 확인하세요.");
                    Array rows = Array.CreateInstance(rowType, table.Rows.Count);
                    for (int i = 0; i < table.Rows.Count; i++)
                    {
                        object row = Activator.CreateInstance(rowType);
                        for (int j = 0; j < fields.Length; j++) fields[j].SetValue(row, table.Rows[i][j]);
                        rows.SetValue(row, i);
                    }
                    string previousPath = DataDirectory + "/" + source.Name + ".asset";
                    bool hasPrevious = File.Exists(previousPath);
                    if (hasPrevious && File.Exists(AssetPath(source)))
                        throw new IOException("기존 위치와 새 위치에 에셋이 모두 있습니다. 중복을 확인하세요: " + source.Name);
                    string existingPath = hasPrevious ? previousPath : AssetPath(source);
                    var existingObject = AssetDatabase.LoadMainAssetAtPath(existingPath);
                    if ((existingObject != null && existingObject.GetType() != tableType) ||
                        (existingObject == null && File.Exists(existingPath)))
                        throw new IOException("다른 타입 또는 읽을 수 없는 기존 에셋을 덮어쓰지 않습니다: " + AssetPath(source));
                    var item = new PendingAsset { Source = source, Existing = existingObject as ScriptableObject, PreviousPath = hasPrevious ? previousPath : null };
                    pending.Add(item);
                    item.Staged = ScriptableObject.CreateInstance(tableType);
                    tableType.GetField("Items").SetValue(item.Staged, rows);
                    object payload = Activator.CreateInstance(payloadType);
                    payloadType.GetField("Items").SetValue(payload, rows);
                    item.Json = JsonUtility.ToJson(payload, true);
                }
                Directory.CreateDirectory(DataDirectory);
                Directory.CreateDirectory(AssetDirectory);
                AssetDatabase.Refresh();
                foreach (PendingAsset item in pending)
                {
                    if (item.PreviousPath != null)
                    {
                        string error = AssetDatabase.MoveAsset(item.PreviousPath, AssetPath(item.Source));
                        if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    }
                    if (item.Existing != null)
                    {
                        Undo.RecordObject(item.Existing, "Import sheet data");
                        // 기존 객체와 .meta GUID를 유지하고 데이터 필드만 갱신합니다.
                        FieldInfo items = item.Existing.GetType().GetField("Items");
                        items.SetValue(item.Existing, items.GetValue(item.Staged));
                        EditorUtility.SetDirty(item.Existing);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(item.Staged, AssetPath(item.Source));
                        item.Staged = null; // 소유권이 AssetDatabase로 넘어갔습니다.
                    }
                    WriteChanged(DataDirectory + "/" + item.Source.Name + ".json", item.Json);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                message = pending.Count + "개 ScriptableObject / JSON 생성·갱신 완료.";
            }
            catch (Exception e) { message = e.Message; }
            finally
            {
                foreach (PendingAsset item in pending)
                    if (item.Staged != null) DestroyImmediate(item.Staged);
            }
        }

        private static void WriteChanged(string path, string text)
        {
            if (!File.Exists(path) || File.ReadAllText(path, Encoding.UTF8) != text)
                File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
