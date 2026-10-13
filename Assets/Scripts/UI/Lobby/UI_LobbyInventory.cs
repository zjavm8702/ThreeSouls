using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 인벤토리의 선택, 필터, 정렬과 상세 표시를 소유하며 변경 요청은 Manager에 전달합니다.
    /// </summary>
    public sealed class UI_LobbyInventory : MonoBehaviour
    {
        private enum ItemFilter
        {
            All,
            Weapon,
            Armor,
            Accessory,
            Equipped
        }

        private enum ItemOrder
        {
            Rarity,
            Name,
            Attack
        }

        [SerializeField] private UI_LobbyItem ItemPrefab;
        [SerializeField] private RectTransform Content;
        [SerializeField] private ScrollRect Scroll;
        [SerializeField] private Button[] HeroButtons;
        [SerializeField] private Image[] HeroIndicators;
        [SerializeField] private Button[] FilterButtons;
        [SerializeField] private Image[] FilterIndicators;
        [SerializeField] private Button OrderButton;
        [SerializeField] private Text OrderLabel;
        [SerializeField] private Text Count;
        [SerializeField] private Text Empty;
        [SerializeField] private Image DetailIcon;
        [SerializeField] private Text DetailTitle;
        [SerializeField] private Text DetailType;
        [SerializeField] private Text DetailStats;
        [SerializeField] private Text Comparison;
        [SerializeField] private Text Message;
        [SerializeField] private Button EquipButton;
        [SerializeField] private Text EquipLabel;
        [SerializeField] private Button DiscardButton;
        [SerializeField] private GameObject DiscardDialog;
        [SerializeField] private Text DiscardTitle;
        [SerializeField] private Button ConfirmDiscard;
        [SerializeField] private Button CancelDiscard;
        private readonly List<UI_LobbyItem> Cells = new();
        private readonly List<EquipmentInstance> VisibleItems = new();
        private UserData User;
        private EquipmentManager Equipment;
        private InventoryManager Inventory;
        private TableManager Tables;
        private EquipmentGrowthManager Growth;
        private int SelectedHero;
        private string SelectedItem;
        private string PendingDiscard;
        private ItemFilter Filter;
        private ItemOrder Order;

        private void Awake()
        {
            for (var i = 0; i < HeroButtons.Length; i++)
            {
                var index = i;
                HeroButtons[i].onClick.AddListener(() => SelectHero(index));
            }

            for (var i = 0; i < FilterButtons.Length; i++)
            {
                var filter = (ItemFilter)i;
                FilterButtons[i].onClick.AddListener(() => SelectFilter(filter));
            }

            OrderButton.onClick.AddListener(ChangeOrder);
            EquipButton.onClick.AddListener(EquipSelected);
            DiscardButton.onClick.AddListener(RequestDiscard);
            ConfirmDiscard.onClick.AddListener(DiscardSelected);
            CancelDiscard.onClick.AddListener(CloseDiscard);
        }

        public void SetUp(UserData user, InventoryManager inventory,
            EquipmentManager equipment, TableManager tables, EquipmentGrowthManager growth)
        {
            Disconnect();
            User = user ?? throw new ArgumentNullException(nameof(user));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            Growth = growth ?? throw new ArgumentNullException(nameof(growth));

            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        private void Connect()
        {
            Refresh();
            User.Inventory.OnInventoryChangedEventHandler += Refresh;
            Equipment.OnEquipmentChangedEventHandler += OnEquipmentChanged;
            Growth.OnEquipmentGrowthChangedEventHandler += Refresh;
        }

        private void Disconnect()
        {
            if (User != null)
            {
                User.Inventory.OnInventoryChangedEventHandler -= Refresh;
                Equipment.OnEquipmentChangedEventHandler -= OnEquipmentChanged;
                Growth.OnEquipmentGrowthChangedEventHandler -= Refresh;
            }
        }

        public void SelectItem(string instanceId)
        {
            SelectedItem = instanceId;
            CloseDiscard();
            Message.text = "선택한 영웅의 장비와 비교합니다.";
            Refresh();
        }

        public EquipmentInstance GetSelectedItem()
        {
            return User.Inventory.FindEquipment(SelectedItem);
        }

        private void SelectHero(int index)
        {
            SelectedHero = index;
            CloseDiscard();
            Refresh();
        }

        private void SelectFilter(ItemFilter filter)
        {
            Filter = filter;
            CloseDiscard();
            Refresh();
            Scroll.verticalNormalizedPosition = 1;
        }

        private void ChangeOrder()
        {
            switch (Order)
            {
                case ItemOrder.Rarity: Order = ItemOrder.Name; break;
                case ItemOrder.Name: Order = ItemOrder.Attack; break;
                case ItemOrder.Attack: Order = ItemOrder.Rarity; break;
            }

            Refresh();
            Scroll.verticalNormalizedPosition = 1;
        }

        private bool MatchesFilter(EquipmentInstance item)
        {
            var definition = Tables.GetEquipmentTableData(item.ItemKey);
            var hero = LobbyPresentationData.GetHeroKey(SelectedHero);
            var owner = Equipment.GetOwner(item.InstanceId);
            if (!EquipmentRules.CanEquip(definition, Tables.GetCharacterTableDataTable(hero)) || owner != null && owner != hero) return false;
            var slot = definition.EquipSlot;

            switch (Filter)
            {
                case ItemFilter.All: return true;
                case ItemFilter.Weapon: return slot == EquipSlot.Weapon;
                case ItemFilter.Armor: return slot == EquipSlot.Armor;
                case ItemFilter.Accessory: return slot == EquipSlot.Accessory;
                case ItemFilter.Equipped: return Equipment.GetOwner(item.InstanceId) != null;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private int CompareItems(EquipmentInstance left, EquipmentInstance right)
        {
            var result = 0;

            switch (Order)
            {
                case ItemOrder.Rarity:
                    result = LobbyTables.GetOption("Rarity", Tables.GetEquipmentTableData(right.ItemKey).Rarity.ToString()).DisplayOrder
                        .CompareTo(LobbyTables.GetOption("Rarity", Tables.GetEquipmentTableData(left.ItemKey).Rarity.ToString()).DisplayOrder);
                    break;
                case ItemOrder.Name:
                    result = string.Compare(Tables.GetEquipmentName(left.ItemKey),
                        Tables.GetEquipmentName(right.ItemKey), StringComparison.Ordinal);
                    break;
                case ItemOrder.Attack:
                    result = Equipment.GetItemBonuses(right).AttackDmg.CompareTo(Equipment.GetItemBonuses(left).AttackDmg);
                    break;
            }

            return result == 0 ? string.Compare(left.InstanceId, right.InstanceId, StringComparison.Ordinal) : result;
        }

        private void Refresh()
        {
            CloseDiscard();
            VisibleItems.Clear();
            var items = User.Inventory.GetItems();

            for (var i = 0; i < items.Length; i++)
            {
                if (MatchesFilter(items[i]) == true)
                {
                    VisibleItems.Add(items[i]);
                }
            }

            VisibleItems.Sort(CompareItems);
            var selectedIndex = VisibleItems.FindIndex(item => item.InstanceId == SelectedItem);

            if (selectedIndex < 0)
            {
                SelectedItem = VisibleItems.Count == 0 ? null : VisibleItems[0].InstanceId;
            }

            var cellCount = Math.Max(20, ((VisibleItems.Count + 4) / 5) * 5);

            while (Cells.Count < cellCount)
            {
                Cells.Add(Instantiate(ItemPrefab, Content));
            }

            for (var i = 0; i < Cells.Count; i++)
            {
                Cells[i].gameObject.SetActive(i < cellCount);

                if (i >= cellCount)
                {
                    continue;
                }

                var item = i < VisibleItems.Count ? VisibleItems[i] : null;
                var row = item == null ? null : Tables.GetEquipmentTableData(item.ItemKey);
                Cells[i].Show(this, item, row,
                    item == null ? string.Empty : Tables.GetEquipmentName(item.ItemKey)
                        + (item.Growth.Level > 0 ? " +" + item.Growth.Level : string.Empty),
                    item == null ? null : Equipment.GetOwner(item.InstanceId),
                    item != null && item.InstanceId == SelectedItem);
            }

            Count.text = $"{User.Inventory.GetCount()} / {User.Inventory.Capacity}   ·   표시 {VisibleItems.Count}";
            Empty.text = VisibleItems.Count == 0 ? "선택한 영웅이 착용할 수 있는 장비가 없습니다." : string.Empty;
            OrderLabel.text = LobbyTables.GetText(LobbyTables.GetOption("Order", Order.ToString()).StrKey);
            SetIndicators(HeroIndicators, SelectedHero);
            SetIndicators(FilterIndicators, (int)Filter);
            ShowDetail();
        }

        private static void SetIndicators(Image[] images, int selected)
        {
            for (var i = 0; i < images.Length; i++)
            {
                images[i].color = i == selected ? new Color32(226, 183, 105, 255)
                    : new Color32(53, 66, 76, 255);
            }
        }

        private void ShowDetail()
        {
            var item = User.Inventory.FindEquipment(SelectedItem);
            DetailIcon.enabled = item != null;
            EquipButton.interactable = false;
            DiscardButton.interactable = false;

            if (item == null)
            {
                DetailTitle.text = "장비를 선택하세요";
                DetailType.text = "아이콘을 누르면 상세 정보가 표시됩니다.";
                DetailStats.text = string.Empty;
                Comparison.text = string.Empty;
                EquipLabel.text = "장착";

                return;
            }

            var row = Tables.GetEquipmentTableData(item.ItemKey);
            var heroKey = LobbyPresentationData.GetHeroKey(SelectedHero);
            var owner = Equipment.GetOwner(item.InstanceId);
            var worn = Equipment.GetEquippedEquipment(heroKey, row.EquipSlot);
            DetailIcon.sprite = LobbyPresentationData.GetItemIcon(item.ItemKey);
            DetailTitle.text = Tables.GetEquipmentName(item.ItemKey) + " +" + item.Growth.Level
                + (item.Growth.Locked == true ? "  [잠금]" : string.Empty);
            DetailTitle.color = LobbyPresentationData.GetRarityColor(row.Rarity);
            DetailType.text = LobbyPresentationData.GetRarityName(row.Rarity) + " · "
                + LobbyPresentationData.GetSlotName(row.EquipSlot) + " · "
                + (row.AllowedCharacterKeys.Length == 0 ? "모든 영웅" : GetAllowedHeroes(row));
            var values = Equipment.GetItemBonuses(item);
            var wornValues = worn == null ? new EquipmentBonuses(0, 0, 0) : Equipment.GetItemBonuses(worn);
            DetailStats.text = $"공격력  +{values.AttackDmg:0.###}     체력  +{values.MaxHP:0.###}\n공격속도  +{values.AttackSpeedBonus:P1}";
            var attack = values.AttackDmg - wornValues.AttackDmg;
            var hp = values.MaxHP - wornValues.MaxHP;
            var speed = (values.AttackSpeedBonus - wornValues.AttackSpeedBonus) * 100;
            Comparison.text = owner == heroKey ? "이 영웅이 장착 중인 장비입니다."
                : $"{LobbyPresentationData.GetHeroName(SelectedHero)} 교체 시  공격 {attack:+0.###;-0.###;0}  /  체력 {hp:+0.###;-0.###;0}  /  공속 {speed:+0.##;-0.##;0}%";

            if (owner != null && owner != heroKey)
            {
                Comparison.text = "다른 영웅이 장착 중입니다. 해당 영웅에서 먼저 해제하세요.";
            }
            else if (EquipmentRules.CanEquip(row, Tables.GetCharacterTableDataTable(heroKey)) == false)
            {
                Comparison.text = "선택한 영웅이 사용할 수 없는 장비입니다.";
            }

            EquipLabel.text = owner == heroKey ? "장착 해제" : "장착";
            EquipButton.interactable = owner == heroKey || Equipment.CanEquip(heroKey, item.InstanceId) == true;
            DiscardButton.interactable = owner == null && item.Growth.Locked == false;
        }

        private static string GetAllowedHeroes(EquipmentTableDataRow row)
        {
            var result = string.Empty;

            for (var i = 0; i < 3; i++)
            {
                if (Array.IndexOf(row.AllowedCharacterKeys, LobbyPresentationData.GetHeroKey(i)) >= 0)
                {
                    result += (result.Length == 0 ? string.Empty : ", ") + LobbyPresentationData.GetHeroName(i);
                }
            }

            return result + " 전용";
        }

        private void EquipSelected()
        {
            var item = User.Inventory.FindEquipment(SelectedItem);

            if (item == null)
            {
                return;
            }

            var heroKey = LobbyPresentationData.GetHeroKey(SelectedHero);

            if (Equipment.GetOwner(item.InstanceId) == heroKey)
            {
                Message.text = Equipment.TryUnequip(heroKey, Tables.GetEquipmentTableData(item.ItemKey).EquipSlot) == true
                    ? "장비를 해제했습니다." : "해제할 장비가 없습니다.";
            }
            else
            {
                Message.text = Equipment.TryEquip(heroKey, item.InstanceId, out var error) == true
                    ? "장비를 장착했습니다." : error;
            }
        }

        private void RequestDiscard()
        {
            var item = User.Inventory.FindEquipment(SelectedItem);

            if (item == null || item.Growth.Locked == true || Equipment.GetOwner(item.InstanceId) != null)
            {
                return;
            }

            PendingDiscard = item.InstanceId;
            DiscardTitle.text = Tables.GetEquipmentName(item.ItemKey) + "\n이 장비를 버릴까요?";
            UIPanelMotion.Show(DiscardDialog);
        }

        private void DiscardSelected()
        {
            var instanceId = PendingDiscard;
            CloseDiscard();
            Message.text = Inventory.TryDiscard(instanceId) == true
                ? "장비를 버렸습니다." : "장착 중이거나 이미 삭제된 장비입니다.";
        }

        private void CloseDiscard()
        {
            PendingDiscard = null;
            UIPanelMotion.Hide(DiscardDialog);
        }

        private void OnEquipmentChanged(string heroKey)
        {
            Refresh();
        }

        private void OnEnable()
        {
            if (User != null)
            {
                Connect();
            }
        }

        private void OnDisable()
        {
            Disconnect();
            CloseDiscard();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
