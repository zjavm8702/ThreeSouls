using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 구매/판매 목록과 실제 장비 상세 및 판매 확인을 표시하고 거래 요청을 전달합니다.
    /// </summary>
    public sealed class UI_EquipmentShop : MonoBehaviour
    {
        private enum ShopPage
        {
            Buy,
            Sell
        }

        [SerializeField] private UI_EquipmentShopItem ItemPrefab;
        [SerializeField] private RectTransform Content;
        [SerializeField] private UnityEngine.UI.ScrollRect Scroll;
        [SerializeField] private UI_LobbyTabs Tabs;
        [SerializeField] private UnityEngine.UI.Button BuyTab;
        [SerializeField] private UnityEngine.UI.Button SellTab;
        [SerializeField] private UnityEngine.UI.Image BuyIndicator;
        [SerializeField] private UnityEngine.UI.Image SellIndicator;
        [SerializeField] private UnityEngine.UI.Button CloseButton;
        [SerializeField] private UnityEngine.UI.Text Title;
        [SerializeField] private UnityEngine.UI.Text Wallet;
        [SerializeField] private UnityEngine.UI.Text Count;
        [SerializeField] private UnityEngine.UI.Text Empty;
        [SerializeField] private UnityEngine.UI.Image DetailIcon;
        [SerializeField] private UnityEngine.UI.Text DetailTitle;
        [SerializeField] private UnityEngine.UI.Text DetailType;
        [SerializeField] private UnityEngine.UI.Text DetailStats;
        [SerializeField] private UnityEngine.UI.Text DetailPrice;
        [SerializeField] private UnityEngine.UI.Text Message;
        [SerializeField] private UnityEngine.UI.Button TradeButton;
        [SerializeField] private UnityEngine.UI.Text TradeLabel;
        [SerializeField] private GameObject Confirmation;
        [SerializeField] private UnityEngine.UI.Text ConfirmationTitle;
        [SerializeField] private UnityEngine.UI.Button ConfirmButton;
        [SerializeField] private UnityEngine.UI.Button CancelButton;
        private readonly List<UI_EquipmentShopItem> Cells = new();
        private readonly List<EquipmentInstance> VisibleItems = new();
        private EquipmentShopManager Shop;
        private UserManager User;
        private EquipmentManager Equipment;
        private TableManager Tables;
        private EquipmentGrowthManager Growth;
        private ShopPage Page;
        private string SelectedId;
        private string PendingSaleId;
        private int PendingSaleGold;

        private void Awake()
        {
            BuyTab.onClick.AddListener(ShowBuy);
            SellTab.onClick.AddListener(ShowSell);
            CloseButton.onClick.AddListener(Close);
            TradeButton.onClick.AddListener(Trade);
            ConfirmButton.onClick.AddListener(ConfirmSale);
            CancelButton.onClick.AddListener(CancelSale);
        }

        public void SetUp(EquipmentShopManager shop, UserManager user, EquipmentManager equipment,
            TableManager tables, EquipmentGrowthManager growth, UI_LobbyTabs tabs = null)
        {
            Disconnect();
            if (tabs != null)
            {
                Tabs = tabs;
            }
            Shop = shop ?? throw new ArgumentNullException(nameof(shop));
            User = user ?? throw new ArgumentNullException(nameof(user));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            Growth = growth ?? throw new ArgumentNullException(nameof(growth));
            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        public bool IsShowing()
        {
            return gameObject.activeSelf;
        }

        public void Open()
        {
            if (Shop == null || Tabs.IsVillageShowing() == false)
            {
                throw new InvalidOperationException("장비 상점은 연결된 마을 화면에서만 열 수 있습니다.");
            }

            Page = ShopPage.Buy;
            SelectedId = null;
            Message.text = string.Empty;
            CancelSale();
            UIPanelMotion.Show(gameObject);
            Scroll.verticalNormalizedPosition = 1;
        }

        public void Close()
        {
            CancelSale();
            UIPanelMotion.Hide(gameObject);
        }

        private void Connect()
        {
            Refresh();
            User.UData.Inventory.OnInventoryChangedEventHandler += Refresh;
            User.OnCombatRewardChangedEventHandler += OnGoldChanged;
            Equipment.OnEquipmentChangedEventHandler += OnEquipmentChanged;
            Growth.OnEquipmentGrowthChangedEventHandler += Refresh;
            Tabs.OnTabChangedEventHandler += OnTabChanged;
        }

        private void Disconnect()
        {
            if (User != null)
            {
                User.UData.Inventory.OnInventoryChangedEventHandler -= Refresh;
                User.OnCombatRewardChangedEventHandler -= OnGoldChanged;
                Equipment.OnEquipmentChangedEventHandler -= OnEquipmentChanged;
                Growth.OnEquipmentGrowthChangedEventHandler -= Refresh;
                Tabs.OnTabChangedEventHandler -= OnTabChanged;
            }
        }

        private void OnGoldChanged(int gold, int chestCount)
        {
            Refresh();
        }

        private void OnEquipmentChanged(string heroKey)
        {
            Refresh();
        }

        private void OnTabChanged(LobbyTab tab)
        {
            if (tab != LobbyTab.Village)
            {
                Close();
            }
        }

        private void ShowBuy()
        {
            SetPage(ShopPage.Buy);
        }

        private void ShowSell()
        {
            SetPage(ShopPage.Sell);
        }

        private void SetPage(ShopPage page)
        {
            Page = page;
            SelectedId = null;
            CancelSale();
            Message.text = string.Empty;
            Refresh();
            Scroll.verticalNormalizedPosition = 1;
        }

        public void SelectItem(string instanceId)
        {
            SelectedId = instanceId;
            Message.text = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            VisibleItems.Clear();
            if (Page == ShopPage.Buy)
            {
                var offers = Shop.GetOffers();
                for (var i = 0; i < offers.Length; i++)
                {
                    VisibleItems.Add(offers[i].Item);
                }
            }
            else
            {
                VisibleItems.AddRange(User.UData.Inventory.GetItems());
            }

            if (VisibleItems.Exists(item => item.InstanceId == SelectedId) == false)
            {
                SelectedId = VisibleItems.Count == 0 ? null : VisibleItems[0].InstanceId;
            }

            while (Cells.Count < VisibleItems.Count)
            {
                Cells.Add(Instantiate(ItemPrefab, Content));
            }

            for (var i = 0; i < Cells.Count; i++)
            {
                var active = i < VisibleItems.Count;
                Cells[i].gameObject.SetActive(active);
                if (active == false)
                {
                    continue;
                }

                var item = VisibleItems[i];
                var price = Page == ShopPage.Buy ? $"{Shop.FindOffer(item.InstanceId).BuyGold:N0} G"
                    : item.Growth.Locked == true ? "잠금" : Equipment.GetOwner(item.InstanceId) != null ? "장착 중"
                    : EquipmentShopCatalog.FindProduct(item.ItemKey) is { } product ? $"{product.SellGold:N0} G" : "취급 안 함";
                Cells[i].Show(item, GetItemName(item), price, item.InstanceId == SelectedId, SelectItem);
            }

            Title.text = EquipmentShopCatalog.GetVillageShop().DisplayName;
            Wallet.text = $"보유 골드  {User.UData.Gold:N0} G";
            Count.text = Page == ShopPage.Buy ? $"진열 {VisibleItems.Count}개  ·  구입한 장비는 인벤토리로 이동합니다"
                : $"보유 {User.UData.Inventory.GetCount()} / {User.UData.Inventory.Capacity}  ·  잠금/장착 장비는 판매 불가";
            Empty.text = VisibleItems.Count == 0 ? Page == ShopPage.Buy ? "진열된 장비를 모두 구매했습니다." : "보유 장비가 없습니다." : string.Empty;
            BuyIndicator.color = Page == ShopPage.Buy ? new Color32(226, 183, 105, 255) : new Color32(53, 66, 76, 255);
            SellIndicator.color = Page == ShopPage.Sell ? new Color32(226, 183, 105, 255) : new Color32(53, 66, 76, 255);
            ShowDetail();
        }

        private string GetItemName(EquipmentInstance item)
        {
            return Tables.GetEquipmentName(item.ItemKey) + (item.Growth.Level > 0 ? " +" + item.Growth.Level : string.Empty);
        }

        private void ShowDetail()
        {
            var item = VisibleItems.Find(value => value.InstanceId == SelectedId);
            TradeLabel.text = Page == ShopPage.Buy ? "장비 구매" : "장비 판매";
            TradeButton.interactable = false;
            DetailIcon.enabled = item != null;
            if (item == null)
            {
                DetailTitle.text = "장비를 선택하세요";
                DetailType.text = string.Empty;
                DetailStats.text = string.Empty;
                DetailPrice.text = string.Empty;
                return;
            }

            var row = Tables.GetEquipmentTableData(item.ItemKey);
            DetailIcon.sprite = LobbyPresentationData.GetItemIcon(item.ItemKey);
            DetailTitle.text = GetItemName(item);
            DetailTitle.color = LobbyPresentationData.GetRarityColor(row.Rarity);
            var heroes = new List<string>();
            for (var i = 0; i < 3; i++)
            {
                if (Array.IndexOf(row.AllowedCharacterKeys, LobbyPresentationData.GetHeroKey(i)) >= 0)
                {
                    heroes.Add(LobbyPresentationData.GetHeroName(i));
                }
            }
            DetailType.text = LobbyPresentationData.GetRarityName(row.Rarity) + " · " + LobbyPresentationData.GetSlotName(row.EquipSlot)
                + " · " + (heroes.Count == 0 ? "모든 영웅" : string.Join(", ", heroes));
            var values = Equipment.GetItemBonuses(item);
            DetailStats.text = $"공격력  +{values.AttackDmg:0.###}     체력  +{values.MaxHP:0.###}\n공격속도  +{values.AttackSpeedBonus:P1}";
            if (Page == ShopPage.Buy)
            {
                var price = Shop.FindOffer(item.InstanceId).BuyGold;
                TradeButton.interactable = Shop.CanBuy(item.InstanceId, out var error);
                DetailPrice.text = $"구매 가격  {price:N0} G\n" + (error ?? "표시된 능력치 그대로 구매합니다.");
            }
            else
            {
                TradeButton.interactable = Shop.TryQuoteSale(item.InstanceId, out var price, out var error);
                DetailPrice.text = error ?? $"판매 가격  {price:N0} G\n강화 비용은 별도로 돌려받지 않습니다.";
            }
        }

        private void Trade()
        {
            if (Page == ShopPage.Buy)
            {
                var result = Shop.TryBuy(SelectedId, out var error);
                Refresh();
                Message.text = result == true ? "장비를 구매했습니다. 인벤토리에서 장착할 수 있습니다." : error;
                return;
            }

            if (Shop.TryQuoteSale(SelectedId, out var gold, out var message) == false)
            {
                Message.text = message;
                return;
            }

            PendingSaleId = SelectedId;
            PendingSaleGold = gold;
            var item = User.UData.Inventory.FindEquipment(PendingSaleId);
            ConfirmationTitle.text = $"{GetItemName(item)}\n{gold:N0} 골드에 판매할까요?\n판매한 장비는 되돌릴 수 없습니다.";
            UIPanelMotion.Show(Confirmation);
        }

        private void ConfirmSale()
        {
            var id = PendingSaleId;
            var gold = PendingSaleGold;
            CancelSale();
            if (id == null)
            {
                return;
            }

            var result = Shop.TrySell(id, gold, out var error);
            Refresh();
            Message.text = result == true ? $"장비를 판매하고 {gold:N0} 골드를 받았습니다." : error;
        }

        private void CancelSale()
        {
            PendingSaleId = null;
            PendingSaleGold = 0;
            UIPanelMotion.Hide(Confirmation);
        }

        private void OnEnable()
        {
            if (Shop != null)
            {
                Connect();
            }
        }

        private void OnDisable()
        {
            Disconnect();
            CancelSale();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
