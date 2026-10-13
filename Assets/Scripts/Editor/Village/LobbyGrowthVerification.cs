using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 격리된 Edit 씬에서 비용 차감, 강화 상한, 잠금, 분해와 실제 UI 버튼을 검사합니다.
    /// </summary>
    public static class LobbyGrowthVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Three Souls/Village/Verify Lobby Growth (Edit Only)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                throw new InvalidOperationException("성장 검사는 Edit 모드 전용입니다.");
            }

            var checks = new List<string>();
            void Check(bool ok, string name)
            {
                if (ok == false)
                {
                    throw new InvalidOperationException("Growth check failed: " + name);
                }

                checks.Add(name);
            }

            var original = SceneManager.GetActiveScene();
            var dirty = original.isDirty;
            var preview = EditorSceneManager.NewPreviewScene();
            var singleton = typeof(GameManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousSingleton = singleton.GetValue(null);
            var random = UnityEngine.Random.state;
            var handles = new List<AsyncOperationHandle>();
            var fixture = new GameObject("GrowthFixture");
            fixture.SetActive(false);
            SceneManager.MoveGameObjectToScene(fixture, preview);

            try
            {
                var game = fixture.AddComponent<GameManager>();
                var inventory = fixture.AddComponent<InventoryManager>();
                var equipment = fixture.AddComponent<EquipmentManager>();
                var growth = fixture.AddComponent<EquipmentGrowthManager>();
                var user = fixture.AddComponent<UserManager>();
                var tables = fixture.AddComponent<TableManager>();
                var assets = fixture.AddComponent<AddressableManager>();
                var managers = (IDictionary)Read(game, "ManagerContainer");
                var components = new BaseManager[] { inventory, equipment, growth, user, tables, assets };

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
                growth.BeginLobbySession();
                var test = fixture.AddComponent<T_VillageData>();
                var definitions = AssetDatabase.LoadAssetAtPath<EquipmentTableDataTable>(ScriptableObjectDatas.EquipmentTableData.ToAddress());
                Call(test, "SeedEquipment", definitions, equipment, inventory);
                var items = user.UData.Inventory.GetItems();
                var sword = items[0];
                var ring = items[4];
                var baseAttack = sword.AttackDmg;
                Check(growth.TryUpgrade(sword.InstanceId, out _) == false && sword.Growth.Level == 0, "Insufficient funds leave growth unchanged");
                user.AddCombatReward(100, 0);
                Check(growth.TryUpgrade(sword.InstanceId, out _) == false && user.UData.Gold == 100, "Missing stones do not charge gold");
                user.AddEnhancementStones(120);
                var growthEvents = 0;
                var equipmentEvents = 0;
                var observedCommittedState = false;
                growth.OnEquipmentGrowthChangedEventHandler += () => growthEvents++;
                equipment.OnEquipmentChangedEventHandler += key =>
                {
                    equipmentEvents++;
                    observedCommittedState = sword.Growth.Level == 1 && user.UData.Gold == 0 && user.UData.EnhancementStones == 119;
                };
                Check(growth.TryUpgrade(sword.InstanceId, out _) == true, "First upgrade succeeds");
                Check(observedCommittedState && growthEvents == 1 && equipmentEvents == 1, "Observers see all committed state and one hero notification");
                Check(sword.AttackDmg == baseAttack && Mathf.Approximately(equipment.GetItemBonuses(sword).AttackDmg, baseAttack * 1.05f), "Original roll stays immutable and effective damage increases");
                Check(Mathf.Approximately(equipment.GetEquipmentBonuses(tables.GetCharacterTableDataTable("Key_night")).AttackDmg, baseAttack * 1.05f), "Hero equipment uses same effective values");
                user.AddCombatReward(50000, 0);
                var goldBefore = user.UData.Gold;

                for (var i = 1; i < 10; i++)
                {
                    Check(growth.TryUpgrade(sword.InstanceId, out _) == true, "Upgrade reaches +" + (i + 1));
                }

                Check(goldBefore - user.UData.Gold == 16000 && sword.Growth.InvestedStones == 91, "Cumulative cost is 16100 gold / 91 stones including first upgrade");
                var maxGold = user.UData.Gold;
                var maxStones = user.UData.EnhancementStones;
                Check(growth.TryUpgrade(sword.InstanceId, out _) == false && user.UData.Gold == maxGold && user.UData.EnhancementStones == maxStones, "+10 rejects repeated upgrade without payment");
                Check(growth.TryDismantle(sword.InstanceId, out _, out _) == false, "Equipped dismantle rejected");
                Check(growth.TrySetLocked(sword.InstanceId, true, out _) == true && inventory.TryDiscard(sword.InstanceId) == false, "Lock is enforced in discard manager");
                equipment.TryUnequip("Key_night", EquipSlot.Weapon);
                Check(growth.TryDismantle(sword.InstanceId, out _, out _) == false, "Unequipped locked dismantle rejected");
                growth.TrySetLocked(sword.InstanceId, false, out _);
                var beforeDismantle = user.UData.EnhancementStones;
                Check(growth.TryDismantle(sword.InstanceId, out var awarded, out _) == true && awarded == 46, "Normal +10 dismantle returns 1 + floor(91 / 2)");
                Check(user.UData.Inventory.FindEquipment(sword.InstanceId) == null && user.UData.EnhancementStones == beforeDismantle + 46, "Dismantle removes item and grants once");
                Check(growth.TryDismantle(sword.InstanceId, out _, out _) == false && user.UData.EnhancementStones == beforeDismantle + 46, "Repeated dismantle cannot duplicate materials");
                Check(growth.TryUpgrade(ring.InstanceId, out _) == true && Mathf.Approximately(equipment.GetItemBonuses(ring).AttackSpeedBonus, ring.AttackSpeedBonus + 0.002f), "Accessory speed adds 0.2 percentage points");
                var armor = items[3];
                Check(growth.TryUpgrade(armor.InstanceId, out _) == true && Mathf.Approximately(equipment.GetItemBonuses(armor).MaxHP, armor.MaxHP * 1.05f), "Armor HP grows from original roll");
                Check(equipment.GetItemBonuses(armor).AttackSpeedBonus == 0, "Zero attack speed does not gain new stat");
                Check(EquipmentUpgradeRules.GetDismantleStones(ItemRarity.Normal, 0) == 1
                    && EquipmentUpgradeRules.GetDismantleStones(ItemRarity.Magic, 0) == 2
                    && EquipmentUpgradeRules.GetDismantleStones(ItemRarity.Rare, 0) == 4, "Three existing rarity rewards are 1 / 2 / 4");
                growth.EndLobbySession();
                Check(growth.TryUpgrade(ring.InstanceId, out _) == false && growth.TrySetLocked(ring.InstanceId, true, out _) == false
                    && growth.TryDismantle(ring.InstanceId, out _, out _) == false, "Growth operations reject outside lobby session");
                growth.BeginLobbySession();
                var ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VillageLobbyUIBuilder.PrefabPath));
                SceneManager.MoveGameObjectToScene(ui, preview);
                var tabs = ui.GetComponentInChildren<UI_LobbyTabs>(true);
                var panel = ui.GetComponentInChildren<UI_LobbyInventory>(true);
                var forge = ui.GetComponentInChildren<UI_LobbyForge>(true);
                var characters = ui.GetComponentInChildren<UI_LobbyCharacters>(true);
                Call(tabs, "Awake");
                Call(panel, "Awake");
                Call(forge, "Awake");
                tabs.SelectTab(LobbyTab.Inventory);
                panel.SetUp(user.UData, inventory, equipment, tables, growth);
                forge.SetUp(growth, equipment, user, tables, panel);
                characters.SetUp(equipment, tables, user.GetHeroGrowthManager());
                panel.SelectItem(armor.InstanceId);
                Read<Button>(forge, "OpenButton").onClick.Invoke();
                Check(Read<GameObject>(forge, "Panel").activeSelf == true && Read<Text>(forge, "Title").text.Contains("+1"), "Open button uses selected item and current upgrade");
                Read<Button>(forge, "UpgradeButton").onClick.Invoke();
                Check(armor.Growth.Level == 2 && Read<Text>(forge, "Title").text.Contains("+2"), "Upgrade button refreshes immediately");
                Check(Read<Text>(panel, "DetailTitle").text.Contains("+2"), "Inventory detail refreshes enhanced item");
                Read<Button>(forge, "LockButton").onClick.Invoke();
                Check(armor.Growth.Locked == true && Read<Button>(forge, "DismantleButton").interactable == false
                    && Read<Button>(panel, "DiscardButton").interactable == false, "Lock button disables both destructive UI actions");
                Read<Button>(forge, "LockButton").onClick.Invoke();
                Read<Button>(forge, "DismantleButton").onClick.Invoke();
                Check(Read<GameObject>(forge, "Confirmation").activeSelf == true && user.UData.Inventory.FindEquipment(armor.InstanceId) != null, "Dismantle waits for confirmation");
                Read<Button>(forge, "CancelButton").onClick.Invoke();
                Check(Read<GameObject>(forge, "Confirmation").activeSelf == false && user.UData.Inventory.FindEquipment(armor.InstanceId) != null, "Cancel preserves equipment");
                Read<Button>(forge, "DismantleButton").onClick.Invoke();
                Read<Button>(forge, "ConfirmButton").onClick.Invoke();
                Check(user.UData.Inventory.FindEquipment(armor.InstanceId) == null && Read<Text>(forge, "Title").text.Contains("없습니다"), "Confirmed UI dismantle removes only requested item");
                Check(growth.TryUpgrade(items[8].InstanceId, out _) == true && Read<Text>(forge, "Title").text.Contains("없습니다"), "Deleted forge selection does not switch to another item");
                Call(forge, "OnDisable");
                Check(Read<GameObject>(forge, "Panel").activeSelf == false, "Hiding inventory closes forge");
                Call(forge, "OnEnable");
                Call(forge, "OnEnable");
                var listeners = (Delegate)Read(user, "onEnhancementMaterialsChangedEventHandler");
                Check(listeners.GetInvocationList().Length == 1, "Repeated enable does not duplicate material subscription");
                Call(forge, "OnDisable");
                Check(Read(user, "onEnhancementMaterialsChangedEventHandler") == null, "Hiding forge releases material subscription");
                Check(original.isDirty == dirty && EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, "Original scene and Android target preserved");
                Directory.CreateDirectory("Docs/Previews/LobbyGrowth");
                File.WriteAllText("Docs/Previews/LobbyGrowth/EditChecks.txt", "Edit mode / isolated preview\nPassed=" + checks.Count + "\n" + string.Join("\n", checks));
                Debug.Log("Lobby growth Edit checks passed: " + checks.Count);
            }
            finally
            {
                singleton.SetValue(null, previousSingleton);
                UnityEngine.Random.state = random;
                SceneManager.SetActiveScene(original);
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
    }
}
