using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using ThreeSouls.Combat;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Growth;
using ThreeSouls.Manager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 격리된 사용자 데이터에서 지도 선택과 출전 준비 및 룬 스냅샷을 검사합니다.
    /// </summary>
    public static class DungeonSelectionVerification
    {
        [MenuItem("Three Souls/Village/Verify Dungeon Selection (Edit Only)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                throw new InvalidOperationException("Edit 모드에서 검사하세요.");
            }

            var checks = new List<string>();
            void Check(bool success, string name)
            {
                if (success == false)
                {
                    throw new InvalidOperationException("Rune check failed: " + name);
                }

                checks.Add(name);
            }

            var original = SceneManager.GetActiveScene();
            var dirty = original.isDirty;
            var preview = EditorSceneManager.NewPreviewScene();
            var fixture = new GameObject("RuneFixture");
            fixture.SetActive(false);
            SceneManager.MoveGameObjectToScene(fixture, preview);
            var singleton = typeof(GameManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousSingleton = singleton.GetValue(null);
            var handles = new List<AsyncOperationHandle>();
            try
            {
                var game = fixture.AddComponent<GameManager>();
                var user = fixture.AddComponent<UserManager>();
                var equipment = fixture.AddComponent<EquipmentManager>();
                var inventory = fixture.AddComponent<InventoryManager>();
                var equipmentGrowth = fixture.AddComponent<EquipmentGrowthManager>();
                var tables = fixture.AddComponent<TableManager>();
                var assets = fixture.AddComponent<AddressableManager>();
                var managers = (IDictionary)typeof(GameManager).GetField("ManagerContainer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                var components = new BaseManager[] { user, equipment, inventory, equipmentGrowth, tables, assets };
                for (var i = 0; i < components.Length; i++)
                {
                    managers.Add(components[i].GetType(), components[i]);
                }

                singleton.SetValue(null, game);
                var cache = typeof(VillageLobbyVerification).GetMethod("Cache", BindingFlags.Static | BindingFlags.NonPublic);
                cache.Invoke(null, new object[] { assets, handles, ScriptableObjectDatas.CharacterTableData.ToAddress(), ScriptableObjectDatas.CharacterTableData.ToAddress() });
                cache.Invoke(null, new object[] { assets, handles, ScriptableObjectDatas.StringTableData.ToAddress(), ScriptableObjectDatas.StringTableData.ToAddress() });
                cache.Invoke(null, new object[] { assets, handles, ScriptableObjectDatas.EquipmentTableData.ToAddress(), ScriptableObjectDatas.EquipmentTableData.ToAddress() });
                user.Initialize();
                user.ApplyTableRules();
                var runes = user.GetRuneManager();
                var preparation = user.GetDungeonPreparationManager();
                var data = user.UData.DungeonPreparation;
                var maps = LobbyTables.Get<DungeonMapTableDataTable>().Items;
                Check(maps.Length == 4 && LobbyTables.Get<DungeonStageSelectionTableDataTable>().Items.Length == 20, "Four dungeons and twenty selection stages");
                Check(preparation.TrySelectStage("Mine_1", out _) == false, "Stage selection outside lobby rejected");
                Check(preparation.TryPrepareDeparture(out _) == false, "Departure outside lobby rejected");
                runes.BeginLobbySession();
                runes.GrantTestRunes();
                preparation.BeginLobbySession();
                Check(preparation.TrySelectStage("missing", out _) == false && data.SelectedStageKey == null, "Unknown selection leaves draft unchanged");
                Check(preparation.TryPrepareDeparture(out _) == false, "No departure without stage");
                var notifications = 0;
                DungeonPreparationManager.OnPreparationChangedDelegate listener = () => notifications++;
                preparation.OnPreparationChangedEventHandler += listener;
                preparation.OnPreparationChangedEventHandler += listener;
                var gold = user.UData.Gold;
                var stones = user.UData.EnhancementStones;
                for (var i = 0; i < maps.Length; i++)
                {
                    var stages = ThreeSouls.Dungeon.DungeonSelectionRules.GetStages(maps[i].Key);
                    Check(stages.Count == 5, "Five stages in " + maps[i].Key);
                    for (var j = 0; j < stages.Count; j++)
                    {
                        var stage = stages[j];
                        var previous = notifications;
                        Check(preparation.TrySelectStage(stage.Key, out _) && notifications == previous + 1, "Exactly one selection notification " + stage.Key);
                        Check(data.State == ThreeSouls.Dungeon.DungeonPreparationState.Preparing && data.Departure == null, "Fresh preparation " + stage.Key);
                        Check(preparation.TryPrepareDeparture(out _), "Departure preparation " + stage.Key);
                        var plan = data.Departure;
                        Check(plan.RegionNumber == maps[i].RegionNumber && plan.StageNumber == j + 1 && plan.StageKey == stage.Key,
                            "Correct departure identity " + stage.Key);
                        Check(plan.GetRuneSlots().Count == 9, "Three sanctuary and six skill slots " + stage.Key);
                        previous = notifications;
                        Check(preparation.TryPrepareDeparture(out _) == false && ReferenceEquals(data.Departure, plan) && notifications == previous,
                            "Repeated departure cannot commit twice " + stage.Key);
                    }
                }
                Check(user.UData.Gold == gold && user.UData.EnhancementStones == stones, "Selection and departure do not spend currency");
                var frozen = data.Departure;
                Check(runes.TryEquip("Shrine_Gold", out _), "Runes remain editable before scene transition");
                Check(data.State == ThreeSouls.Dungeon.DungeonPreparationState.Preparing && data.Departure == null, "Rune change invalidates stale preparation");
                Check(Math.Abs(frozen.Runes.GoldMultiplier - 1.1f) < .0001f, "Prior immutable snapshot keeps original gold bonus");
                Check(preparation.TryPrepareDeparture(out _) && Math.Abs(data.Departure.Runes.GoldMultiplier - 1.16f) < .0001f,
                    "Reconfirmation captures current sanctuary rune");
                Check(data.Departure.GetRuneSlots().Count(x => x.RuneKey == "Shrine_Gold" && x.Rank == 1) == 1, "Loadout snapshot captures key and rank");
                var goldPlan = data.Departure;
                Check(runes.TryEquip("HeavySlash_Power", out _) && data.Departure == null, "Skill replacement invalidates preparation");
                Check(preparation.TryPrepareDeparture(out _) && Math.Abs(data.Departure.Runes.GetSkill("HeavySlash").DamageMultiplier - 1.08f) < .0001f,
                    "Prepared skill effect matches equipped rune");
                Check(goldPlan.Runes.GetSkill("HeavySlash").DamageMultiplier == 1, "Previous skill snapshot remains immutable");
                var readyPlan = data.Departure;
                var frozenName = readyPlan.StageName;
                var selected = ThreeSouls.Dungeon.DungeonSelectionRules.GetStage(data.SelectedStageKey);
                var originalName = selected.DisplayName;
                try
                {
                    selected.DisplayName = "changed";
                    Check(readyPlan.StageName == frozenName, "Departure copies stage text rather than owning mutable table row");
                }
                finally
                {
                    selected.DisplayName = originalName;
                }
                Check(preparation.TryResumePreparation(out _) && data.State == ThreeSouls.Dungeon.DungeonPreparationState.Preparing && data.Departure == null,
                    "Resume discards ready result but retains selection");
                Check(preparation.TryResumePreparation(out _) == false, "Repeated resume rejected");
                try
                {
                    selected.Enabled = false;
                    Check(preparation.TryPrepareDeparture(out _) == false && data.SelectedStageKey == null, "Disabled-after-selection stage clears stale draft");
                    Check(preparation.TrySelectStage(selected.Key, out _) == false, "Disabled selection rejected");
                }
                finally
                {
                    selected.Enabled = true;
                }
                Check(preparation.TrySelectStage("Mine_2", out _) && preparation.TryPrepareDeparture(out _), "New stage can prepare after stale draft cancellation");
                preparation.CancelSelection();
                Check(data.SelectedStageKey == null && data.Departure == null && data.State == ThreeSouls.Dungeon.DungeonPreparationState.Selecting,
                    "Cancel clears selection and departure");
                preparation.EndLobbySession();
                Check(preparation.TrySelectStage("Mine_1", out _) == false, "Lobby exit blocks new selection");
                preparation.BeginLobbySession();
                preparation.BeginLobbySession();
                preparation.TrySelectStage("Mine_1", out _);
                preparation.TryPrepareDeparture(out _);
                var beforeChange = notifications;
                runes.TryUnequip("Shrine_Gold", out _);
                Check(notifications == beforeChange + 1 && data.Departure == null, "Repeated session connection does not duplicate rune invalidation");
                preparation.OnPreparationChangedEventHandler -= listener;
                beforeChange = notifications;
                preparation.CancelSelection();
                Check(notifications == beforeChange, "Explicit notification removal");
                var savedX = maps[0].WorldX;
                try
                {
                    maps[0].WorldX = float.NaN;
                    var threw = false;
                    try { ThreeSouls.Dungeon.DungeonSelectionRules.Validate(); }
                    catch (InvalidOperationException) { threw = true; }
                    Check(threw, "Nonfinite map coordinates rejected at validation boundary");
                }
                finally
                {
                    maps[0].WorldX = savedX;
                }
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Lobby/UI_DungeonSelection.prefab");
                Check(prefab != null && prefab.GetComponentsInChildren<ThreeSouls.UI.UI_DungeonPreparation>(true).Length == 1, "Map contains one preparation popup");
                var view = prefab.GetComponent<ThreeSouls.UI.UI_DungeonSelection>();
                var serialized = new SerializedObject(view);
                var textures = serialized.FindProperty("MapTextures");
                Check(textures.arraySize == 5, "Native overview and four dungeon textures connected");
                for (var i = 0; i < textures.arraySize; i++)
                {
                    var texture = (Sprite)textures.GetArrayElementAtIndex(i).objectReferenceValue;
                    var expected = "Assets/Images/Map/Previews/" + (i == 0 ? "NativeWorldMap" : "NativeDungeon_" + i) + ".png";
                    Check(AssetDatabase.GetAssetPath(texture) == expected && texture.rect.width == 1024 && texture.rect.height == 576,
                        "Actual tilemap preview and dimensions verified " + expected);
                }
                Check(original == SceneManager.GetActiveScene() && dirty == original.isDirty, "Open scene unchanged by isolated checks");
                Directory.CreateDirectory("Docs/Previews/DungeonSelection");
                File.WriteAllText("Docs/Previews/DungeonSelection/EditChecks.txt", "Isolated Edit mode\nPassed=" + checks.Count + "\n" + string.Join("\n", checks));
                Debug.Log("Dungeon selection Edit checks passed: " + checks.Count);

            }
            finally
            {
                singleton.SetValue(null, previousSingleton);
                EditorSceneManager.ClosePreviewScene(preview);
                for (var i = 0; i < handles.Count; i++)
                {
                    if (handles[i].IsValid() == true)
                    {
                        UnityEngine.AddressableAssets.Addressables.Release(handles[i]);
                    }
                }
            }
        }
    }
}
