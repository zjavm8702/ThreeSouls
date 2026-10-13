using System;
using System.Linq;
using ThreeSouls.UI;
using ThreeSouls.Village;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 정식 마을 전용 룬 상점과 이야기 프리팹 및 NPC 참조를 생성합니다.
    /// </summary>
    public static class StoryVillageBuilder
    {
        private static readonly Color Ink = new Color32(18, 27, 37, 255);
        private static readonly Color Card = new Color32(35, 49, 59, 255);
        private static readonly Color Gold = new Color32(235, 191, 111, 255);
        private static readonly Color White = new Color32(241, 237, 218, 255);
        private static TMP_FontAsset Font;
        private const string Folder = "Assets/Village/Production";

        [MenuItem("Tools/Three Souls/Village/Connect Story and Rune Shop")]
        public static void Connect()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || scene.path != "Assets/Scenes/Scene_Village.unity")
            {
                throw new InvalidOperationException("정식 Scene_Village를 Edit 모드로 열어 주세요.");
            }

            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/StoryUI.asset");
            if (Font == null)
            {
                Font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/Fonts/Mulmaru.ttf"),
                    42, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                Font.name = "StoryUI";
                AssetDatabase.CreateAsset(Font, Folder + "/StoryUI.asset");
                AssetDatabase.AddObjectToAsset(Font.material, Font);
                var textures = Font.atlasTextures;
                for (var i = 0; i < textures.Length; i++)
                {
                    AssetDatabase.AddObjectToAsset(textures[i], Font);
                }
            }

            var world = scene.GetRootGameObjects().Single(x => x.name == "Production Village World");
            var presentation = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<VillageWorldPresentation>(true)).Single();
            var party = world.GetComponentInChildren<VillagePartyController>(true);
            var interaction = world.GetComponent<VillageNpcInteraction>();
            if (interaction == null)
            {
                interaction = world.AddComponent<VillageNpcInteraction>();
            }

            Bind(interaction, "Party", party);
            var names = new[] { "RuneShop", "Quest_1", "Quest_2", "Quest_3", "Quest_4" };
            var targets = new UnityEngine.Object[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var actor = world.GetComponentsInChildren<Transform>(true).Single(x => x.name == "NPC_" + names[i]);
                var target = actor.GetComponent<VillageNpcTarget>();
                if (target == null)
                {
                    target = actor.gameObject.AddComponent<VillageNpcTarget>();
                }

                var hit = actor.GetComponent<BoxCollider2D>();
                if (hit == null)
                {
                    hit = actor.gameObject.AddComponent<BoxCollider2D>();
                }

                hit.isTrigger = true;
                hit.size = new Vector2(0.85f, 0.95f);
                hit.offset = new Vector2(0, 0.35f);
                var role = actor.Find("Role Label");
                var label = role == null ? null : role.GetComponent<TMP_Text>();
                if (label != null) label.font = Font;
                var so = new SerializedObject(target);
                so.FindProperty("NpcKey").stringValue = names[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Bind(target, "HitArea", hit);
                Bind(target, "RoleLabel", label);
                targets[i] = target;
            }

            BindArray(interaction, "Targets", targets);
            QuestPresentationBuilder.ApplyWorld(scene);
            Bind(presentation, "NpcInteraction", interaction);
            Bind(presentation, "RuneShopPrefab", BuildShop().GetComponent<UI_RuneShop>());
            Bind(presentation, "StoryPrefab", BuildStory().GetComponent<UI_StoryQuest>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildShop()
        {
            var root = Box("UI_RuneShop", null, 0, 0, 1, 1, Ink, true);
            var view = root.gameObject.AddComponent<UI_RuneShop>();
            Label("Title", root, .04f, .939f, .75f, .983f, "루아의 룬 상점", 38, Gold);
            Label("Subtitle", root, .04f, .901f, .96f, .935f, "작은 문양에 새기는 세 영웅의 약속", 24, White);
            Bind(view, "CloseButton", Button("Close", root, .79f, .946f, .96f, .984f, "돌아가기"));
            Bind(view, "Wallet", Label("Wallet", root, .04f, .856f, .96f, .895f, "", 28, Gold));
            var buttons = new UnityEngine.Object[6];
            var labels = new UnityEngine.Object[6];
            for (var i = 0; i < buttons.Length; i++)
            {
                var x = .04f + i % 2 * .47f;
                var y = .728f - i / 2 * .103f;
                var button = Button("Product" + i, root, x, y, x + .45f, y + .094f, "상품");
                buttons[i] = button;
                labels[i] = button.GetComponentInChildren<TMP_Text>();
            }

            BindArray(view, "ProductButtons", buttons);
            BindArray(view, "ProductLabels", labels);
            Bind(view, "PreviousButton", Button("Previous", root, .04f, .470f, .28f, .511f, "이전"));
            Bind(view, "PageText", Label("Page", root, .43f, .470f, .60f, .508f, "1 / 3", 26, Gold));
            Bind(view, "NextButton", Button("Next", root, .72f, .470f, .96f, .511f, "다음"));
            Box("DetailCard", root, .04f, .254f, .96f, .451f, Card);
            Bind(view, "Detail", Label("Detail", root, .064f, .267f, .94f, .437f, "", 27, White));
            Bind(view, "BuyButton", Button("Buy", root, .04f, .191f, .49f, .241f, "선택 룬 구매"));
            Bind(view, "SellButton", Button("Sell", root, .51f, .191f, .96f, .241f, "선택 룬 판매"));
            Bind(view, "ManageButton", Button("Manage", root, .04f, .128f, .96f, .178f, "강화 · 장착 / 해제"));
            Bind(view, "Message", Label("Message", root, .04f, .031f, .96f, .111f, "", 24, White));
            var management = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Lobby/UI_RuneStones.prefab"), root);
            management.name = "RuneManagement";
            management.SetActive(false);
            Bind(view, "Management", management.GetComponent<UI_RuneStones>());
            var confirmation = Box("Confirmation", root, 0, 0, 1, 1, new Color(0, 0, 0, .8f), true);
            Box("Card", confirmation, .055f, .25f, .945f, .74f, Ink);
            Bind(view, "ConfirmText", Label("Text", confirmation, .09f, .39f, .91f, .70f, "거래 확인", 30, White));
            Bind(view, "ConfirmButton", Button("Confirm", confirmation, .09f, .285f, .49f, .35f, "확인"));
            Bind(view, "CancelButton", Button("Cancel", confirmation, .51f, .285f, .91f, .35f, "취소"));
            Bind(view, "Confirmation", confirmation.gameObject);
            confirmation.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            return Save(root, "UI_RuneShop");
        }

        private static GameObject BuildStory()
        {
            var root = Box("UI_StoryQuest", null, 0, 0, 1, 1, Color.clear);
            var view = root.gameObject.AddComponent<UI_StoryQuest>();
            var tracker = Button("StoryTracker", root, .025f, .935f, .55f, 1.070f, "");
            var trackerText = tracker.GetComponentInChildren<TMP_Text>();
            trackerText.fontSize = 22;
            trackerText.alignment = TextAlignmentOptions.TopLeft;
            Bind(view, "TrackerButton", tracker);
            Bind(view, "Tracker", trackerText);
            var panel = Box("DialoguePanel", root, 0, 0, 1, 1, new Color(0, 0, 0, .82f), true);
            Box("Card", panel, .035f, .13f, .965f, .88f, Ink);
            Bind(view, "Speaker", Label("Speaker", panel, .075f, .802f, .77f, .853f, "주민", 31, Gold));
            Bind(view, "CloseButton", Button("Close", panel, .80f, .812f, .927f, .857f, "닫기"));
            Bind(view, "Title", Label("Title", panel, .075f, .682f, .925f, .785f, "", 36, White));
            Bind(view, "Dialogue", Label("Dialogue", panel, .075f, .452f, .925f, .657f, "", 31, White));
            Bind(view, "Objective", Label("Objective", panel, .075f, .338f, .925f, .426f, "", 26, Gold));
            Bind(view, "Reward", Label("Reward", panel, .075f, .285f, .925f, .324f, "", 26, White));
            var action = Button("Action", panel, .075f, .210f, .925f, .267f, "의뢰 수락");
            Bind(view, "ActionButton", action);
            Bind(view, "ActionLabel", action.GetComponentInChildren<TMP_Text>());
            Bind(view, "MapButton", Button("Map", panel, .075f, .149f, .925f, .198f, "던전 지도 열기"));
            Bind(view, "DialoguePanel", panel.gameObject);
            panel.gameObject.SetActive(false);
            return Save(root, "UI_StoryQuest");
        }

        private static GameObject Save(RectTransform root, string name)
        {
            UiPolishBuilder.Apply(root.gameObject);
            var result = PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Prefabs/UI/Village/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return result;
        }

        private static RectTransform Box(string name, Transform parent, float x0, float y0, float x1, float y1, Color color, bool raycast = false)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private static TMP_Text Label(string name, Transform parent, float x0, float y0, float x1, float y1, string text, int size, Color color)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Color.clear);
            UnityEngine.Object.DestroyImmediate(rect.GetComponent<Image>());
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static Button Button(string name, Transform parent, float x0, float y0, float x1, float y1, string text)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Card, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var label = Label("Label", rect, .035f, .06f, .965f, .94f, text, 26, White);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
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
        }
    }
}
