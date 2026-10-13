#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeSouls.Editor
{
    /// <summary>
    /// 정식 시작·UI 마을·폐광 전투의 씬과 공통 UI 및 에셋 등록을 연결합니다.
    /// </summary>
    public static class GameCycleSetup
    {
        private const string LobbyPath = "Assets/Prefabs/UI/Lobby/UI_Lobby.prefab";
        private const string SuppliesPath = "Assets/Prefabs/UI/HUD/UI_HUD_Supplies.prefab";

        [MenuItem("Tools/Three Souls/Game Cycle/Connect Start Village Combat")]
        public static void Connect()
        {
            if (EditorApplication.isPlaying == true)
            {
                throw new InvalidOperationException("Edit 모드에서 게임 사이클을 연결하세요.");
            }
            var active = SceneManager.GetActiveScene();
            if (active.isDirty == true)
            {
                throw new InvalidOperationException("현재 씬의 사용자 변경을 저장한 뒤 연결하세요.");
            }
            var lobby = PrefabUtility.LoadPrefabContents(LobbyPath);
            ConfigureLobby(lobby.GetComponentInChildren<UI_LobbyTabs>(true));
            PrefabUtility.SaveAsPrefabAsset(lobby, LobbyPath);
            PrefabUtility.UnloadPrefabContents(lobby);
            BuildSupplyHud();
            ConnectScenes();
            RegisterResources();
            AssetDatabase.SaveAssets();
            Debug.Log("Start → Village → 폐광 1 전투 → Village 연결 완료. 정식 3개 씬과 UI 프리팹을 저장했습니다.");
        }

        public static void ConfigureLobby(UI_LobbyTabs tabs)
        {
            var page = tabs.transform.Find("Village");
            page.Find("Welcome/Eyebrow").GetComponent<UnityEngine.UI.Text>().text = "장비·룬·공급품을 준비하고 출전하세요";
            var existing = page.Find("OpenEquipmentShop");
            if (existing == null)
            {
                var source = tabs.transform.Find("Navigation/OpenDungeonMap") ?? page.Find("OpenDungeonMap");
                var button = UnityEngine.Object.Instantiate(source.gameObject, page, false);
                button.name = "OpenEquipmentShop";
                button.GetComponent<UnityEngine.UI.Button>().onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(0.15f, 0.29f);
                rect.anchorMax = new Vector2(0.85f, 0.36f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var labels = button.GetComponentsInChildren<TMPro.TMP_Text>(true);
                for (var i = 0; i < labels.Length; i++)
                {
                    labels[i].text = "장비 상점";
                }
            }
            if (page.Find("UIOnlyHome") == null)
            {
                var home = Rect("UIOnlyHome", page, new Vector2(0.07f, 0.37f), new Vector2(0.93f, 0.82f));
                var image = home.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = new Color32(23, 34, 43, 255);
                image.raycastTarget = false;
                var view = home.gameObject.AddComponent<UI_VillageHome>();
                Label("Title", home, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.96f), 37).text = "출전 준비";
                Label("Hint", home, new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.79f), 25).text = "장비·성장·룬·공급품을 준비하세요";
                var heroes = ThreeSouls.Data.LobbyTables.Get<ThreeSouls.Data.Generated.LobbyHeroTableDataTable>().Items;
                for (var i = 0; i < heroes.Length; i++)
                {
                    var center = 0.185f + 0.315f * i;
                    var portrait = Rect("Hero" + i, home, new Vector2(center - 0.12f, 0.24f), new Vector2(center + 0.12f, 0.60f));
                    var picture = portrait.gameObject.AddComponent<UnityEngine.UI.Image>();
                    picture.sprite = LobbyResourceCatalog.GetSprite(heroes[i].PortraitImagePath, heroes[i].PortraitSpriteName);
                    picture.preserveAspect = true;
                    picture.raycastTarget = false;
                    Label("Name" + i, home, new Vector2(center - 0.14f, 0.15f), new Vector2(center + 0.14f, 0.25f), 27).text =
                        ThreeSouls.Data.LobbyTables.GetText(heroes[i].NameStrKey);
                }
                Label("Stage", home, new Vector2(0.03f, 0.015f), new Vector2(0.97f, 0.12f), 25).text = "폐광·유적·숲·설원 총 20스테이지 출전 가능";
                Bind(view, "DungeonButton", tabs.transform.Find("Navigation/OpenDungeonMap") ?? page.Find("OpenDungeonMap"));
                Bind(view, "ShopButton", page.Find("OpenEquipmentShop"));
                home.gameObject.SetActive(false);
            }
            page.Find("UIOnlyHome/Stage").GetComponent<UnityEngine.UI.Text>().text = "폐광·유적·숲·설원 총 20스테이지 출전 가능";
        }

        private static UI_HUD_Supplies BuildSupplyHud()
        {
            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI/HUD");
            var root = Rect("UI_HUD_Supplies", null, Vector2.zero, Vector2.one);
            var view = root.gameObject.AddComponent<UI_HUD_Supplies>();
            var panel = Rect("SupplyBar", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            panel.anchoredPosition = new Vector2(0, 463);
            panel.sizeDelta = new Vector2(610, 105);
            var backdrop = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color32(17, 25, 35, 235);
            backdrop.raycastTarget = false;
            var buttons = new UnityEngine.UI.Button[SupplyLoadoutCount];
            var labels = new UnityEngine.UI.Text[SupplyLoadoutCount];
            for (var i = 0; i < SupplyLoadoutCount; i++)
            {
                var rect = Rect("Slot" + i, panel, new Vector2(0.025f + i * 0.495f, 0.40f), new Vector2(0.475f + i * 0.495f, 0.91f));
                var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = new Color32(65, 54, 40, 255);
                var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                buttons[i] = button;
                labels[i] = Label("Label", rect, Vector2.zero, Vector2.one, 25);
            }
            var status = Label("Status", panel, new Vector2(0, 0.04f), new Vector2(1, 0.37f), 23);
            Bind(view, "Buttons", buttons);
            Bind(view, "Labels", labels);
            Bind(view, "Status", status);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, SuppliesPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<UI_HUD_Supplies>();
        }

        private const int SupplyLoadoutCount = ThreeSouls.Data.SupplyLoadout.SlotCount;

        private static void ConnectScenes()
        {
            var start = SceneManager.GetSceneByPath("Assets/Scenes/Scene_Start.unity");
            var startWasLoaded = start.IsValid() == true;
            if (startWasLoaded == false)
            {
                start = EditorSceneManager.OpenScene("Assets/Scenes/Scene_Start.unity", OpenSceneMode.Additive);
            }
            var eventSystem = start.GetRootGameObjects().Single(x => x.GetComponent<UnityEngine.EventSystems.EventSystem>() != null);
            var village = SceneManager.GetSceneByPath("Assets/Scenes/Scene_Village.unity");
            var villageWasLoaded = village.IsValid() == true;
            if (villageWasLoaded == false)
            {
                village = EditorSceneManager.OpenScene("Assets/Scenes/Scene_Village.unity", OpenSceneMode.Additive);
            }
            if (village.GetRootGameObjects().Any(x => x.GetComponent<Scene_Village>() != null) == false)
            {
                var setting = new GameObject("Setting_SceneVillage");
                SceneManager.MoveGameObjectToScene(setting, village);
                setting.AddComponent<Scene_Village>();
            }
            if (village.GetRootGameObjects().Any(x => x.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) == false)
            {
                var instance = UnityEngine.Object.Instantiate(eventSystem);
                instance.name = "EventSystem";
                SceneManager.MoveGameObjectToScene(instance, village);
            }
            EditorSceneManager.SaveScene(village);
            var camera = village.GetRootGameObjects().Select(x => x.GetComponent<Camera>()).First(x => x != null);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(14, 23, 31, 255);
            EditorSceneManager.SaveScene(village);
            var dungeon = SceneManager.GetSceneByPath("Assets/Scenes/Scene_Dungeon.unity");
            var dungeonWasLoaded = dungeon.IsValid() == true;
            if (dungeonWasLoaded == false)
            {
                dungeon = EditorSceneManager.OpenScene("Assets/Scenes/Scene_Dungeon.unity", OpenSceneMode.Additive);
            }
            var roots = dungeon.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "GrassyPlainsGrid" || roots[i].name == "Canvas")
                {
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }
            var sceneCombat = roots.Where(x => x != null).Select(x => x.GetComponent<Scene_Combat>()).First(x => x != null);
            EditorSceneManager.SaveScene(dungeon);
            EditorSceneManager.SaveScene(start);
            if (dungeonWasLoaded == false)
            {
                EditorSceneManager.CloseScene(dungeon, true);
            }
            if (startWasLoaded == false)
            {
                EditorSceneManager.CloseScene(start, true);
            }
            if (villageWasLoaded == false)
            {
                EditorSceneManager.CloseScene(village, true);
            }
            var builds = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var paths = new[] { "Assets/Scenes/Scene_Start.unity", "Assets/Scenes/Scene_Village.unity", "Assets/Scenes/Scene_Dungeon.unity" };
            for (var i = 0; i < paths.Length; i++)
            {
                var existing = builds.Find(x => x.path == paths[i]);
                if (existing == null)
                {
                    builds.Add(new EditorBuildSettingsScene(paths[i], true));
                }
                else
                {
                    existing.enabled = true;
                }
            }
            EditorBuildSettings.scenes = builds.ToArray();
        }

        private static void RegisterResources()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var prefabs = settings.FindGroup("Prefabs");
            var paths = new[] { LobbyPath, SuppliesPath };
            for (var i = 0; i < paths.Length; i++)
            {
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(paths[i]), prefabs).address = paths[i];
            }
            // 폴더의 PackSeparately는 폴더 단위이므로 실제 의존 에셋도 개별 등록합니다.
            var names = new[] { "Prefabs", "Images", "Animations", "SpriteLibrarys", "LobbyResources" };
            var snapshots = new Dictionary<string, AddressableAssetEntry>(StringComparer.Ordinal);
            for (var i = 0; i < names.Length; i++)
            {
                var group = settings.FindGroup(names[i]);
                var entries = new List<AddressableAssetEntry>();
                group.GatherAllAssets(entries, true, true, false);
                for (var j = 0; j < entries.Count; j++)
                {
                    if (AssetDatabase.IsValidFolder(entries[j].AssetPath) == false)
                    {
                        snapshots[entries[j].AssetPath] = entries[j];
                    }
                }
                group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            }
            var roots = snapshots.Values.Where(x => x.parentGroup == prefabs).Select(x => x.AssetPath).ToArray();
            var dependencies = AssetDatabase.GetDependencies(roots, true);
            for (var i = 0; i < dependencies.Length; i++)
            {
                if (snapshots.TryGetValue(dependencies[i], out var previous) == true)
                {
                    var address = previous.address;
                    settings.CreateOrMoveEntry(previous.guid, previous.parentGroup).address = address;
                }
            }
            AddressableKeysGenerator.Generate(false);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 minimum, Vector2 maximum)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static UnityEngine.UI.Text Label(string name, Transform parent, Vector2 minimum, Vector2 maximum, int size)
        {
            var rect = Rect(name, parent, minimum, maximum);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Mulmaru.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(247, 224, 176, 255);
            text.raycastTarget = false;
            return text;
        }

        private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
