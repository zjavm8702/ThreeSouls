using System;
using ThreeSouls.Growth;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.Events;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 캐릭터 탭의 영웅별 성장 상태, 강화 한도와 비용 및 확인 요청을 표시합니다.
    /// </summary>
    public sealed class UI_HeroGrowth : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button[] HeroButtons;
        [SerializeField] private UnityEngine.UI.Image[] HeroIndicators;
        [SerializeField] private UnityEngine.UI.Button[] StatButtons;
        [SerializeField] private UnityEngine.UI.Image[] StatIndicators;
        [SerializeField] private UnityEngine.UI.Text[] StatValues;
        [SerializeField] private UnityEngine.UI.Text[] RankLabels;
        [SerializeField] private UnityEngine.UI.Text Heading;
        [SerializeField] private UnityEngine.UI.Text Progress;
        [SerializeField] private UnityEngine.UI.Image ExperienceFill;
        [SerializeField] private UnityEngine.UI.Text Wallet;
        [SerializeField] private UnityEngine.UI.Text Detail;
        [SerializeField] private UnityEngine.UI.Text Cost;
        [SerializeField] private UnityEngine.UI.Text Message;
        [SerializeField] private UnityEngine.UI.Button UpgradeButton;
        [SerializeField] private UnityEngine.UI.Button CloseButton;
        [SerializeField] private GameObject Confirmation;
        [SerializeField] private UnityEngine.UI.Text ConfirmationText;
        [SerializeField] private UnityEngine.UI.Button ConfirmButton;
        [SerializeField] private UnityEngine.UI.Button CancelButton;
        private HeroGrowthManager Growth;
        private UserManager User;
        private EquipmentManager Equipment;
        private TableManager Tables;
        private int HeroIndex;
        private HeroStat SelectedStat;
        private HeroUpgradeQuote Pending;
        private UnityAction[] HeroCallbacks;
        private UnityAction[] StatCallbacks;

        public void SetUp(HeroGrowthManager growth, UserManager user, EquipmentManager equipment, TableManager tables)
        {
            Disconnect();
            Growth = growth ?? throw new ArgumentNullException(nameof(growth));
            User = user ?? throw new ArgumentNullException(nameof(user));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            InitializeButtons();
            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        public void OpenHero(int index)
        {
            if (Growth == null || index < 0 || index >= HeroButtons.Length)
            {
                throw new InvalidOperationException("성장 화면과 영웅을 먼저 연결해야 합니다.");
            }

            HeroIndex = index;
            SelectedStat = HeroStat.Attack;
            UIPanelMotion.Hide(Confirmation);
            Message.text = "능력치를 선택하고 비용을 확인하세요. 레벨업은 강화 한도를 늘립니다.";
            UIPanelMotion.Show(gameObject);
            Refresh();
        }

        public void Close()
        {
            UIPanelMotion.Hide(Confirmation);
            Pending = default;
            UIPanelMotion.Hide(gameObject);
        }

        private void InitializeButtons()
        {
            if (HeroCallbacks != null)
            {
                return;
            }

            if (HeroButtons.Length != 3 || StatButtons.Length != 6 || StatValues.Length != 6 ||
                RankLabels.Length != 6 || StatIndicators.Length != 6 || HeroIndicators.Length != 3 ||
                Confirmation == null || Heading == null || Progress == null || ExperienceFill == null ||
                Wallet == null || Detail == null || Cost == null || Message == null || UpgradeButton == null ||
                CloseButton == null || ConfirmButton == null || CancelButton == null || ConfirmationText == null)
            {
                throw new InvalidOperationException("영웅 성장 화면의 버튼/표시 참조를 연결해야 합니다.");
            }

            HeroCallbacks = new UnityAction[HeroButtons.Length];
            for (var i = 0; i < HeroButtons.Length; i++)
            {
                var index = i;
                HeroCallbacks[i] = () => OpenHero(index);
                HeroButtons[i].onClick.AddListener(HeroCallbacks[i]);
            }

            StatCallbacks = new UnityAction[StatButtons.Length];
            for (var i = 0; i < StatButtons.Length; i++)
            {
                var stat = (HeroStat)i;
                StatCallbacks[i] = () => SelectStat(stat);
                StatButtons[i].onClick.AddListener(StatCallbacks[i]);
            }

            UpgradeButton.onClick.AddListener(RequestUpgrade);
            CloseButton.onClick.AddListener(Close);
            ConfirmButton.onClick.AddListener(ConfirmUpgrade);
            CancelButton.onClick.AddListener(CancelUpgrade);
        }

        private void SelectStat(HeroStat stat)
        {
            SelectedStat = stat;
            Message.text = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            var key = LobbyPresentationData.GetHeroKey(HeroIndex);
            var hero = Growth.GetHero(key);
            var basis = Tables.GetCharacterTableDataTable(key);
            var growth = Growth.GetBonuses(basis);
            var equipment = Equipment.GetEquipmentBonuses(basis);
            Heading.text = LobbyPresentationData.GetHeroName(HeroIndex) + " · 능력치 성장";
            var needed = HeroGrowthRules.GetExperienceRequired(hero.Level);
            Progress.text = needed == 0 ? $"Lv.{hero.Level} · 최고 레벨" :
                $"Lv.{hero.Level}  ·  경험치 {hero.Experience:N0} / {needed:N0}";
            var experienceRatio = needed == 0 ? 1 : (float)hero.Experience / needed;
            ExperienceFill.rectTransform.anchorMax = new Vector2(experienceRatio, 1);
            Wallet.text = $"골드 {User.UData.Gold:N0}  ·  성장석 {User.UData.GrowthStones:N0}";
            for (var i = 0; i < HeroButtons.Length; i++)
            {
                HeroIndicators[i].color = i == HeroIndex ? new Color32(226, 183, 105, 255) : new Color32(53, 66, 76, 255);
            }

            for (var i = 0; i < StatButtons.Length; i++)
            {
                var stat = (HeroStat)i;
                var row = HeroGrowthRules.GetStat(stat);
                var locked = hero.Level < row.UnlockLevel;
                StatValues[i].text = row.DisplayName + (stat == HeroStat.AttackSpeed ? " 보너스" : string.Empty) + "  " +
                    HeroGrowthPresentation.Format(stat, HeroGrowthPresentation.GetValue(stat, basis, equipment, growth));
                RankLabels[i].text = locked == true ? $"Lv.{row.UnlockLevel} 해금" :
                    $"강화 {hero.GetRank(stat)} / {HeroGrowthRules.GetRankLimit(stat, hero.Level)}";
                RankLabels[i].color = locked == true ? new Color32(151, 169, 176, 255) : new Color32(226, 183, 105, 255);
                StatIndicators[i].color = stat == SelectedStat ? new Color32(226, 183, 105, 255) : new Color32(53, 66, 76, 255);
            }

            var selected = HeroGrowthRules.GetStat(SelectedStat);
            var value = HeroGrowthPresentation.GetValue(SelectedStat, basis, equipment, growth);
            var gain = HeroGrowthRules.GetGain(SelectedStat, basis);
            var canUpgrade = Growth.TryQuoteUpgrade(key, SelectedStat, out var quote, out var error);
            var rankLimit = HeroGrowthRules.GetRankLimit(SelectedStat, hero.Level);
            Detail.text = selected.DisplayName + "  " + HeroGrowthPresentation.Format(SelectedStat, value) +
                " → " + HeroGrowthPresentation.Format(SelectedStat, value + gain) + "\n";
            if (SelectedStat == HeroStat.Luck)
            {
                var keys = ThreeSouls.Data.LobbyTables.GetVillageRules().HeroKeys;
                Detail.text += $"부대 장비 드롭 보너스 +{Growth.GetPartyDropMultiplier(keys) - 1:P2}\n";
                var rule = HeroGrowthRules.GetRule();
                Detail.text += $"부대 평균 행운 1당 장비 드롭 확률이 {rule.LuckDropBonusPerPoint:P1} 증가합니다. " +
                    $"행운 보너스는 최대 {rule.MaximumLuckDropBonus:P0}까지 적용됩니다.";
            }
            else
            {
                Detail.text += selected.Note;
            }
            Cost.text = $"골드 {quote.Gold:N0} + 성장석 {quote.Stones:N0}\n" +
                (canUpgrade == true ? $"현재 레벨 한도 {rankLimit}단계 · 최대 {selected.MaximumRank}단계" : error);
            UpgradeButton.interactable = canUpgrade;
        }

        private void RequestUpgrade()
        {
            var key = LobbyPresentationData.GetHeroKey(HeroIndex);
            if (Growth.TryQuoteUpgrade(key, SelectedStat, out Pending, out var error) == false)
            {
                Message.text = error;
                Refresh();
                return;
            }

            ConfirmationText.text = LobbyPresentationData.GetHeroName(HeroIndex) + "의 " +
                HeroGrowthRules.GetStat(SelectedStat).DisplayName + $"을(를) {Pending.Rank + 1}단계로 강화할까요?\n\n" +
                $"골드 {Pending.Gold:N0} · 성장석 {Pending.Stones:N0} 소모\n강화는 해제하거나 환급할 수 없습니다.";
            UIPanelMotion.Show(Confirmation);
        }

        private void ConfirmUpgrade()
        {
            if (Confirmation.activeSelf == false)
            {
                return;
            }

            var expected = Pending;
            CancelUpgrade();
            Message.text = Growth.TryUpgrade(expected, out var error) == true ? "능력치 강화가 완료되었습니다." : error;
            Refresh();
        }

        private void CancelUpgrade()
        {
            UIPanelMotion.Hide(Confirmation);
            Pending = default;
        }

        private void Connect()
        {
            Refresh();
            Growth.OnHeroGrowthChangedEventHandler += OnHeroChanged;
            Equipment.OnEquipmentChangedEventHandler += OnHeroChanged;
            User.OnCombatRewardChangedEventHandler += OnGoldChanged;
            User.OnGrowthMaterialsChangedEventHandler += OnStonesChanged;
        }

        private void Disconnect()
        {
            if (Growth != null)
            {
                Growth.OnHeroGrowthChangedEventHandler -= OnHeroChanged;
                Equipment.OnEquipmentChangedEventHandler -= OnHeroChanged;
                User.OnCombatRewardChangedEventHandler -= OnGoldChanged;
                User.OnGrowthMaterialsChangedEventHandler -= OnStonesChanged;
            }
        }

        private void OnHeroChanged(string key)
        {
            Refresh();
        }

        private void OnGoldChanged(int gold, int chests)
        {
            Refresh();
        }

        private void OnStonesChanged(int stones)
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
            if (Confirmation != null)
            {
                CancelUpgrade();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
            if (HeroCallbacks == null)
            {
                return;
            }

            for (var i = 0; i < HeroCallbacks.Length; i++)
            {
                HeroButtons[i].onClick.RemoveListener(HeroCallbacks[i]);
            }

            for (var i = 0; i < StatCallbacks.Length; i++)
            {
                StatButtons[i].onClick.RemoveListener(StatCallbacks[i]);
            }

            UpgradeButton.onClick.RemoveListener(RequestUpgrade);
            CloseButton.onClick.RemoveListener(Close);
            ConfirmButton.onClick.RemoveListener(ConfirmUpgrade);
            CancelButton.onClick.RemoveListener(CancelUpgrade);
        }
    }
}
