using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using ThreeSouls.Village;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 실제 Play의 Update 이동, 화면 입력 투영, 버튼 거래와 UI 구독 수명을 단계별로 검사합니다.
    /// </summary>
    public static class EquipmentShopPlayVerification
    {
        private static readonly List<string> Checks = new();
        private static VillagePartyController Party;
        private static VillageMoveInput Input;
        private static VillageShopInteraction Interaction;
        private static VillageShopTarget Target;
        private static UI_EquipmentShop View;
        private static UI_LobbyTabs Tabs;
        private static EquipmentShopManager Shop;
        private static UserManager User;
        private static int Step;
        private static double Deadline;
        private static bool PreviousBackground;
        private static string SaleItemId;
        private static EquipmentShopOffer PurchasedOffer;
        private static int GoldBeforePurchase;
        private static int SalePrice;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Start()
        {
            if (EditorApplication.isPlaying == false)
            {
                throw new InvalidOperationException("실제 Village Play에서 실행하세요.");
            }

            EditorApplication.update -= Advance;
            Checks.Clear();
            Party = UnityEngine.Object.FindFirstObjectByType<VillagePartyController>();
            Input = Party.GetComponent<VillageMoveInput>();
            Interaction = Party.GetComponent<VillageShopInteraction>();
            Target = UnityEngine.Object.FindFirstObjectByType<VillageShopTarget>();
            View = UnityEngine.Object.FindObjectsByType<UI_EquipmentShop>(FindObjectsInactive.Include).Single();
            Tabs = UnityEngine.Object.FindFirstObjectByType<UI_LobbyTabs>();
            Shop = EquipmentShopManager.Get();
            User = UserManager.Get();
            PreviousBackground = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                Check(Shop.GetOffers().Length == 8 && User.UData.Inventory.GetCount() == 24 && User.UData.Gold == 20000, "Play initializes eight offers and preserves existing seed");
                Check(View.IsShowing() == false && Tabs.IsVillageShowing() == true, "Village starts with shop closed");
                ClickWorld(new Vector2(-3.5f, -3.25f));
                Check(Party.IsMoving() == true, "Screen-projected floor input starts existing party movement");
                Step = 0;
                Deadline = EditorApplication.timeSinceStartup + 25;
                EditorApplication.update += Advance;
            }
            catch
            {
                Finish();
                throw;
            }
        }

        private static void Advance()
        {
            try
            {
                if (EditorApplication.isPlaying == false || EditorApplication.timeSinceStartup > Deadline)
                {
                    throw new InvalidOperationException("Shop Play step timed out or Play ended: " + Step);
                }

                if (Party.IsMoving() == true)
                {
                    return;
                }

                if (Step == 0)
                {
                    Check(Vector2.Distance(Party.GetLeaderPosition(), new Vector2(-3.5f, -3.25f)) < 0.02f, "Actual Update reaches floor click");
                    Interaction.TryHandleWorldClick(new Vector2(-13, -2));
                    Check((bool)Read(Interaction, "PendingOpen") == true && Party.IsMoving() == true, "Building selection queues entrance approach");
                    ClickWorld(Party.GetLeaderPosition() + Vector2.down * 0.5f);
                    Check((bool)Read(Interaction, "PendingOpen") == false && View.IsShowing() == false, "Another floor input cancels queued shop opening");
                    Step = 1;
                }
                else if (Step == 1)
                {
                    Interaction.TryHandleWorldClick(new Vector2(-13, -2));
                    Tabs.SelectTab(LobbyTab.Inventory);
                    Check((bool)Read(Interaction, "PendingOpen") == false && Party.IsMoving() == false, "Changing tab cancels approach and stops its route");
                    Tabs.SelectTab(LobbyTab.Village);
                    Check(Party.TryMoveTo(new Vector2(-13, -7.5f)) == true, "Controlled setup finds an unobstructed path near the shop");
                    Step = 2;
                }
                else if (Step == 2)
                {
                    Check(Vector2.Distance(Party.GetLeaderPosition(), new Vector2(-13, -7.5f)) < 0.02f, "Actual Update moves party to the shop area");
                    Check(Target.ContainsPoint(new Vector2(-13, -5.5f)) == true && Target.ContainsPoint(new Vector2(-8.2f, -5.7f)) == true, "Building and sign selection areas are connected");
                    ClickWorld(new Vector2(-13, -5.5f));
                    Check((bool)Read(Interaction, "PendingOpen") == true, "Visible building screen input queues shop opening");
                    Step = 3;
                }
                else if (Step == 3)
                {
                    if (View.IsShowing() == false)
                    {
                        return;
                    }

                    Check(Vector2.Distance(Party.GetLeaderPosition(), Target.GetEntrancePosition()) <= EquipmentShopCatalog.GetVillageShop().InteractRadius,
                        "Shop opens only after actual entrance arrival");
                    Check(Interaction.IsInputBlocked() == true && Party.IsMoving() == false, "Shop blocks world input while party is idle");
                    Check(Read<UnityEngine.UI.Text>(View, "Wallet").text.Contains("20,000"), "Shop displays live wallet");
                    Check(Hits(Read<UnityEngine.UI.Button>(View, "TradeButton")), "Purchase button receives real Canvas raycast");
                    var selected = (string)Read(View, "SelectedId");
                    var offer = Shop.FindOffer(selected);
                    var beforeGold = User.UData.Gold;
                    Read<UnityEngine.UI.Button>(View, "TradeButton").onClick.Invoke();
                    Check(User.UData.Gold == beforeGold - offer.BuyGold && User.UData.Inventory.GetCount() == 25, "Actual purchase button transfers gold and item");
                    Check(User.UData.Inventory.FindEquipment(selected) == offer.Item && Shop.GetOffers().Length == 7, "Purchased roll matches preview and disappears from stock");
                    Read<UnityEngine.UI.Button>(View, "SellTab").onClick.Invoke();
                    View.SelectItem(User.UData.Inventory.GetItems()[0].InstanceId);
                    Check(Read<UnityEngine.UI.Button>(View, "TradeButton").interactable == false, "Equipped item sale button is disabled");
                    View.SelectItem(selected);
                    Check(Read<UnityEngine.UI.Button>(View, "TradeButton").interactable == true, "Purchased unequipped item can be sold");
                    var saleGold = EquipmentShopCatalog.FindProduct(offer.Item.ItemKey).SellGold;
                    Read<UnityEngine.UI.Button>(View, "TradeButton").onClick.Invoke();
                    Check(Read<GameObject>(View, "Confirmation").activeSelf == true && User.UData.Inventory.GetCount() == 25, "Sale awaits explicit confirmation");
                    SaleItemId = selected;
                    PurchasedOffer = offer;
                    GoldBeforePurchase = beforeGold;
                    SalePrice = saleGold;
                    Step = 4;
                }
                else if (Step == 4)
                {
                    var selected = SaleItemId;
                    var offer = PurchasedOffer;
                    var beforeGold = GoldBeforePurchase;
                    var saleGold = SalePrice;
                    Check(Hits(Read<UnityEngine.UI.Button>(View, "ConfirmButton")), "Confirmation receives topmost UI raycast");
                    Read<UnityEngine.UI.Button>(View, "CancelButton").onClick.Invoke();
                    Check(Read<GameObject>(View, "Confirmation").activeSelf == false && User.UData.Inventory.GetCount() == 25 && User.UData.Gold == beforeGold - offer.BuyGold, "Cancel preserves item and wallet");
                    Read<UnityEngine.UI.Button>(View, "TradeButton").onClick.Invoke();
                    Read<UnityEngine.UI.Button>(View, "ConfirmButton").onClick.Invoke();
                    Check(User.UData.Inventory.FindEquipment(selected) == null && User.UData.Gold == beforeGold - offer.BuyGold + saleGold, "Confirmed sale removes exactly selected item and credits table price");
                    Read<UnityEngine.UI.Button>(View, "ConfirmButton").onClick.Invoke();
                    Check(User.UData.Gold == beforeGold - offer.BuyGold + saleGold, "Repeated confirm cannot duplicate proceeds");
                    var count = User.UData.Inventory.GetCount();
                    Check(Read<UnityEngine.UI.Text>(View, "Count").text.Contains(count.ToString()) && Read<UnityEngine.UI.Text>(View, "Wallet").text.Contains(User.UData.Gold.ToString("N0")), "List and wallet refresh after committed sale");
                    var stock = Shop.GetOffers()[0].Item;
                    View.Close();
                    Check(CountListeners(User.UData.Inventory, "onInventoryChangedEventHandler", View) == 0 && CountListeners(Tabs, "onTabChangedEventHandler", View) == 0, "Closing shop unsubscribes inventory and tabs");
                    Interaction.TryHandleWorldClick(new Vector2(-13, -5.5f));
                    Check(View.IsShowing() == true && Shop.GetOffers().Length == 7 && Shop.GetOffers()[0].Item == stock, "Reopening nearby preserves stock and rolled stats");
                    Check(CountListeners(User.UData.Inventory, "onInventoryChangedEventHandler", View) == 1, "Reopen creates one inventory subscription");
                    Tabs.SelectTab(LobbyTab.Characters);
                    Check(View.IsShowing() == false && Read<GameObject>(View, "Confirmation").activeSelf == false, "Tab change closes shop and confirmation");
                    Tabs.SelectTab(LobbyTab.Village);
                    Check(Interaction.IsInputBlocked() == false, "Returning to Village restores world input");
                    Party.gameObject.SetActive(false);
                    Check(CountListeners(Tabs, "onTabChangedEventHandler", Interaction) == 0, "Disabled interaction releases tab subscription");
                    Party.gameObject.SetActive(true);
                    Check(CountListeners(Tabs, "onTabChangedEventHandler", Interaction) == 1, "Reenabled interaction subscribes once");
                    Interaction.TryHandleWorldClick(new Vector2(-13, -5.5f));
                    Check(View.IsShowing() == true, "Interaction still opens after disable and reenable");
                    Step = 5;
                    Directory.CreateDirectory("Docs/Previews/EquipmentShop");
                    File.WriteAllText("Docs/Previews/EquipmentShop/PlayChecks.txt", "Actual Play; Update movement; projected input; invoked button callbacks\nPassed=" + Checks.Count + "\n" + string.Join("\n", Checks));
                    Debug.Log("Equipment shop Play checks passed: " + Checks.Count);
                    Finish();
                }
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("CodexBackups/VillageEquipmentShop_20261009");
                File.WriteAllText("CodexBackups/VillageEquipmentShop_20261009/PlayFailure.txt", error.ToString() + "\n" + string.Join("\n", Checks));
                Debug.LogException(error);
                Finish();
            }
        }

        private static void ClickWorld(Vector2 position)
        {
            var screen = (Vector2)Camera.main.WorldToScreenPoint(position);
            Check(screen.x > 0 && screen.x < Screen.width && screen.y > 0 && screen.y < Screen.height, "Requested screen input lies inside the Game view");
            Input.GetType().GetMethod("RequestMove", Private).Invoke(Input, new object[] { screen, -1 });
        }

        private static bool Hits(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            var results = new List<RaycastResult>();
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
            };
            EventSystem.current.RaycastAll(data, results);
            return results.Count > 0 && results[0].gameObject == button.gameObject;
        }

        private static int CountListeners(object source, string field, object target)
        {
            var listeners = (Delegate)Read(source, field);
            return listeners == null ? 0 : listeners.GetInvocationList().Count(callback => callback.Target == target);
        }

        private static void Check(bool result, string name)
        {
            if (result == false)
            {
                throw new InvalidOperationException("Shop Play check failed: " + name);
            }

            Checks.Add(name);
        }

        private static object Read(object target, string name)
        {
            return target.GetType().GetField(name, Private).GetValue(target);
        }

        private static T Read<T>(object target, string name)
        {
            return (T)Read(target, name);
        }

        private static void Finish()
        {
            EditorApplication.update -= Advance;
            Application.runInBackground = PreviousBackground;
        }
    }
}
