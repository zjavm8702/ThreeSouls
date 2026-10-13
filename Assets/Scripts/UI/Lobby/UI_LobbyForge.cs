using System;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 선택 장비의 강화 미리보기, 잠금과 확인 후 분해 요청 및 화면 구독을 맡습니다.
    /// </summary>
    public sealed class UI_LobbyForge : MonoBehaviour
    {
        [SerializeField] private GameObject Panel;
        [SerializeField] private Button OpenButton;
        [SerializeField] private Button CloseButton;
        [SerializeField] private Button UpgradeButton;
        [SerializeField] private Button LockButton;
        [SerializeField] private Text LockLabel;
        [SerializeField] private Button DismantleButton;
        [SerializeField] private Text Title;
        [SerializeField] private Text Wallet;
        [SerializeField] private Text Stats;
        [SerializeField] private Text Cost;
        [SerializeField] private Text Message;
        [SerializeField] private GameObject Confirmation;
        [SerializeField] private Text ConfirmationTitle;
        [SerializeField] private Button ConfirmButton;
        [SerializeField] private Button CancelButton;
        private EquipmentGrowthManager Growth;
        private EquipmentManager Equipment;
        private UserManager User;
        private TableManager Tables;
        private UI_LobbyInventory Inventory;
        private string SelectedItem;
        private string PendingDismantle;

        private void Awake()
        {
            OpenButton.onClick.AddListener(Open);
            CloseButton.onClick.AddListener(Close);
            UpgradeButton.onClick.AddListener(Upgrade);
            LockButton.onClick.AddListener(ToggleLock);
            DismantleButton.onClick.AddListener(RequestDismantle);
            ConfirmButton.onClick.AddListener(ConfirmDismantle);
            CancelButton.onClick.AddListener(CancelDismantle);
        }

        public void SetUp(EquipmentGrowthManager growth, EquipmentManager equipment,
            UserManager user, TableManager tables, UI_LobbyInventory inventory)
        {
            Disconnect();
            Growth = growth ?? throw new ArgumentNullException(nameof(growth));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            User = user ?? throw new ArgumentNullException(nameof(user));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        private void Connect()
        {
            Growth.OnEquipmentGrowthChangedEventHandler += Refresh;
            User.UData.Inventory.OnInventoryChangedEventHandler += Refresh;
            User.OnEnhancementMaterialsChangedEventHandler += OnMaterialsChanged;
            User.OnCombatRewardChangedEventHandler += OnCurrencyChanged;
        }

        private void Disconnect()
        {
            if (Growth != null)
            {
                Growth.OnEquipmentGrowthChangedEventHandler -= Refresh;
                User.UData.Inventory.OnInventoryChangedEventHandler -= Refresh;
                User.OnEnhancementMaterialsChangedEventHandler -= OnMaterialsChanged;
                User.OnCombatRewardChangedEventHandler -= OnCurrencyChanged;
            }
        }

        private void Open()
        {
            SelectedItem = Inventory.GetSelectedItem()?.InstanceId;
            Message.text = "강화는 항상 성공합니다. 잠금은 분해와 버리기를 막습니다.";
            UIPanelMotion.Show(Panel);
            Refresh();
        }

        private void Close()
        {
            CancelDismantle();
            UIPanelMotion.Hide(Panel);
        }

        private void Refresh()
        {
            if (Panel.activeSelf == false)
            {
                return;
            }

            var item = User.UData.Inventory.FindEquipment(SelectedItem);
            Wallet.text = $"골드  {User.UData.Gold:N0}     강화석  {User.UData.EnhancementStones:N0}";
            UpgradeButton.interactable = false;
            LockButton.interactable = item != null;
            DismantleButton.interactable = false;

            if (item == null)
            {
                Title.text = "선택한 장비가 없습니다";
                Stats.text = "분해를 마쳤다면 돌아가서 다른 장비를 선택하세요.";
                Cost.text = string.Empty;
                LockLabel.text = "잠금";
                return;
            }

            var slot = Tables.GetEquipmentTableData(item.ItemKey).EquipSlot;
            var now = Equipment.GetItemBonuses(item);
            var next = EquipmentUpgradeRules.Calculate(item, slot, Math.Min(EquipmentUpgradeRules.GetMaxLevel(), item.Growth.Level + 1));
            Title.text = Tables.GetEquipmentName(item.ItemKey) + "  +" + item.Growth.Level;
            Stats.text = $"원본 추첨값\n공격 +{item.AttackDmg:0.###}   HP +{item.MaxHP:0.###}   공속 +{item.AttackSpeedBonus:P1}\n\n"
                + $"현재 → 다음 단계\n공격 +{now.AttackDmg:0.###} → +{next.AttackDmg:0.###}\n"
                + $"HP +{now.MaxHP:0.###} → +{next.MaxHP:0.###}\n공속 +{now.AttackSpeedBonus:P1} → +{next.AttackSpeedBonus:P1}";
            LockLabel.text = item.Growth.Locked == true ? "잠금 해제" : "장비 잠금";
            DismantleButton.interactable = item.Growth.Locked == false && Equipment.GetOwner(item.InstanceId) == null;

            if (EquipmentUpgradeRules.TryGetCost(item.Growth.Level, out var gold, out var stones) == true)
            {
                var missingGold = Math.Max(0, gold - User.UData.Gold);
                var missingStones = Math.Max(0, stones - User.UData.EnhancementStones);
                Cost.text = $"+{item.Growth.Level + 1} 비용  골드 {gold:N0} / 강화석 {stones}\n"
                    + (missingGold > 0 || missingStones > 0 ? $"부족: 골드 {missingGold:N0} / 강화석 {missingStones}"
                        : $"성공 보장 · 최대 +{EquipmentUpgradeRules.GetMaxLevel()}");
                var rule = LobbyTables.GetRules();
                UpgradeButton.interactable = missingGold == 0 && missingStones == 0 &&
                    (item.Growth.Locked == false || rule.AllowLockedUpgrade == true) &&
                    (Equipment.GetOwner(item.InstanceId) == null || rule.AllowEquippedUpgrade == true);
            }
            else
            {
                Cost.text = $"최대 강화 +{EquipmentUpgradeRules.GetMaxLevel()}에 도달했습니다.";
            }

            var rarity = Tables.GetEquipmentTableData(item.ItemKey).Rarity;
            Cost.text += $"\n분해 반환: 강화석 {Growth.GetDismantleStones(item)}개 "
                + $"(투자량 {EquipmentUpgradeRules.GetDismantleReturnRate(rarity):P0} 포함)";

            if (Equipment.GetOwner(item.InstanceId) != null)
            {
                Cost.text += "\n장착 중: 해제 후 분해할 수 있습니다.";
            }
            else if (item.Growth.Locked == true)
            {
                Cost.text += "\n잠금 중: 해제 후 분해할 수 있습니다.";
            }
        }

        private void Upgrade()
        {
            var success = Growth.TryUpgrade(SelectedItem, out var error);
            Message.text = success ? "강화가 완료되었습니다." : error;
            if (!success) ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Error);
            Refresh();
        }

        private void ToggleLock()
        {
            var item = User.UData.Inventory.FindEquipment(SelectedItem);

            if (item != null)
            {
                Message.text = Growth.TrySetLocked(SelectedItem, item.Growth.Locked == false, out var error) == true
                    ? "잠금 상태를 변경했습니다." : error;
            }
        }

        private void RequestDismantle()
        {
            var item = User.UData.Inventory.FindEquipment(SelectedItem);

            if (item == null || item.Growth.Locked == true || Equipment.GetOwner(SelectedItem) != null)
            {
                return;
            }

            PendingDismantle = item.InstanceId;
            ConfirmationTitle.text = $"{Tables.GetEquipmentName(item.ItemKey)} +{item.Growth.Level}\n분해하면 장비가 사라집니다.\n강화석 {Growth.GetDismantleStones(item)}개를 받을까요?";
            UIPanelMotion.Show(Confirmation);
        }

        private void ConfirmDismantle()
        {
            var id = PendingDismantle;
            CancelDismantle();
            Message.text = Growth.TryDismantle(id, out var stones, out var error) == true
                ? $"분해 완료: 강화석 {stones}개를 받았습니다." : error;
            Refresh();
        }

        private void CancelDismantle()
        {
            PendingDismantle = null;
            UIPanelMotion.Hide(Confirmation);
        }

        private void OnMaterialsChanged(int stones)
        {
            Refresh();
        }

        private void OnCurrencyChanged(int gold, int chestCount)
        {
            Refresh();
        }

        private void OnEnable()
        {
            if (Growth != null)
            {
                Connect();
            }
        }

        private void OnDisable()
        {
            Disconnect();
            Close();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
