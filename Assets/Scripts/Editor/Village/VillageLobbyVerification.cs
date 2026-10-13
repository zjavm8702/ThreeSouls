using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 열린 씬과 사용자 데이터를 변경하지 않고 격리 Edit 씬에서 로비를 검증하고 렌더합니다.
    /// </summary>
    public static class VillageLobbyVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<string> Checks = new();

        [MenuItem("Three Souls/Village/Verify Lobby UI (Edit Only)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                throw new InvalidOperationException("로비 검사는 Edit 모드 전용입니다.");
            }

            Checks.Clear();
            var previousScene = SceneManager.GetActiveScene();
            var previousDirty = previousScene.isDirty;
            var preview = EditorSceneManager.NewPreviewScene();
            var singleton = typeof(GameManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousSingleton = singleton.GetValue(null);
            var random = UnityEngine.Random.state;
            var handles = new List<AsyncOperationHandle>();
            var fixture = new GameObject("LobbyVerificationManagers");
            fixture.SetActive(false);
            SceneManager.MoveGameObjectToScene(fixture, preview);
            GameObject ui = null;

            try
            {
                var game = fixture.AddComponent<GameManager>();
                var inventory = fixture.AddComponent<InventoryManager>();
                var equipment = fixture.AddComponent<EquipmentManager>();
                var user = fixture.AddComponent<UserManager>();
                var growth = fixture.AddComponent<EquipmentGrowthManager>();
                var tables = fixture.AddComponent<TableManager>();
                var assets = fixture.AddComponent<AddressableManager>();
                var managers = (IDictionary)Read(game, "ManagerContainer");
                var components = new BaseManager[] { inventory, equipment, user, growth, tables, assets };

                for (var i = 0; i < components.Length; i++)
                {
                    managers.Add(components[i].GetType(), components[i]);
                }

                singleton.SetValue(null, game);
                Cache(assets, handles, ScriptableObjectDatas.CharacterTableData.ToAddress(),
                    ScriptableObjectDatas.CharacterTableData.ToAddress());
                Cache(assets, handles, ScriptableObjectDatas.StringTableData.ToAddress(),
                    ScriptableObjectDatas.StringTableData.ToAddress());
                Cache(assets, handles, ScriptableObjectDatas.EquipmentTableData.ToAddress(),
                    ScriptableObjectDatas.EquipmentTableData.ToAddress());
                user.Initialize();
                user.ApplyTableRules();
                UnityEngine.Random.InitState(3109);
                var test = fixture.AddComponent<T_VillageData>();
                CheckSceneConnections(previousScene, fixture);
                var definitions = AssetDatabase.LoadAssetAtPath<EquipmentTableDataTable>(ScriptableObjectDatas.EquipmentTableData.ToAddress());
                Call(test, "SeedEquipment", definitions, equipment, inventory);
                Check(user.UData.Inventory.GetCount() == 24, "Test seed: 24 owned items through managers");
                var items = user.UData.Inventory.GetItems();
                var unique = new HashSet<string>();

                for (var i = 0; i < items.Length; i++)
                {
                    Check(unique.Add(items[i].InstanceId), "Unique instance " + i);
                    var row = tables.GetEquipmentTableData(items[i].ItemKey);
                    Check(items[i].AttackDmg >= row.AttackDmgMin && items[i].AttackDmg <= row.AttackDmgMax
                        && items[i].MaxHP >= row.MaxHPMin && items[i].MaxHP <= row.MaxHPMax
                        && items[i].AttackSpeedBonus >= row.AttackSpeedBonusMin && items[i].AttackSpeedBonus <= row.AttackSpeedBonusMax,
                        "Table stat range " + i);
                }

                user.AddCombatReward(12500, 0);
                var sourceCanvas = previousScene.GetRootGameObjects().Single(root => root.name == "CanvasGroup")
                    .transform.Find("UICanvas").GetComponent<Canvas>();
                var host = new GameObject("VerificationCanvas", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(host, preview);
                EditorUtility.CopySerialized(sourceCanvas, host.AddComponent<Canvas>());
                EditorUtility.CopySerialized(sourceCanvas.GetComponent<CanvasScaler>(), host.AddComponent<CanvasScaler>());
                host.AddComponent<GraphicRaycaster>();
                ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VillageLobbyUIBuilder.PrefabPath), host.transform);
                var tabs = ui.GetComponentInChildren<UI_LobbyTabs>(true);
                var panel = ui.GetComponentInChildren<UI_LobbyInventory>(true);
                var characters = ui.GetComponentInChildren<UI_LobbyCharacters>(true);
                Call(tabs, "Awake");
                Call(panel, "Awake");
                tabs.SelectTab(LobbyTab.Inventory);
                tabs.SetUp(user);
                panel.SetUp(user.UData, inventory, equipment, tables, growth);
                characters.SetUp(equipment, tables, user.GetHeroGrowthManager());
                Check(Read<Text>(tabs, "Gold").text == "12,500", "Currency initial state");
                user.AddCombatReward(100, 0);
                Check(Read<Text>(tabs, "Gold").text == "12,600", "Currency event updates immediately");
                user.AddCombatReward(0, 0);
                Check(Read<Text>(panel, "Count").text.Contains("24 / 80"), "Inventory count and capacity");
                Check(ui.GetComponentsInChildren<UI_LobbyItem>(true).Length == 25, "Grid reuses 25 cells for 24 items");
                Check(CountListeners(user.UData.Inventory, "onInventoryChangedEventHandler") == 1, "Inventory subscribes once");
                panel.SetUp(user.UData, inventory, equipment, tables, growth);
                Check(CountListeners(user.UData.Inventory, "onInventoryChangedEventHandler") == 1, "Reconnection does not duplicate subscription");

                var cells = ui.GetComponentsInChildren<UI_LobbyItem>(true);

                for (var i = 0; i < cells.Length; i++)
                {
                    Call(cells[i], "Awake");
                }

                Read<Button>(cells[0], "SelectButton").onClick.Invoke();
                Check(Read<string>(panel, "SelectedItem") == Read<string>(cells[0], "InstanceId"), "Cell click selects item");
                var filters = Read<Button[]>(panel, "FilterButtons");
                var expected = new[] { 24, 18, 3, 3, 3 };

                for (var i = 0; i < filters.Length; i++)
                {
                    filters[i].onClick.Invoke();
                    Check(((IList)Read(panel, "VisibleItems")).Count == expected[i], "Filter count " + i);
                }

                filters[0].onClick.Invoke();
                var sword = items[0];
                panel.SelectItem(sword.InstanceId);
                Check(Read<Text>(panel, "EquipLabel").text == "장착 해제", "Equipped item offers unequip");
                Check(Read<Button>(panel, "DiscardButton").interactable == false, "Equipped discard is disabled");
                Read<Button[]>(panel, "HeroButtons")[1].onClick.Invoke();
                Check(Read<Button>(panel, "EquipButton").interactable == false, "Wrong hero equip is disabled");
                Read<Button[]>(panel, "HeroButtons")[0].onClick.Invoke();
                Read<Button>(panel, "EquipButton").onClick.Invoke();
                Check(equipment.GetOwner(sword.InstanceId) == null, "Unequip request uses manager");
                Read<Button>(panel, "EquipButton").onClick.Invoke();
                Check(equipment.GetOwner(sword.InstanceId) == "Key_night", "Equip request uses manager");
                var armor = items[3];
                panel.SelectItem(armor.InstanceId);
                Read<Button>(panel, "EquipButton").onClick.Invoke();
                Check(equipment.GetOwner(armor.InstanceId) == "Key_night", "Shared armor equips");
                Read<Button[]>(panel, "HeroButtons")[1].onClick.Invoke();
                Check(Read<Button>(panel, "EquipButton").interactable == false, "Other hero ownership blocks equip");
                Check(inventory.TryDiscard(armor.InstanceId) == false, "Manager also rejects equipped discard");

                tabs.SelectTab(LobbyTab.Characters);
                Call(panel, "OnDisable");
                Call(characters, "OnEnable");
                Check(Read<Text[]>(characters, "EquipmentLabels")[0].text.Contains(tables.GetEquipmentName(armor.ItemKey)), "Character tab shows equipped armor");
                var previousStats = Read<Text[]>(characters, "Stats")[0].text;
                equipment.TryUnequip("Key_night", EquipSlot.Armor);
                Check(previousStats != Read<Text[]>(characters, "Stats")[0].text, "Character stats update from equipment event");
                Check(CountListeners(user.UData.Inventory, "onInventoryChangedEventHandler") == 0, "Hidden inventory unsubscribes");
                Render(ui, preview, "Characters", 1080, 1920);
                tabs.SelectTab(LobbyTab.Inventory);
                Call(characters, "OnDisable");
                Call(panel, "OnEnable");
                Check(CountListeners(user.UData.Inventory, "onInventoryChangedEventHandler") == 1, "Returned inventory reconnects");
                panel.SelectItem(armor.InstanceId);
                Read<Button>(panel, "DiscardButton").onClick.Invoke();
                Check(Read<GameObject>(panel, "DiscardDialog").activeSelf == true, "Discard requires confirmation");
                Read<Button>(panel, "CancelDiscard").onClick.Invoke();
                Check(user.UData.Inventory.FindEquipment(armor.InstanceId) != null, "Cancel retains item");
                Read<Button>(panel, "DiscardButton").onClick.Invoke();
                Read<Button>(panel, "ConfirmDiscard").onClick.Invoke();
                Check(user.UData.Inventory.FindEquipment(armor.InstanceId) == null, "Confirmed discard removes selected item");
                Check(user.UData.Inventory.GetCount() == 23, "Discard updates count");
                Check(Read<GameObject>(panel, "DiscardDialog").activeSelf == false, "Confirmation closes after discard");
                Read<Button>(panel, "OrderButton").onClick.Invoke();
                Check(Read<Text>(panel, "OrderLabel").text.Contains("이름순"), "Sort cycles to name");
                Read<Button>(panel, "OrderButton").onClick.Invoke();
                var visible = (List<EquipmentInstance>)Read(panel, "VisibleItems");

                for (var i = 1; i < visible.Count; i++)
                {
                    Check(visible[i - 1].AttackDmg >= visible[i].AttackDmg, "Descending attack order " + i);
                }

                Read<Button>(panel, "OrderButton").onClick.Invoke();
                Read<Button[]>(panel, "HeroButtons")[0].onClick.Invoke();
                panel.SelectItem(items[5].InstanceId);
                Render(ui, preview, "Inventory", 1080, 1920);
                Render(ui, preview, "Inventory_Landscape", 1920, 1080);
                tabs.SelectTab(LobbyTab.Village);
                Check(panel.gameObject.activeSelf == false && characters.gameObject.activeSelf == false, "Village tab hides other pages");
                Read<Button[]>(tabs, "TabButtons")[1].onClick.Invoke();
                Check(panel.gameObject.activeSelf == true, "Navigation opens inventory");
                filters[4].onClick.Invoke();

                for (var i = 0; i < 3; i++)
                {
                    equipment.TryUnequip(LobbyPresentationData.GetHeroKey(i), EquipSlot.Weapon);
                }

                Check(((IList)Read(panel, "VisibleItems")).Count == 0, "Empty equipped filter has no items");
                Check(Read<Text>(panel, "Empty").text.Length > 0 && Read<Button>(panel, "EquipButton").interactable == false,
                    "Empty state displays message and disables actions");
                var transforms = ui.GetComponentsInChildren<Transform>(true);
                var missing = 0;

                for (var i = 0; i < transforms.Length; i++)
                {
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[i].gameObject);
                }

                Check(missing == 0, "Prefab Missing Script count is zero");
                Check(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, "Android target retained");
                Check(EditorApplication.isPlaying == false && previousScene.isDirty == previousDirty,
                    "Play never entered; original scene dirty state retained");
                Directory.CreateDirectory("Docs/Previews/Lobby");
                File.WriteAllText("Docs/Previews/Lobby/Verification.txt",
                    "Edit mode only; no Play or device verification\nPassed=" + Checks.Count + "\n" + string.Join("\n", Checks));
                Debug.Log("Lobby Edit checks passed: " + Checks.Count);
            }
            finally
            {
                if (ui != null)
                {
                    UnityEngine.Object.DestroyImmediate(ui);
                }

                singleton.SetValue(null, previousSingleton);

                for (var i = 0; i < handles.Count; i++)
                {
                    Addressables.Release(handles[i]);
                }

                UnityEngine.Object.DestroyImmediate(fixture);
                EditorSceneManager.ClosePreviewScene(preview);
                UnityEngine.Random.state = random;
            }
        }

        private static void Cache(AddressableManager assets, List<AsyncOperationHandle> handles, string key, string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            var handle = Addressables.ResourceManager.CreateCompletedOperation(asset, string.Empty);
            handles.Add(handle);
            ((Dictionary<string, AssetData>)Read(assets, "LoadedAssets")).Add(key, new AssetData(key, handle, null, false));
        }

        private static void CheckSceneConnections(Scene scene, GameObject fixture)
        {
            var roots = scene.GetRootGameObjects();
            Check(roots.Count(root => root.CompareTag("CanvasGroup") == true) == 1,
                "One existing CanvasGroup; duplicate lobby canvases removed");
            var group = roots.Single(root => root.CompareTag("CanvasGroup") == true);
            Check(group.GetComponentsInChildren<Canvas>(true).Length == 3, "Existing three canvases retained");
            var tabs = group.GetComponentInChildren<UI_LobbyTabs>(true);
            Check(tabs.transform.parent.parent.name == "UICanvas", "Lobby attached to existing UICanvas");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageLobbyUIBuilder.PrefabPath);
            Check(prefab.GetComponentsInChildren<Canvas>(true).Length == 0, "Lobby prefab owns no Canvas");
            var sourceCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
            Check(sourceCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>() != null, "Main camera has Cinemachine Brain");
            Check(sourceCamera.GetComponent<ThreeSouls.Village.VillageCameraFrame>() == null,
                "Old village framing no longer competes with Cinemachine");
            var sourceDriver = roots.Select(root => root.GetComponent<T_Camera>()).Single(driver => driver != null);
            var source = new SerializedObject(sourceDriver);
            Check(source.FindProperty("FollowCamera").objectReferenceValue != null
                && source.FindProperty("CameraBounds").objectReferenceValue is PolygonCollider2D,
                "Transferred camera and polygon bounds are connected");
            var cameraObject = new GameObject("CameraContractFixture");
            cameraObject.SetActive(false);
            cameraObject.transform.SetParent(fixture.transform, false);
            var camera = cameraObject.AddComponent<Unity.Cinemachine.CinemachineCamera>();
            cameraObject.AddComponent<Unity.Cinemachine.CinemachinePositionComposer>();
            cameraObject.AddComponent<Unity.Cinemachine.CinemachineConfiner2D>();
            var polygon = cameraObject.AddComponent<PolygonCollider2D>();
            polygon.points = ((PolygonCollider2D)source.FindProperty("CameraBounds").objectReferenceValue).points;
            var driverFixture = cameraObject.AddComponent<T_Camera>();
            var driverData = new SerializedObject(driverFixture);
            driverData.FindProperty("FollowCamera").objectReferenceValue = camera;
            driverData.FindProperty("CameraBounds").objectReferenceValue = polygon;
            driverData.FindProperty("CameraPresentationSettings").objectReferenceValue = source.FindProperty("CameraPresentationSettings").objectReferenceValue;
            driverData.ApplyModifiedPropertiesWithoutUndo();
            Check((bool)Call(driverFixture, "SetUpCamera") == true, "Existing T_Camera setup succeeds in isolated Edit fixture");
            Check(camera.GetComponent<Unity.Cinemachine.CinemachineConfiner2D>().BoundingShape2D == polygon,
                "Existing camera setup applies bounds");
        }

        private static object Read(object target, string name)
        {
            return target.GetType().GetField(name, Private).GetValue(target);
        }

        private static T Read<T>(object target, string name)
        {
            return (T)Read(target, name);
        }

        private static object Call(object target, string name, params object[] args)
        {
            return target.GetType().GetMethod(name, Private).Invoke(target, args);
        }

        private static int CountListeners(object target, string field)
        {
            return ((Delegate)Read(target, field))?.GetInvocationList().Length ?? 0;
        }

        private static void Check(bool result, string name)
        {
            if (result == false)
            {
                throw new InvalidOperationException("Lobby check failed: " + name);
            }

            Checks.Add(name);
        }

        private static void Render(GameObject ui, Scene preview, string name, int width, int height)
        {
            var cameraObject = new GameObject("LobbyPreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(29, 39, 39, 255);
            camera.cullingMask = 1 << 31;
            var transforms = ui.GetComponentsInChildren<Transform>(true);

            for (var i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = 31;
            }

            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            var canvases = ui.GetComponentsInParent<Canvas>(true);

            try
            {
                camera.targetTexture = target;

                for (var i = 0; i < canvases.Length; i++)
                {
                    canvases[i].gameObject.layer = 31;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 5;
                }

                Canvas.ForceUpdateCanvases();
                ui.GetComponentInChildren<UI_LobbyLayout>(true).Apply();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)ui.transform);
                var graphics = ui.GetComponentsInChildren<Graphic>();

                for (var i = 0; i < graphics.Length; i++)
                {
                    graphics[i].SetAllDirty();
                    graphics[i].Rebuild(CanvasUpdate.PreRender);
                }

                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                var pixels = texture.GetPixels32();
                var changed = 0;

                for (var i = 1; i < pixels.Length; i++)
                {
                    if (pixels[i].Equals(pixels[0]) == false)
                    {
                        changed++;
                    }
                }

                Check(changed > width * height / 10, "Visible UI pixels in " + name);
                Directory.CreateDirectory("Docs/Previews/Lobby");
                File.WriteAllBytes("Docs/Previews/Lobby/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                for (var i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].worldCamera = null;
                }

                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(texture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
