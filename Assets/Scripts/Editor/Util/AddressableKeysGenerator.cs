#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ThreeSouls.Editor
{
    public static class AddressableKeysGenerator
    {
        private sealed class GeneratedEntry
        {
            public string Identifier { get; set; }
            public string Address { get; set; }
        }

        private sealed class GeneratedGroup
        {
            public string EnumName { get; set; }
            public List<GeneratedEntry> Entries { get; } = new();
        }

        private static readonly HashSet<string> CSharpKeywords =
            new(StringComparer.Ordinal)
            {
                "abstract", "as", "base", "bool", "break", "byte",
                "case", "catch", "char", "checked", "class", "const",
                "continue", "decimal", "default", "delegate", "do",
                "double", "else", "enum", "event", "explicit",
                "extern", "false", "finally", "fixed", "float",
                "for", "foreach", "goto", "if", "implicit", "in",
                "int", "interface", "internal", "is", "lock", "long",
                "namespace", "new", "null", "object", "operator",
                "out", "override", "params", "private", "protected",
                "public", "readonly", "ref", "return", "sbyte",
                "sealed", "short", "sizeof", "stackalloc", "static",
                "string", "struct", "switch", "this", "throw",
                "true", "try", "typeof", "uint", "ulong",
                "unchecked", "unsafe", "ushort", "using", "virtual",
                "void", "volatile", "while"
            };

        /// <summary>
        /// 현재 Addressables 그룹과 키를 기준으로 enum 및 실제 주소 변환 코드를 생성한다.
        /// 폴더 Addressable의 암시적 하위 에셋도 포함하며 Addressable 설정은 변경하지 않는다.
        /// </summary>
        public static void Generate(bool addExtension = false)
        {
            List<GeneratedGroup> generatedGroups = CollectGroups(addExtension);
            string source = BuildSource(generatedGroups);

            WriteScript(source);

            int keyCount = generatedGroups.Sum(group => group.Entries.Count);
            Debug.Log(
                $"[AddressableKeysGenerator] enum {generatedGroups.Count}개, " +
                $"키 {keyCount}개를 생성했습니다: {AddressableDefinition.KeysEnumScriptPath}");
        }

        private static List<GeneratedGroup> CollectGroups(bool addExtension)
        {
            var result = new List<GeneratedGroup>();
            var usedEnumNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "AddressableKeyExtensions"
            };

            IEnumerable<AddressableAssetGroup> groups = AddressableGroupService
                .GetAllGroups()
                .Where(group => group != null && group.HasSchema<PlayerDataGroupSchema>() == false)
                .OrderBy(group => group.Name, StringComparer.Ordinal);

            foreach (AddressableAssetGroup group in groups)
            {
                var generatedGroup = new GeneratedGroup
                {
                    EnumName = MakeUniqueIdentifier(group.Name, usedEnumNames, group.Guid)
                };

                var usedEntryNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (AddressableAssetEntry entry in CollectEntries(group))
                {
                    string entryName = GetEntryName(entry, addExtension);
                    string identifier = MakeUniqueIdentifier(entryName, usedEntryNames, entry.guid);

                    generatedGroup.Entries.Add(new GeneratedEntry
                    {
                        Identifier = identifier,
                        Address = entry.address
                    });
                }

                // 키가 없는 빈 그룹은 사용할 enum도 없으므로 생성하지 않는다.
                if (generatedGroup.Entries.Count > 0)
                {
                    result.Add(generatedGroup);
                }
            }

            return result;
        }

        private static IEnumerable<AddressableAssetEntry> CollectEntries(AddressableAssetGroup group)
        {
            // GetEntries: 일반 명시적 엔트리
            // GetAllAssets: Addressable 폴더가 암시적으로 포함하는 하위 에셋
            IEnumerable<AddressableAssetEntry> explicitEntries = AddressableGroupService
                .GetEntries(group)
                .Where(entry => entry != null && AssetDatabase.IsValidFolder(entry.AssetPath) == false);

            IEnumerable<AddressableAssetEntry> implicitFolderEntries = AddressableGroupService
                .GetAllAssets(group)
                .Where(entry => entry != null && AssetDatabase.IsValidFolder(entry.AssetPath) == false);

            return explicitEntries
                .Concat(implicitFolderEntries)
                .Where(entry => entry.MainAsset != null && string.IsNullOrWhiteSpace(entry.address) == false)
                .GroupBy(GetEntryIdentity, StringComparer.Ordinal)
                .Select(entries => entries.First())
                .OrderBy(entry => entry.address, StringComparer.Ordinal);
        }

        private static string GetEntryIdentity(AddressableAssetEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.guid) == false)
            {
                return entry.guid;
            }

            return $"{entry.AssetPath}|{entry.address}";
        }

        private static string GetEntryName(AddressableAssetEntry entry, bool addExtension)
        {
            string entryName = AddressableAssetMover.GetAssetName(entry.MainAsset);

            if (string.IsNullOrWhiteSpace(entryName))
            {
                entryName = Path.GetFileNameWithoutExtension(entry.AssetPath);
            }

            if (addExtension)
            {
                string extension = Path.GetExtension(entry.AssetPath).TrimStart('.');

                if (string.IsNullOrEmpty(extension) == false)
                {
                    entryName = $"{entryName}_{extension}";
                }
            }

            return entryName;
        }

        private static string BuildSource(IReadOnlyList<GeneratedGroup> groups)
        {
            var source = new StringBuilder();

            source.AppendLine("// <auto-generated />");
            source.AppendLine("using System;");
            source.AppendLine();
            source.Append("namespace ").AppendLine(MakeNamespace(AddressableDefinition.KeysEnumNameSpaceName));
            source.AppendLine("{");

            foreach (GeneratedGroup group in groups)
            {
                source.Append("    public enum ").AppendLine(group.EnumName);
                source.AppendLine("    {");

                foreach (GeneratedEntry entry in group.Entries)
                {
                    source.Append("        ").Append(entry.Identifier).AppendLine(",");
                }

                source.AppendLine("    }");
                source.AppendLine();
            }

            AppendAddressResolver(source, groups);
            source.AppendLine("}");

            return source.ToString();
        }

        private static void AppendAddressResolver(StringBuilder source, IReadOnlyList<GeneratedGroup> groups)
        {
            source.AppendLine("    public static class AddressableKeyExtensions");
            source.AppendLine("    {");

            foreach (GeneratedGroup group in groups)
            {
                source.Append("        public static string ToAddress(this ")
                    .Append(group.EnumName)
                    .AppendLine(" value)");
                source.AppendLine("        {");
                source.AppendLine("            return value switch");
                source.AppendLine("            {");

                foreach (GeneratedEntry entry in group.Entries)
                {
                    source.Append("                ")
                        .Append(group.EnumName)
                        .Append('.')
                        .Append(entry.Identifier)
                        .Append(" => ")
                        .Append(ToCSharpStringLiteral(entry.Address))
                        .AppendLine(",");
                }

                source.AppendLine("                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)");
                source.AppendLine("            };");
                source.AppendLine("        }");
                source.AppendLine();
            }

            source.AppendLine("        public static string GetAddress<TEnum>(TEnum value)");
            source.AppendLine("            where TEnum : struct, Enum");
            source.AppendLine("        {");
            source.AppendLine("            object boxed = value;");

            foreach (GeneratedGroup group in groups)
            {
                source.Append("            if (boxed is ")
                    .Append(group.EnumName)
                    .Append(' ')
                    .Append(group.EnumName)
                    .AppendLine("Value)");
                source.AppendLine("            {");
                source.Append("                return ")
                    .Append(group.EnumName)
                    .AppendLine("Value.ToAddress();");
                source.AppendLine("            }");
                source.AppendLine();
            }

            source.AppendLine("            throw new ArgumentException($\"지원하지 않는 Addressable enum 타입입니다: {typeof(TEnum).FullName}\", nameof(value));");
            source.AppendLine("        }");
            source.AppendLine("    }");
        }

        private static void WriteScript(string source)
        {
            string scriptPath = AddressableDefinition.KeysEnumScriptPath;
            string directory = Path.GetDirectoryName(scriptPath);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(scriptPath, source, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceUpdate);
        }

        private static string MakeUniqueIdentifier(string value, ISet<string> usedNames, string uniqueKey)
        {
            string baseIdentifier = MakeIdentifier(value);

            if (usedNames.Add(baseIdentifier))
            {
                return baseIdentifier;
            }

            string suffix = MakeSuffix(uniqueKey);
            string candidate = $"{baseIdentifier}_{suffix}";
            int sequence = 2;

            while (usedNames.Add(candidate) == false)
            {
                candidate = $"{baseIdentifier}_{suffix}_{sequence}";
                sequence++;
            }

            return candidate;
        }

        private static string MakeSuffix(string uniqueKey)
        {
            if (string.IsNullOrWhiteSpace(uniqueKey))
            {
                return "Duplicate";
            }

            int suffixLength = Math.Min(8, uniqueKey.Length);
            return MakeIdentifier(uniqueKey.Substring(0, suffixLength));
        }

        private static string MakeNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "AddressableKeys";
            }

            return string.Join(
                ".",
                value.Split('.')
                    .Where(part => string.IsNullOrWhiteSpace(part) == false)
                    .Select(MakeIdentifier));
        }

        private static string MakeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed";
            }

            value = value.Trim();
            var builder = new StringBuilder(value.Length + 1);

            if (char.IsLetter(value[0]) == false && value[0] != '_')
            {
                builder.Append('_');
            }

            foreach (char character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            }

            string identifier = builder.ToString();

            if (CSharpKeywords.Contains(identifier))
            {
                identifier = $"_{identifier}";
            }

            return identifier;
        }

        private static string ToCSharpStringLiteral(string value)
        {
            if (value == null)
            {
                return "null";
            }

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('\"');

            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '\"': builder.Append("\\\""); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default: builder.Append(character); break;
                }
            }

            builder.Append('\"');
            return builder.ToString();
        }
    }
}
#endif