using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 격리된 마을 거래에서 확정 알림, 실패 보존, 삭제 보호와 가격/재고 테이블 반영을 검사합니다.
    /// </summary>
    public static class EquipmentShopVerification
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Three Souls/Village/Verify Equipment Shop (Edit Only)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                throw new InvalidOperationException("상점 검사는 Edit 모드 전용입니다.");
            }

            var checks = new List<string>();
            void Check(bool ok, string name)
            {
                if (ok == false)
                {
                    throw new InvalidOperationException("Shop check failed: " + name);
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
            var fixture = new GameObject("EquipmentShopFixture");
            fixture.SetActive(false);
            SceneManager.MoveGameObjectToScene(fixture, preview);
            var products = LobbyTables.Get<EquipmentShopTableDataTable>().Items;
            var price = products[0].BuyGold;
            var sellPrice = products[0].SellGold;
            var stockCount = products[0].StockCount;

            try
            {
                var game = fixture.AddComponent<GameManager>();
                var inventory = fixture.AddComponent<InventoryManager>();
                var equipment = fixture.AddComponent<EquipmentManager>();
                var growth = fixture.AddComponent<EquipmentGrowthManager>();
                var user = fixture.AddComponent<UserManager>();
                var tables = fixture.AddComponent<TableManager>();
                var assets = fixture.AddComponent<AddressableManager>();
                var shop = fixture.AddComponent<EquipmentShopManager>();
                var managers = (IDictionary)Read(game, "ManagerContainer");
                var components = new BaseManager[] { inventory, equipment, growth, user, tables, assets, shop };
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
                shop.Configure(user, inventory, equipment);
                Check(shop.TryBuy("missing", out _) == false, "Trade requires a lobby session");
                shop.BeginLobbySession();
                var offers = shop.GetOffers();
                Check(offers.Length == 8, "Eight initial actual equipment offers");
                var ids = new HashSet<string>();
                for (var i = 0; i < offers.Length; i++)
                {
                    Check(ids.Add(offers[i].Item.InstanceId) == true, "Unique offer " + i);
                }

                var offer = offers[0];
                Check(shop.GetOffers()[0].Item == offer.Item, "Repeated reads keep the same rolled equipment");
                Check(shop.TryBuy(offer.Item.InstanceId, out _) == false && user.UData.Gold == 0 &&
                    user.UData.Inventory.GetCount() == 0 && shop.GetOffers().Length == 8, "Insufficient gold preserves all state");
                user.AddCombatReward(20000, 0);
                var invEvents = 0;
                var goldEvents = 0;
                var shopEvents = 0;
                var committed = true;
                Inventory.OnInventoryChangedDelegate onInventory = () =>
                {
                    invEvents++;
                    committed &= user.UData.Gold == 20000 - offer.BuyGold &&
                        user.UData.Inventory.FindEquipment(offer.Item.InstanceId) == offer.Item &&
                        shop.FindOffer(offer.Item.InstanceId) == null;
                };
                UserManager.OnCombatRewardChangedDelegate onGold = (gold, chests) =>
                {
                    goldEvents++;
                    committed &= user.UData.Inventory.FindEquipment(offer.Item.InstanceId) == offer.Item &&
                        shop.FindOffer(offer.Item.InstanceId) == null;
                };
                EquipmentShopManager.OnShopChangedDelegate onShop = () => shopEvents++;
                user.UData.Inventory.OnInventoryChangedEventHandler += onInventory;
                user.OnCombatRewardChangedEventHandler += onGold;
                shop.OnShopChangedEventHandler += onShop;
                Check(shop.TryBuy(offer.Item.InstanceId, out _) == true, "Actual offered equipment purchased");
                Check(committed == true && invEvents == 1 && goldEvents == 1 && shopEvents == 1, "All observers see committed inventory/gold/stock exactly once");
                var afterBuyGold = user.UData.Gold;
                Check(shop.TryBuy(offer.Item.InstanceId, out _) == false && user.UData.Gold == afterBuyGold &&
                    user.UData.Inventory.GetCount() == 1 && invEvents == 1, "Repeat purchase cannot charge or clone an item");
                user.UData.Inventory.OnInventoryChangedEventHandler -= onInventory;
                user.OnCombatRewardChangedEventHandler -= onGold;
                shop.OnShopChangedEventHandler -= onShop;
                Check(equipment.TryEquip("Key_night", offer.Item.InstanceId, out _) == true, "Purchased item uses existing equipment manager");
                Check(shop.TryQuoteSale(offer.Item.InstanceId, out _, out _) == false, "Equipped item sale rejected");
                equipment.TryUnequip("Key_night", EquipSlot.Weapon);
                growth.TrySetLocked(offer.Item.InstanceId, true, out _);
                Check(shop.TrySell(offer.Item.InstanceId, sellPrice, out _) == false && user.UData.Gold == afterBuyGold, "Locked sale rejected without payment/removal");
                growth.TrySetLocked(offer.Item.InstanceId, false, out _);
                user.AddEnhancementStones(10);
                Check(growth.TryUpgrade(offer.Item.InstanceId, out _) == true, "Purchased equipment can be upgraded");
                Check(shop.TryQuoteSale(offer.Item.InstanceId, out var quoted, out _) == true && quoted == sellPrice, "Sale price is table value, no enhancement refund");
                var beforeSellGold = user.UData.Gold;
                Check(shop.TrySell(offer.Item.InstanceId, quoted + 1, out _) == false && user.UData.Gold == beforeSellGold, "Unexpected confirmation price rejected");
                products[0].SellGold++;
                Check(shop.TrySell(offer.Item.InstanceId, quoted, out _) == false && user.UData.Inventory.GetCount() == 1, "Changed price requires another confirmation");
                products[0].SellGold = sellPrice;
                products[0].SellGold = 0;
                var invalidSaleRejected = false;
                try
                {
                    shop.TrySell(offer.Item.InstanceId, 0, out _);
                }
                catch (InvalidOperationException)
                {
                    invalidSaleRejected = true;
                }

                Check(invalidSaleRejected == true && user.UData.Gold == beforeSellGold &&
                    user.UData.Inventory.FindEquipment(offer.Item.InstanceId) != null, "Invalid changed sale price fails before item removal");
                products[0].SellGold = sellPrice;
                var spare = shop.GetOffers()[0];
                Call(user.UData.Inventory, "ConfigureCapacity", 1);
                Check(shop.TryBuy(spare.Item.InstanceId, out _) == false && user.UData.Gold == beforeSellGold && shop.FindOffer(spare.Item.InstanceId) != null, "Full inventory preserves gold and offer");
                Call(user.UData.Inventory, "ConfigureCapacity", 2);
                equipment.TryCreateEquipment("Item_Armor_01", out var pending, out _);
                Call(inventory, "BeginCombatRewards");
                Call(inventory, "TryCollectCombatEquipment", pending);
                Check(shop.TryBuy(spare.Item.InstanceId, out _) == false && user.UData.Gold == beforeSellGold, "Pending dungeon reward capacity is reserved");
                Call(inventory, "CancelCombatRewards");
                user.ApplyTableRules();
                user.AddCombatReward(int.MaxValue - user.UData.Gold, 0);
                Check(shop.TrySell(offer.Item.InstanceId, quoted, out _) == false && user.UData.Gold == int.MaxValue && user.UData.Inventory.GetCount() == 1, "Wallet overflow cannot consume equipment");
                Call(user.UData, "SpendGold", int.MaxValue - beforeSellGold);
                var saleObserved = false;
                Inventory.OnInventoryChangedDelegate saleObserver = () =>
                {
                    saleObserved = user.UData.Gold == beforeSellGold + quoted &&
                        user.UData.Inventory.FindEquipment(offer.Item.InstanceId) == null;
                };
                user.UData.Inventory.OnInventoryChangedEventHandler += saleObserver;
                Check(shop.TrySell(offer.Item.InstanceId, quoted, out _) == true && saleObserved == true, "Sale confirms gold before inventory notification");
                user.UData.Inventory.OnInventoryChangedEventHandler -= saleObserver;
                Check(shop.TrySell(offer.Item.InstanceId, quoted, out _) == false && user.UData.Gold == beforeSellGold + quoted, "Repeated sale cannot duplicate gold");
                Check(shop.GetOffers().Length == 7, "Sold item does not return as restock or buyback");
                shop.EndLobbySession();
                Check(shop.TryBuy(spare.Item.InstanceId, out _) == false && shop.GetOffers().Length == 0, "Session end rejects trading and clears stock");
                products[0].BuyGold = 333;
                products[0].StockCount = 2;
                shop.BeginLobbySession();
                Check(shop.GetOffers().Length == 9 && shop.GetOffers()[0].BuyGold == 333, "Next session applies table price and stock count");
                shop.EndLobbySession();
                products[0].BuyGold = -1;
                var invalidRejected = false;
                try
                {
                    shop.BeginLobbySession();
                }
                catch (InvalidOperationException)
                {
                    invalidRejected = true;
                }

                Check(invalidRejected == true && shop.GetOffers().Length == 0, "Invalid table cannot create a partial active session");
                Check(original.isDirty == dirty && EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, "Original scene and Android target preserved");
                Directory.CreateDirectory("Docs/Previews/EquipmentShop");
                File.WriteAllText("Docs/Previews/EquipmentShop/EditChecks.txt", "Isolated Edit mode\nPassed=" + checks.Count + "\n" + string.Join("\n", checks));
                Debug.Log("Equipment shop Edit checks passed: " + checks.Count);
            }
            finally
            {
                products[0].BuyGold = price;
                products[0].SellGold = sellPrice;
                products[0].StockCount = stockCount;
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

        private static object Call(object target, string name, params object[] args)
        {
            return target.GetType().GetMethod(name, Private).Invoke(target, args);
        }
    }
}
