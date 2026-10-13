using System;
using System.Linq;
using ThreeSouls.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 기존 캐릭터 탭에 별도 성장 프리팹과 영웅별 진입 버튼을 연결합니다.
    /// </summary>
    public static class HeroGrowthBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/Lobby/UI_HeroGrowth.prefab";
        private static readonly Color Ink = new Color32(19, 28, 35, 255);
        private static readonly Color Card = new Color32(32, 44, 53, 255);
        private static readonly Color Gold = new Color32(226, 183, 105, 255);
        private static readonly Color White = new Color32(237, 236, 218, 255);
        private static readonly Color Muted = new Color32(151, 169, 176, 255);
        private static Font Font;

        [MenuItem("Three Souls/Village/Connect Hero Stat Growth")]
        public static void Connect()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || scene.path != VillageLobbyUIBuilder.ScenePath)
            {
                throw new InvalidOperationException("테스트 Village를 Edit 모드에서 열어 연결하세요.");
            }

            var root = PrefabUtility.LoadPrefabContents(VillageLobbyUIBuilder.PrefabPath);
            try
            {
                ConfigureCharacters(root.GetComponentInChildren<UI_LobbyCharacters>(true));
                UiPolishBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, VillageLobbyUIBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var view = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<UI_LobbyCharacters>(true)).Single();
            ConfigureCharacters(view);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        internal static void ConfigureCharacters(UI_LobbyCharacters view)
        {
            if (view == null)
            {
                throw new InvalidOperationException("기존 캐릭터 탭이 필요합니다.");
            }

            Font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Mulmaru.ttf")
                ?? throw new InvalidOperationException("기존 UI 폰트가 필요합니다.");
            var panel = view.GetComponentInChildren<UI_HeroGrowth>(true);
            if (panel == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
                panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, view.transform)).GetComponent<UI_HeroGrowth>();
            }

            panel.gameObject.SetActive(false);
            Bind(view, "GrowthPanel", panel);
            var names = new UnityEngine.Object[3];
            for (var i = 0; i < 3; i++)
            {
                var hero = view.transform.Find("Hero" + i);
                var name = hero.Find("Name").GetComponent<UnityEngine.UI.Text>();
                name.rectTransform.anchorMax = new Vector2(0.69f, 0.97f);
                name.fontSize = 29;
                Record(name);
                Record(name.rectTransform);
                names[i] = name;
                var growthButton = hero.Find("GrowStats");
                if (growthButton == null)
                {
                    var button = Button("GrowStats", hero, 0.72f, 0.75f, 0.975f, 0.95f, "능력치 성장");
                    UnityEventTools.AddIntPersistentListener(button.onClick, view.OpenGrowth, i);
                }

                var stats = hero.Find("Stats").GetComponent<UnityEngine.UI.Text>();
                stats.fontSize = 22;
                Record(stats);
            }

            BindArray(view, "HeroNames", names);
            var subtitle = view.transform.Find("Subtitle").GetComponent<UnityEngine.UI.Text>();
            subtitle.text = "레벨로 능력치를 해금하고 골드·성장석으로 강화하세요.";
            Record(subtitle);
        }

        private static GameObject BuildPrefab()
        {
            var root = Box("UI_HeroGrowth", null, 0, 0, 1, 1, Ink, true);
            var view = root.gameObject.AddComponent<UI_HeroGrowth>();
            Bind(view, "Heading", Label("Heading", root, 0.04f, 0.94f, 0.77f, 0.985f, "능력치 성장", 40, Gold));
            Bind(view, "CloseButton", Button("Close", root, 0.79f, 0.945f, 0.96f, 0.985f, "돌아가기"));
            Bind(view, "Progress", Label("Progress", root, 0.04f, 0.901f, 0.96f, 0.939f, "", 25, White));
            var track = Box("ExperienceTrack", root, 0.04f, 0.889f, 0.96f, 0.899f, Card);
            var fill = Box("Experience", track, 0, 0, 1, 1, Gold).GetComponent<UnityEngine.UI.Image>();
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            Bind(view, "ExperienceFill", fill);
            var buttons = new UnityEngine.Object[3];
            var indicators = new UnityEngine.Object[3];
            for (var i = 0; i < 3; i++)
            {
                var x = 0.04f + i * 0.313f;
                var button = Button("Hero" + i, root, x, 0.803f, x + 0.293f, 0.875f, LobbyPresentationData.GetHeroName(i));
                var text = button.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();
                text.rectTransform.anchorMin = new Vector2(0.38f, 0.04f);
                var portrait = Box("Portrait", button.transform, 0.025f, 0.06f, 0.37f, 0.94f, Color.white).GetComponent<UnityEngine.UI.Image>();
                portrait.sprite = LobbyPresentationData.GetHeroPortrait(i);
                portrait.preserveAspect = true;
                buttons[i] = button;
                indicators[i] = Box("Indicator", button.transform, 0.02f, 0, 0.98f, 0.035f, Muted).GetComponent<UnityEngine.UI.Image>();
            }

            BindArray(view, "HeroButtons", buttons);
            BindArray(view, "HeroIndicators", indicators);
            Bind(view, "Wallet", Label("Wallet", root, 0.04f, 0.753f, 0.96f, 0.798f, "", 27, White));
            var stats = new UnityEngine.Object[6];
            var values = new UnityEngine.Object[6];
            var ranks = new UnityEngine.Object[6];
            var selected = new UnityEngine.Object[6];
            for (var i = 0; i < 6; i++)
            {
                var y = 0.675f - i * 0.073f;
                var row = Button("Stat" + i, root, 0.04f, y, 0.96f, y + 0.068f, "");
                var label = row.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();
                label.rectTransform.anchorMax = new Vector2(0.65f, 0.95f);
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 28;
                stats[i] = row;
                values[i] = label;
                ranks[i] = Label("Rank", row.transform, 0.67f, 0.04f, 0.97f, 0.96f, "", 22, Gold);
                selected[i] = Box("Selected", row.transform, 0, 0, 0.009f, 1, Muted).GetComponent<UnityEngine.UI.Image>();
            }

            BindArray(view, "StatButtons", stats);
            BindArray(view, "StatValues", values);
            BindArray(view, "RankLabels", ranks);
            BindArray(view, "StatIndicators", selected);
            var detail = Box("SelectedDetail", root, 0.04f, 0.141f, 0.96f, 0.287f, Card);
            Bind(view, "Detail", Label("Detail", detail, 0.03f, 0.06f, 0.97f, 0.96f, "", 24, White));
            Bind(view, "Cost", Label("Cost", root, 0.04f, 0.065f, 0.69f, 0.136f, "", 22, Gold));
            Bind(view, "UpgradeButton", Button("Upgrade", root, 0.71f, 0.071f, 0.96f, 0.127f, "강화 +1"));
            Bind(view, "Message", Label("Message", root, 0.04f, 0.006f, 0.96f, 0.06f, "", 22, Muted));
            var confirmation = Box("Confirmation", root, 0, 0, 1, 1, new Color(0, 0, 0, 0.85f), true);
            var card = Box("Panel", confirmation, 0.07f, 0.30f, 0.93f, 0.70f, Ink);
            Bind(view, "ConfirmationText", Label("Text", card, 0.06f, 0.28f, 0.94f, 0.96f, "", 30, White));
            Bind(view, "ConfirmButton", Button("Confirm", card, 0.06f, 0.075f, 0.47f, 0.225f, "강화 확정"));
            Bind(view, "CancelButton", Button("Cancel", card, 0.53f, 0.075f, 0.94f, 0.225f, "취소"));
            Bind(view, "Confirmation", confirmation.gameObject);
            confirmation.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            UiPolishBuilder.Apply(root.gameObject);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static RectTransform Box(string name, Transform parent, float x0, float y0, float x1, float y1,
            Color color, bool raycast = false)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private static UnityEngine.UI.Text Label(string name, Transform parent, float x0, float y0, float x1, float y1,
            string text, int size, Color color)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Color.clear);
            UnityEngine.Object.DestroyImmediate(rect.GetComponent<UnityEngine.UI.Image>());
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, float x0, float y0, float x1, float y1, string title)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Card, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
            var label = Label("Label", rect, 0.035f, 0.02f, 0.965f, 0.98f, title, 24, White);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Record(target);
        }

        private static void BindArray(UnityEngine.Object target, string name, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(name);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Record(target);
        }

        private static void Record(UnityEngine.Object target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target) == true)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }
    }
}
