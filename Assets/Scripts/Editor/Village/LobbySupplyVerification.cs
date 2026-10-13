using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 격리 Edit 씬에서 구매의 원자성, 편성 한도와 공급품 UI의 구독 수명을 검사합니다.
    /// </summary>
    public static class LobbySupplyVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Three Souls/Village/Verify Lobby Supplies (Edit Only)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                throw new InvalidOperationException("공급품 격리 검사는 Edit 모드 전용입니다.");
            }

            var original = SceneManager.GetActiveScene();
            var dirty = original.isDirty;
            var preview = EditorSceneManager.NewPreviewScene();
            var checks = new List<string>();
            void Check(bool condition, string label)
            {
                if (condition == false)
                {
                    throw new InvalidOperationException("Supply check failed: " + label);
                }

                checks.Add(label);
            }

            var fixture = new GameObject("SupplyFixture");
            fixture.SetActive(false);
            SceneManager.MoveGameObjectToScene(fixture, preview);

            try
            {
                var user = fixture.AddComponent<UserManager>();
                var manager = fixture.AddComponent<LobbySupplyManager>();
                Call(manager, "Configure", user);
                var data = user.UData;
                var items = data.Consumables;
                var slots = data.Supplies;
                var small = ConsumableType.SmallHealingPotion;
                var party = ConsumableType.PartyHealingPotion;
                Check(slots.GetTotalAssigned() == 0 && slots.Threshold == AutoHealThreshold.At30Percent, "Default preparation is empty / HP30");
                Check(manager.TryBuy(small, 1, out _) == false, "Purchase rejects outside lobby");
                manager.BeginLobbySession();
                Check(manager.TryBuy(small, 1, out _) == false && items.GetOwned(small) == 0, "No gold cannot grant potion");
                user.AddCombatReward(80, 0);
                var events = 0;
                LobbySupplyManager.OnPreparationChangedDelegate listener = () => events++;
                manager.OnPreparationChangedEventHandler += listener;
                manager.OnPreparationChangedEventHandler += listener;
                var atomic = false;
                UserManager.OnCombatRewardChangedDelegate currency = (gold, chest) => atomic = gold == 0 && items.GetOwned(small) == 1;
                user.OnCombatRewardChangedEventHandler += currency;
                Check(manager.TryBuy(small, 1, out _) == true && atomic == true && events == 1, "Exact funds purchase commits both states before deduplicated notifications");
                user.OnCombatRewardChangedEventHandler -= currency;
                Check(manager.TryBuy(party, 1, out _) == false && events == 1, "Failed purchase makes no notification");
                user.AddCombatReward(10000, 0);
                var beforeGold = data.Gold;
                Check(manager.TryBuy(party, 5, out _) == true && items.GetOwned(party) == 5 && data.Gold == beforeGold - 900, "Party bulk price is 180 each");
                Check(manager.TryBuy(ConsumableType.None, 1, out _) == false
                    && manager.TryBuy(small, 0, out _) == false && manager.TryBuy(small, -1, out _) == false
                    && manager.TryBuy(small, int.MaxValue, out _) == false, "Invalid type / quantities rejected");
                Check(data.Gold == beforeGold - 900 && items.GetOwned(small) == 1, "Invalid requests preserve both wallet and inventory");
                Check(manager.TrySetSlot(0, small, 1, out _) == true && items.GetOwned(small) == 1, "Preparation never consumes owned potion");
                Check(manager.TrySetSlot(1, small, 1, out _) == false && slots.GetSlot(1).Count == 0, "Combined allocation cannot exceed ownership");
                manager.TryBuy(small, 5, out _);
                Check(manager.TrySetSlot(0, small, 2, out _) == true && manager.TrySetSlot(1, small, 2, out _) == true
                    && slots.GetAssigned(small) == 4 && slots.GetTotalAssigned() == 4, "Same type in two slots / four maximum");
                var beforeEvents = events;
                Check(manager.TrySetSlot(0, small, 3, out _) == false && manager.TrySetSlot(-1, small, 1, out _) == false
                    && manager.TrySetSlot(2, small, 1, out _) == false && manager.TrySetSlot(0, small, -1, out _) == false
                    && manager.TrySetSlot(0, ConsumableType.None, 1, out _) == false
                    && manager.TrySetSlot(0, (ConsumableType)99, 0, out _) == false && events == beforeEvents, "Invalid slots, types and counts have no side effects");
                Check(manager.TrySetSlot(0, party, 2, out _) == true && slots.GetAssigned(small) == 2
                    && slots.GetAssigned(party) == 2, "Changing type releases old draft allocation");
                beforeEvents = events;
                Check(manager.TrySetSlot(0, party, 2, out _) == true && events == beforeEvents, "Unchanged preparation emits no event");
                Check(manager.TrySetSlot(0, party, 0, out _) == true && slots.GetSlot(0).Type == ConsumableType.None
                    && items.GetOwned(party) == 5, "Zero normalizes to empty without refund duplication");
                var thresholds = new[] { AutoHealThreshold.Disabled, AutoHealThreshold.At20Percent, AutoHealThreshold.At30Percent, AutoHealThreshold.At40Percent };

                for (var i = 0; i < thresholds.Length; i++)
                {
                    Check(manager.TrySetAutoHeal(thresholds[i], out _) == true && slots.Threshold == thresholds[i], "Auto heal option " + thresholds[i]);
                }

                beforeEvents = events;
                Check(manager.TrySetAutoHeal((AutoHealThreshold)99, out _) == false && events == beforeEvents, "Invalid heal threshold rejected");
                Check(manager.TryClearSlots(out _) == true && slots.GetTotalAssigned() == 0
                    && items.GetOwned(small) == 6 && items.GetOwned(party) == 5, "Clear keeps purchased supplies and heal setting");
                beforeEvents = events;
                Check(manager.TryClearSlots(out _) == true && events == beforeEvents, "Repeated clear does not notify");
                Call(manager, "GrantStartingPotions", small, int.MaxValue - 6);
                beforeGold = data.Gold;
                Check(manager.TryBuy(small, 1, out _) == false && data.Gold == beforeGold && items.GetOwned(small) == int.MaxValue, "Inventory overflow cannot charge gold");
                manager.EndLobbySession();
                Check(manager.TrySetSlot(0, party, 1, out _) == false && manager.TrySetAutoHeal(AutoHealThreshold.Disabled, out _) == false
                    && manager.TryClearSlots(out _) == false, "All preparation changes reject outside lobby");
                manager.BeginLobbySession();
                manager.OnPreparationChangedEventHandler -= listener;

                var ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VillageLobbyUIBuilder.PrefabPath));
                SceneManager.MoveGameObjectToScene(ui, preview);
                var tabs = ui.GetComponentInChildren<UI_LobbyTabs>(true);
                var panel = ui.GetComponentInChildren<UI_LobbySupplies>(true);
                Call(tabs, "Awake");
                Call(panel, "Awake");
                panel.SetUp(manager, user);
                tabs.SelectTab(LobbyTab.Supplies);
                Call(panel, "OnEnable");
                Check(panel.gameObject.activeSelf == true && tabs.IsVillageShowing() == false, "Fourth tab shows supplies and gates village input");
                Check(Read<Text>(panel, "Wallet").text.Contains(data.Gold.ToString("N0")), "Initial wallet rendered");
                beforeGold = data.Gold;
                Read<Button[]>(panel, "BuyOne")[1].onClick.Invoke();
                Check(data.Gold == beforeGold - 180 && items.GetOwned(party) == 6
                    && Read<Text[]>(panel, "Owned")[1].text.Contains("6"), "UI purchase refreshes owned amount");
                Read<Button[]>(panel, "TypeButtons")[1].onClick.Invoke();
                Read<Button>(panel, "Increase").onClick.Invoke();
                Check(slots.GetSlot(0).Type == party && slots.GetSlot(0).Count == 2
                    && Read<Button>(panel, "Increase").interactable == false, "UI quantity respects slot maximum");
                Read<Button[]>(panel, "SlotButtons")[1].onClick.Invoke();
                Read<Button[]>(panel, "TypeButtons")[1].onClick.Invoke();
                Check(slots.GetSlot(1).Type == party && slots.GetSlot(1).Count == 1, "Second slot selection affects only second slot");
                Read<Button>(panel, "Decrease").onClick.Invoke();
                Check(slots.GetSlot(1).Type == ConsumableType.None && Read<Button>(panel, "Increase").interactable == false, "UI decrement to zero clears selected slot");
                Read<Button[]>(panel, "ThresholdButtons")[0].onClick.Invoke();
                Check(slots.Threshold == AutoHealThreshold.Disabled && Read<Text>(panel, "Summary").text.Contains("사용 안 함"), "UI disabled auto heal appears in summary");
                Read<Button>(panel, "ClearSlots").onClick.Invoke();
                Check(slots.GetTotalAssigned() == 0 && Read<Button>(panel, "ClearSlots").interactable == false, "UI clear is idempotent and disabled when empty");
                Call(panel, "OnDisable");
                Check(Read(manager, "onPreparationChangedEventHandler") == null, "Hidden page unsubscribes");
                var shown = Read<Text>(panel, "Wallet").text;
                user.AddCombatReward(100, 0);
                Check(Read<Text>(panel, "Wallet").text == shown, "Hidden page does not observe currency");
                Call(panel, "OnEnable");
                Call(panel, "OnEnable");
                Check(((Delegate)Read(manager, "onPreparationChangedEventHandler")).GetInvocationList().Length == 1
                    && Read<Text>(panel, "Wallet").text.Contains(data.Gold.ToString("N0")), "Reopen applies current state with one subscription");
                Check(Read<Button[]>(panel, "BuyOne")[0].interactable == false, "UI disables overflow purchase");
                Check(original.isDirty == dirty && EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, "Original scene and Android preserved");
                Directory.CreateDirectory("Docs/Previews/LobbySupplies");
                File.WriteAllText("Docs/Previews/LobbySupplies/EditChecks.txt", "Isolated Edit preview\nPassed=" + checks.Count + "\n" + string.Join("\n", checks));
                Debug.Log("Lobby supplies Edit checks passed: " + checks.Count);
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static object Read(object target, string field)
        {
            return target.GetType().GetField(field, Private).GetValue(target);
        }

        private static T Read<T>(object target, string field)
        {
            return (T)Read(target, field);
        }

        private static object Call(object target, string method, params object[] args)
        {
            return target.GetType().GetMethod(method, Private).Invoke(target, args);
        }
    }
}
