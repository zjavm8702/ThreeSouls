using System;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 물약 상점과 출정 준비 화면을 표시하고 공급품 Manager에 요청합니다.
    /// </summary>
    public sealed class UI_LobbySupplies : MonoBehaviour
    {
        [SerializeField] private Text Wallet;
        [SerializeField] private Text[] Owned;
        [SerializeField] private Button[] BuyOne;
        [SerializeField] private Button[] BuyFive;
        [SerializeField] private Button[] SlotButtons;
        [SerializeField] private Text[] SlotLabels;
        [SerializeField] private Image[] SlotIndicators;
        [SerializeField] private Button[] TypeButtons;
        [SerializeField] private Button RemoveSlot;
        [SerializeField] private Button Decrease;
        [SerializeField] private Button Increase;
        [SerializeField] private Text Selected;
        [SerializeField] private Button[] ThresholdButtons;
        [SerializeField] private Image[] ThresholdIndicators;
        [SerializeField] private Button ClearSlots;
        [SerializeField] private Text Summary;
        [SerializeField] private Text Message;
        private static readonly ConsumableType[] PotionTypes =
            { ConsumableType.SmallHealingPotion, ConsumableType.PartyHealingPotion };
        private static readonly AutoHealThreshold[] Thresholds =
            { AutoHealThreshold.Disabled, AutoHealThreshold.At20Percent, AutoHealThreshold.At30Percent, AutoHealThreshold.At40Percent };
        private LobbySupplyManager Manager;
        private UserManager User;
        private int SelectedSlot;

        private void Awake()
        {
            if (Owned.Length != 2 || BuyOne.Length != 2 || BuyFive.Length != 2 || TypeButtons.Length != 2 ||
                SlotButtons.Length != SupplyLoadout.SlotCount || SlotLabels.Length != SupplyLoadout.SlotCount ||
                SlotIndicators.Length != SupplyLoadout.SlotCount || ThresholdButtons.Length != Thresholds.Length ||
                ThresholdIndicators.Length != Thresholds.Length)
            {
                throw new InvalidOperationException("공급품 화면의 물약, 슬롯, 자동 회복 연결 수를 확인하세요.");
            }

            for (var i = 0; i < PotionTypes.Length; i++)
            {
                var type = PotionTypes[i];
                BuyOne[i].onClick.AddListener(() => Buy(type, LobbyTables.GetSupplyRules().BuyAmounts[0]));
                BuyFive[i].onClick.AddListener(() => Buy(type, LobbyTables.GetSupplyRules().BuyAmounts[1]));
                TypeButtons[i].onClick.AddListener(() => SetType(type));
            }

            for (var i = 0; i < SlotButtons.Length; i++)
            {
                var index = i;
                SlotButtons[i].onClick.AddListener(() => SelectSlot(index));
            }

            for (var i = 0; i < ThresholdButtons.Length; i++)
            {
                var threshold = Thresholds[i];
                ThresholdButtons[i].onClick.AddListener(() =>
                {
                    Manager.TrySetAutoHeal(threshold, out var error);
                    ShowResult(error, "자동 회복 기준을 변경했습니다.");
                });
            }

            Decrease.onClick.AddListener(() => ChangeCount(-1));
            Increase.onClick.AddListener(() => ChangeCount(1));
            RemoveSlot.onClick.AddListener(() =>
            {
                Manager.TrySetSlot(SelectedSlot, ConsumableType.None, 0, out var error);
                ShowResult(error, "선택한 슬롯을 비웠습니다.");
            });
            ClearSlots.onClick.AddListener(() =>
            {
                Manager.TryClearSlots(out var error);
                ShowResult(error, "공급품 편성을 초기화했습니다.");
            });
        }

        public void SetUp(LobbySupplyManager manager, UserManager user)
        {
            Disconnect();
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            User = user ?? throw new ArgumentNullException(nameof(user));

            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        private void SelectSlot(int index)
        {
            SelectedSlot = index;
            Message.text = "물약 종류를 고르고 + / −로 수량을 조절하세요.";
            Refresh();
        }

        private void Buy(ConsumableType type, int count)
        {
            Manager.TryBuy(type, count, out var error);
            ShowResult(error, $"{ConsumableCatalog.GetName(type)} {count}개를 구매했습니다.");
        }

        private void SetType(ConsumableType type)
        {
            var current = User.UData.Supplies.GetSlot(SelectedSlot);
            Manager.TrySetSlot(SelectedSlot, type, Math.Max(1, current.Count), out var error);
            ShowResult(error, $"슬롯 {SelectedSlot + 1}에 {ConsumableCatalog.GetName(type)}을 편성했습니다.");
        }

        private void ChangeCount(int amount)
        {
            var current = User.UData.Supplies.GetSlot(SelectedSlot);
            Manager.TrySetSlot(SelectedSlot, current.Type, current.Count + amount, out var error);
            ShowResult(error, "공급품 수량을 변경했습니다.");
        }

        private void ShowResult(string error, string success)
        {
            Message.text = error ?? success;
            Message.color = error == null ? new Color32(151, 169, 176, 255) : new Color32(244, 163, 139, 255);
        }

        private void Refresh()
        {
            var data = User.UData;
            var loadout = data.Supplies;
            Wallet.text = $"보유 골드  {data.Gold:N0}";

            for (var i = 0; i < PotionTypes.Length; i++)
            {
                var type = PotionTypes[i];
                var price = ConsumableCatalog.GetPrice(type);
                Owned[i].text = $"보유 {data.Consumables.GetOwned(type)}개  ·  편성 {loadout.GetAssigned(type)}개";
                BuyOne[i].interactable = data.Gold >= (long)price * LobbyTables.GetSupplyRules().BuyAmounts[0] &&
                    data.Consumables.CanAdd(type, LobbyTables.GetSupplyRules().BuyAmounts[0]) == true;
                BuyFive[i].interactable = data.Gold >= (long)price * LobbyTables.GetSupplyRules().BuyAmounts[1] && data.Consumables.CanAdd(type, LobbyTables.GetSupplyRules().BuyAmounts[1]) == true;
            }

            for (var i = 0; i < SlotButtons.Length; i++)
            {
                var slot = loadout.GetSlot(i);
                SlotLabels[i].text = $"슬롯 {i + 1}\n{ConsumableCatalog.GetName(slot.Type)}  {slot.Count} / {SupplyLoadout.GetMaximumPerSlot()}";
                SlotIndicators[i].color = i == SelectedSlot ? new Color32(226, 183, 105, 255) : new Color32(53, 66, 76, 255);
            }

            var selected = loadout.GetSlot(SelectedSlot);
            Selected.text = $"슬롯 {SelectedSlot + 1} 선택  ·  {selected.Count} / {SupplyLoadout.GetMaximumPerSlot()}개";
            Decrease.interactable = selected.Count > 0;
            Increase.interactable = selected.Count > 0 && selected.Count < SupplyLoadout.GetMaximumPerSlot() &&
                data.Consumables.GetOwned(selected.Type) > loadout.GetAssigned(selected.Type);
            RemoveSlot.interactable = selected.Count > 0;
            ClearSlots.interactable = loadout.GetTotalAssigned() > 0;

            for (var i = 0; i < Thresholds.Length; i++)
            {
                ThresholdIndicators[i].color = loadout.Threshold == Thresholds[i]
                    ? new Color32(226, 183, 105, 255) : new Color32(53, 66, 76, 255);
            }

            Summary.text = $"준비한 공급품  {loadout.GetTotalAssigned()} / {SupplyLoadout.SlotCount * SupplyLoadout.GetMaximumPerSlot()}개\n자동 회복  ·  {ConsumableCatalog.GetThresholdLabel(loadout.Threshold)}";
        }

        private void Connect()
        {
            Refresh();
            Manager.OnPreparationChangedEventHandler += Refresh;
            ThreeSouls.Persistence.PersistentSession.Changed += Refresh;
            User.OnCombatRewardChangedEventHandler += OnCurrencyChanged;
        }

        private void OnCurrencyChanged(int gold, int chestCount)
        {
            Refresh();
        }

        private void Disconnect()
        {
            ThreeSouls.Persistence.PersistentSession.Changed -= Refresh;
            if (Manager != null)
            {
                Manager.OnPreparationChangedEventHandler -= Refresh;
                User.OnCombatRewardChangedEventHandler -= OnCurrencyChanged;
            }
        }

        private void OnEnable()
        {
            if (Manager != null)
            {
                Connect();
            }
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
