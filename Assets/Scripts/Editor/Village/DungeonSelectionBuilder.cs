using System;
using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Dungeon;
using ThreeSouls.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 기존 마을 탭에 지도와 룬 준비 프리팹을 연결하며 다른 씬은 수정하지 않습니다.
    /// </summary>
    public static class DungeonSelectionBuilder
    {
        private const string MapPrefab = "Assets/Prefabs/UI/Lobby/UI_DungeonSelection.prefab";
        private const string PreparationPrefab = "Assets/Prefabs/UI/Lobby/UI_DungeonPreparation.prefab";
        private static readonly Color Ink = new Color32(19, 28, 35, 255);
        private static readonly Color Card = new Color32(32, 44, 53, 255);
        private static readonly Color Gold = new Color32(226, 183, 105, 255);
        private static readonly Color White = new Color32(237, 236, 218, 255);
        private static TMP_FontAsset Font;

        [MenuItem("Three Souls/Village/Connect Dungeon Selection")]
        public static void Connect()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || scene.path != VillageLobbyUIBuilder.ScenePath)
            {
                throw new InvalidOperationException("테스트 Village를 Edit 모드에서 여세요.");
            }

            DungeonSelectionRules.Validate();
            var root = PrefabUtility.LoadPrefabContents(VillageLobbyUIBuilder.PrefabPath);
            try
            {
                Configure(root.GetComponentInChildren<UI_LobbyTabs>(true));
                UiPolishBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, VillageLobbyUIBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var tabs = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<UI_LobbyTabs>(true)).Single();
            Configure(tabs);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        public static void Configure(UI_LobbyTabs tabs)
        {
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/RuneUI.asset")
                ?? throw new InvalidOperationException("기존 룬 UI 폰트가 필요합니다.");
            var village = tabs.transform.Find("Village")
                ?? throw new InvalidOperationException("기존 마을 탭이 필요합니다.");
            var panel = village.GetComponentInChildren<UI_DungeonSelection>(true);
            if (panel == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefab) ?? BuildMapPrefab();
                panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, village)).GetComponent<UI_DungeonSelection>();
            }

            panel.gameObject.SetActive(false);
            var navigation = tabs.transform.Find("Navigation");
            var entry = navigation.Find("OpenDungeonMap") ?? village.Find("OpenDungeonMap");
            if (entry == null)
            {
                entry = Button("OpenDungeonMap", navigation, .81f, .08f, .99f, .94f, "던전 지도").transform;
            }
            entry.SetParent(navigation, false);
            var mapButton = entry.GetComponent<UnityEngine.UI.Button>();
            mapButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(mapButton.onClick, tabs.OpenDungeonMap);
            var template = navigation.Find("Tab0");
            var image = mapButton.GetComponent<UnityEngine.UI.Image>();
            var templateImage = template.GetComponent<UnityEngine.UI.Image>();
            image.sprite = templateImage.sprite;
            image.type = templateImage.type;
            image.color = templateImage.color;
            mapButton.colors = template.GetComponent<UnityEngine.UI.Button>().colors;
            foreach (var childName in new[] { "Hint", "Indicator" })
            {
                if (entry.Find(childName) == null) UnityEngine.Object.Instantiate(template.Find(childName).gameObject, entry, false).name = childName;
            }
            entry.Find("Hint").GetComponent<UnityEngine.UI.Text>().text = "DUNGEON";
            entry.Find("Indicator").GetComponent<UnityEngine.UI.Image>().color = new Color32(53, 66, 76, 255);
            for (var i = 0; i < 5; i++)
            {
                var item = i < 4 ? navigation.Find("Tab" + i) : entry;
                var rect = (RectTransform)item;
                rect.anchorMin = new Vector2(i / 5f + .01f, .08f);
                rect.anchorMax = new Vector2((i + 1) / 5f - .01f, .94f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                foreach (var text in item.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    text.fontSize = text.name == "Hint" ? 14 : 24;
                    text.rectTransform.anchorMin = new Vector2(.23f, text.name == "Hint" ? .08f : .40f);
                    text.rectTransform.anchorMax = new Vector2(.97f, text.name == "Hint" ? .36f : .95f);
                    text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                }
                foreach (var text in item.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.fontSize = 24;
                    text.rectTransform.anchorMin = new Vector2(.23f, .40f);
                    text.rectTransform.anchorMax = new Vector2(.97f, .95f);
                    text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                }
            }
        }

        private static GameObject BuildPreparationPrefab()
        {
            var rect = Box("UI_DungeonPreparation", null, 0, 0, 1, 1, Ink, true);
            var view = rect.gameObject.AddComponent<UI_DungeonPreparation>();
            Bind(view, "Title", Label("Stage", rect, .04f, .89f, .73f, .98f, "출전 준비", 32, Gold));
            Bind(view, "CancelButton", Button("Cancel", rect, .78f, .93f, .96f, .982f, "취소"));
            var runePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Lobby/UI_RuneStones.prefab")
                ?? throw new InvalidOperationException("기존 룬 UI 프리팹이 필요합니다.");
            var runes = ((GameObject)PrefabUtility.InstantiatePrefab(runePrefab, rect)).GetComponent<UI_RuneStones>();
            var runeRect = (RectTransform)runes.transform;
            runeRect.anchorMin = new Vector2(.025f, .145f);
            runeRect.anchorMax = new Vector2(.975f, .878f);
            runeRect.offsetMin = Vector2.zero;
            runeRect.offsetMax = Vector2.zero;
            PrefabUtility.RecordPrefabInstancePropertyModifications(runeRect);
            Bind(view, "RuneView", runes);
            var detailCard = (RectTransform)runes.transform.Find("DetailCard");
            detailCard.anchorMin = new Vector2(.04f, .147f);
            detailCard.anchorMax = new Vector2(.96f, .341f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(detailCard);
            var detail = detailCard.GetComponentInChildren<TMP_Text>();
            detail.enableAutoSizing = false;
            detail.fontSize = 26;
            PrefabUtility.RecordPrefabInstancePropertyModifications(detail);
            var loadout = runes.transform.Find("Loadout").GetComponent<TMP_Text>();
            loadout.enableAutoSizing = false;
            loadout.fontSize = 26;
            PrefabUtility.RecordPrefabInstancePropertyModifications(loadout);
            var runeSerialized = new SerializedObject(runes);
            var runeClose = (UnityEngine.UI.Button)runeSerialized.FindProperty("CloseButton").objectReferenceValue;
            UnityEventTools.AddPersistentListener(runeClose.onClick, view.Close);
            runeClose.GetComponentInChildren<TMP_Text>().text = "스테이지로";
            PrefabUtility.RecordPrefabInstancePropertyModifications(runeClose);
            PrefabUtility.RecordPrefabInstancePropertyModifications(runeClose.GetComponentInChildren<TMP_Text>());
            var ready = Box("ReadyCard", rect, .04f, .15f, .96f, .875f, Card, true);
            Bind(view, "ReadySummary", Label("Summary", ready, .045f, .035f, .955f, .965f, "", 27, White));
            Bind(view, "ReadyCard", ready.gameObject);
            ready.gameObject.SetActive(false);
            Bind(view, "Message", Label("Message", rect, .04f, .09f, .96f, .135f, "", 23, White));
            var start = Button("Start", rect, .48f, .017f, .96f, .075f, "전투 시작");
            start.targetGraphic.color = new Color32(85, 74, 40, 255);
            Bind(view, "StartButton", start);
            Bind(view, "StartLabel", start.GetComponentInChildren<TMP_Text>());
            Bind(view, "ResumeButton", Button("Resume", rect, .04f, .017f, .43f, .075f, "준비 수정"));
            rect.gameObject.SetActive(false);
            UiPolishBuilder.Apply(rect.gameObject);
            var prefab = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, PreparationPrefab);
            UnityEngine.Object.DestroyImmediate(rect.gameObject);
            return prefab;
        }

        private static GameObject BuildMapPrefab()
        {
            var rect = Box("UI_DungeonSelection", null, 0, 0, 1, 1, Ink, true);
            var view = rect.gameObject.AddComponent<UI_DungeonSelection>();
            Bind(view, "Title", Label("Title", rect, .04f, .935f, .61f, .985f, "던전 지도", 38, Gold));
            Bind(view, "CloseButton", Button("Close", rect, .80f, .939f, .96f, .985f, "마을로"));
            Bind(view, "BackButton", Button("Back", rect, .63f, .939f, .79f, .985f, "전체 지도"));
            Bind(view, "Description", Label("Description", rect, .04f, .887f, .96f, .929f, "", 24, White));
            var holder = Box("MapFrame", rect, .024f, .403f, .976f, .878f, Card);
            var mapRect = Box("Map", holder, 0, 0, 1, 1, Color.white);
            UnityEngine.Object.DestroyImmediate(mapRect.GetComponent<UnityEngine.UI.Image>());
            var map = mapRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            map.raycastTarget = false;
            mapRect.gameObject.AddComponent<UI_DungeonMapLayout>();
            Bind(view, "Map", map);
            Bind(view, "MapTitle", Label("MapTitle", mapRect, .39f, .899f, .61f, .974f, "던전 선택", 22, Ink, TextAlignmentOptions.Center));
            var note = Label("WorldNote", mapRect, .646f, .088f, .953f, .307f,
                "광산 · 유적 · 숲 · 설원\n\n룬으로 출전을 준비하세요", 20, Ink);
            Bind(view, "WorldNote", note.gameObject);
            var maps = LobbyTables.Get<DungeonMapTableDataTable>().Items;
            var textures = new UnityEngine.Object[maps.Length + 1];
            textures[0] = LoadMap("Assets/Images/Map/Previews/NativeWorldMap.png");
            var points = new UnityEngine.Object[maps.Length];
            for (var i = 0; i < maps.Length; i++)
            {
                textures[i + 1] = LoadMap(maps[i].TexturePath);
                var point = Point("Dungeon" + i, mapRect, maps[i].WorldX, maps[i].WorldY, maps[i].DisplayName, 166);
                points[i] = point;
            }

            map.sprite = (Sprite)textures[0];
            BindArray(view, "MapTextures", textures);
            BindArray(view, "DungeonPoints", points);
            var stages = new UnityEngine.Object[5];
            var rows = new UnityEngine.Object[5];
            var labels = new UnityEngine.Object[5];
            for (var i = 0; i < 5; i++)
            {
                stages[i] = Point("Stage" + i, mapRect, .2f + i * .13f, .4f, "", 126);
                ((UnityEngine.UI.Button)stages[i]).gameObject.SetActive(false);
                var y = .268f - i * .056f;
                var row = Button("List" + i, rect, .04f, y, .96f, y + .045f, "");
                labels[i] = row.GetComponentInChildren<TMP_Text>();
                ((TMP_Text)labels[i]).alignment = TextAlignmentOptions.Left;
                ((TMP_Text)labels[i]).rectTransform.anchorMin = new Vector2(.04f, .04f);
                rows[i] = row;
            }

            BindArray(view, "StagePoints", stages);
            BindArray(view, "ListButtons", rows);
            BindArray(view, "ListLabels", labels);
            Bind(view, "Hint", Label("Hint", rect, .04f, .329f, .96f, .386f, "", 25, Gold));
            var preparationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreparationPrefab) ?? BuildPreparationPrefab();
            var preparation = (GameObject)PrefabUtility.InstantiatePrefab(preparationPrefab, rect);
            Bind(view, "PreparationView", preparation.GetComponent<UI_DungeonPreparation>());
            rect.gameObject.SetActive(false);
            UiPolishBuilder.Apply(rect.gameObject);
            var prefab = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, MapPrefab);
            UnityEngine.Object.DestroyImmediate(rect.gameObject);
            return prefab;
        }

        private static Sprite LoadMap(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path)
                ?? throw new InvalidOperationException("던전 이미지가 없습니다: " + path);
        }

        private static UnityEngine.UI.Button Point(string name, Transform parent, float x, float y, string text, int width)
        {
            var button = Button(name, parent, x, y, x, y, text);
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(width, 62);
            rect.anchoredPosition = Vector2.zero;
            button.targetGraphic.color = new Color32(55, 41, 26, 245);
            button.GetComponentInChildren<TMP_Text>().fontSize = 22;
            return button;
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
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private static TMP_Text Label(string name, Transform parent, float x0, float y0, float x1, float y1, string text, int size,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Color.clear);
            UnityEngine.Object.DestroyImmediate(rect.GetComponent<UnityEngine.UI.Image>());
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, float x0, float y0, float x1, float y1, string text)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Card, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
            Label("Label", rect, .035f, .04f, .965f, .96f, text, 25, White, TextAlignmentOptions.Center);
            return button;
        }

        private static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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


