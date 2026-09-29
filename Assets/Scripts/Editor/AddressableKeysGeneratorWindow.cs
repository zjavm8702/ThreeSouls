#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ThreeSouls.Editor
{
    public sealed class AddressableKeysGeneratorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Three Souls/Addressables/Generate Keys Enum";

        [SerializeField]
        private bool IsAddExtension;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<AddressableKeysGeneratorWindow>();

            window.titleContent = new GUIContent("Addressable Keys");
            window.minSize = new Vector2(420f, 150f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Addressable Keys Enum Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Output", AddressableDefinition.KeysEnumScriptPath);
                EditorGUILayout.TextField("Namespace", AddressableDefinition.KeysEnumNameSpaceName);
            }

            IsAddExtension = EditorGUILayout.ToggleLeft("enum 이름에 확장자 포함 (예: BaseCharacter_prefab)", IsAddExtension);

            EditorGUILayout.Space(10f);

            if (GUILayout.Button("Generate Addressable Keys Enum", GUILayout.Height(32f)))
            {
                Generate();
            }
        }

        private void Generate()
        {
            try
            {
                AddressableKeysGenerator.Generate(IsAddExtension);
                EditorUtility.DisplayDialog("Addressable Keys", $"생성이 완료되었습니다.\n\n{AddressableDefinition.KeysEnumScriptPath}", "확인");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Addressable Keys 생성 실패", "생성 중 오류가 발생했습니다. Console을 확인해 주세요.", "확인");
            }
        }
    }
}
#endif
