using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using ThreeSouls.Runes;
using TMPro;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 두 종류의 룬 목록·장착·효과 비교와 제작·강화 확인 요청만 담당합니다.
    /// </summary>
    public sealed class UI_RuneStones : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button CloseButton;
        [SerializeField] private UnityEngine.UI.Button[] CategoryButtons;
        [SerializeField] private UnityEngine.UI.Button[] HeroButtons;
        [SerializeField] private UnityEngine.UI.Button[] RuneButtons;
        [SerializeField] private TMP_Text[] RuneLabels;
        [SerializeField] private UnityEngine.UI.Image[] RuneImages;
        [SerializeField] private Sprite[] DefinitionIcons;
        [SerializeField] private TMP_Text Wallet;
        [SerializeField] private TMP_Text Loadout;
        [SerializeField] private TMP_Text Detail;
        [SerializeField] private TMP_Text Message;
        [SerializeField] private UnityEngine.UI.Button EquipButton;
        [SerializeField] private TMP_Text EquipLabel;
        [SerializeField] private UnityEngine.UI.Button PurchaseButton;
        [SerializeField] private TMP_Text PurchaseLabel;
        [SerializeField] private GameObject Confirmation;
        [SerializeField] private TMP_Text ConfirmationText;
        [SerializeField] private UnityEngine.UI.Button ConfirmButton;
        [SerializeField] private UnityEngine.UI.Button CancelButton;
        private RuneManager Runes;
        private UserManager User;
        private RuneCategory Category;
        private int HeroIndex;
        private string Selected;
        private readonly List<RuneTableDataRow> Visible = new();
        private RuneQuote Quote;
        private bool Bound;
        private readonly Color SelectedColor = new Color32(255, 221, 161, 255);
        private readonly Color CardColor = new Color32(177, 194, 218, 255);

        public void SetUp(RuneManager runes, UserManager user)
        {
            Disconnect();
            Runes = runes ?? throw new ArgumentNullException(nameof(runes));
            User = user ?? throw new ArgumentNullException(nameof(user));
            if (Bound == false)
            {
                CloseButton.onClick.AddListener(Close);
                CategoryButtons[0].onClick.AddListener(SelectSanctuary);
                CategoryButtons[1].onClick.AddListener(SelectSkills);
                for (var i = 0; i < HeroButtons.Length; i++)
                {
                    var index = i;
                    HeroButtons[i].onClick.AddListener(() => SelectHero(index));
                }

                for (var i = 0; i < RuneButtons.Length; i++)
                {
                    var index = i;
                    RuneButtons[i].onClick.AddListener(() => SelectRune(index));
                }

                EquipButton.onClick.AddListener(Equip);
                PurchaseButton.onClick.AddListener(RequestPurchase);
                ConfirmButton.onClick.AddListener(Confirm);
                CancelButton.onClick.AddListener(Cancel);
                Bound = true;
            }

            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        public void Open()
        {
            if (Runes == null)
            {
                throw new InvalidOperationException("룬 화면에 Manager를 연결하세요.");
            }

            Message.text = "장착·교체·해제는 무료입니다. 스킬마다 한 개의 룬을 선택하세요.";
            UIPanelMotion.Show(gameObject);
            transform.SetAsLastSibling();
            ResetDetailScroll();
        }

        public void Close()
        {
            Cancel();
            UIPanelMotion.Hide(gameObject);
        }

        private void Connect()
        {
            Refresh();
            Runes.OnRunesChangedEventHandler += Refresh;
            User.OnCombatRewardChangedEventHandler += OnGoldChanged;
            User.OnEnhancementMaterialsChangedEventHandler += OnStonesChanged;
        }

        private void Disconnect()
        {
            if (Runes != null)
            {
                Runes.OnRunesChangedEventHandler -= Refresh;
                User.OnCombatRewardChangedEventHandler -= OnGoldChanged;
                User.OnEnhancementMaterialsChangedEventHandler -= OnStonesChanged;
            }
        }

        private void SelectSanctuary()
        {
            Category = RuneCategory.Sanctuary;
            Selected = null;
            Cancel();
            Refresh();
            ResetDetailScroll();
        }

        private void SelectSkills()
        {
            Category = RuneCategory.Skill;
            Selected = null;
            Cancel();
            Refresh();
            ResetDetailScroll();
        }

        private void SelectHero(int index)
        {
            HeroIndex = index;
            Selected = null;
            Cancel();
            Refresh();
            ResetDetailScroll();
        }

        private void SelectRune(int index)
        {
            if (index < Visible.Count)
            {
                Selected = Visible[index].Key;
                Cancel();
                Refresh();
                ResetDetailScroll();
            }
        }

        private void Refresh()
        {
            Wallet.text = $"골드 {User.UData.Gold:N0}     강화석 {User.UData.EnhancementStones:N0}";
            for (var i = 0; i < CategoryButtons.Length; i++)
            {
                CategoryButtons[i].targetGraphic.color = (i == 0 && Category == RuneCategory.Sanctuary ||
                    i == 1 && Category == RuneCategory.Skill) ? SelectedColor : CardColor;
            }

            var heroKey = LobbyPresentationData.GetHeroKey(HeroIndex);
            for (var i = 0; i < HeroButtons.Length; i++)
            {
                HeroButtons[i].gameObject.SetActive(Category == RuneCategory.Skill);
                HeroButtons[i].targetGraphic.color = i == HeroIndex ? SelectedColor : CardColor;
            }

            var skills = LobbyTables.Get<SkillTableDataTable>().Items;
            Visible.Clear();
            var rows = LobbyTables.Get<RuneTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var match = row.Category == Category && (Category == RuneCategory.Sanctuary || IsHeroSkill(skills, row.SkillKey, heroKey));
                if (match == true)
                {
                    Visible.Add(row);
                }
            }

            if (Visible.Count == 0 || Visible.Count > RuneButtons.Length)
            {
                throw new InvalidOperationException("룬 화면의 후보 수와 테이블이 일치하지 않습니다.");
            }

            if (Selected == null || Visible.Exists(x => x.Key == Selected) == false)
            {
                Selected = Visible[0].Key;
            }

            for (var i = 0; i < RuneButtons.Length; i++)
            {
                RuneButtons[i].gameObject.SetActive(i < Visible.Count);
                if (i >= Visible.Count)
                {
                    RuneImages[i].enabled = false;
                    continue;
                }

                var row = Visible[i];
                var rank = Runes.GetRank(row.Key);
                var status = row.Enabled == false ? "준비 중" : rank == 0 ? "미보유" : $"Lv.{rank}" + (Runes.IsEquipped(row.Key) == true ? " · 장착 중" : " · 보유");
                if (rank == 0 && User.UData.Campaign.ClearedProgress < CampaignBalance.RuneUnlock(row.Key)) status = CampaignBalance.UnlockLabel(CampaignBalance.RuneUnlock(row.Key));
                RuneLabels[i].text = row.DisplayName + "\n" + status;
                RuneButtons[i].targetGraphic.color = row.Key == Selected ? SelectedColor : CardColor;
                RuneImages[i].sprite = DefinitionIcons[Array.IndexOf(rows, row)];
                RuneImages[i].enabled = RuneImages[i].sprite != null;
            }

            if (Category == RuneCategory.Sanctuary)
            {
                var snapshot = Runes.CreatePreview();
                Loadout.text = $"성소 장비 +{snapshot.EquipmentDropMultiplier - 1:P0} · 골드 +{snapshot.GoldMultiplier - 1:P0}\n무작위 능력 감소 -{snapshot.DebuffRatio:P0} · 보석 준비 중";
            }
            else
            {
                var text = string.Empty;
                for (var i = 0; i < skills.Length; i++)
                {
                    if (skills[i].HeroKey == heroKey)
                    {
                        text += RunePresentation.GetName(Runes.GetEquipped("Skill/" + skills[i].Key)) + "\n";
                    }
                }

                Loadout.text = "현재 스킬 룬\n" + text.TrimEnd().Replace("\n", " / ");
            }

            var selected = RuneRules.GetDefinition(Selected);
            var selectedRank = Runes.GetRank(Selected);
            Detail.text = selected.DisplayName + "\n" + RunePresentation.Describe(selected, selectedRank);
            if (selected.Enabled == true && selectedRank > 0 && selectedRank < RuneRules.GetSanctuaryRule().MaximumRuneRank)
            {
                Detail.text += "\n\n다음 Lv." + (selectedRank + 1) + "\n" + RunePresentation.Describe(selected, selectedRank + 1);
            }

            EquipButton.interactable = selected.Enabled == true && selectedRank > 0;
            EquipLabel.text = Runes.IsEquipped(Selected) == true ? "해제" : "장착 / 교체";
            var maximum = selectedRank >= RuneRules.GetSanctuaryRule().MaximumRuneRank;
            PurchaseButton.interactable = Runes.CanPurchase(Selected,
                selectedRank == 0 ? RunePurchase.Craft : RunePurchase.Upgrade, out _);
            var cost = selectedRank > 0 && maximum == false ? RuneRules.GetUpgrade(selectedRank + 1) : null;
            PurchaseLabel.text = selected.Enabled == false ? "준비 중" : maximum == true ? "최대 단계" :
                (selectedRank == 0 ? "제작" : "강화 +1") + $"\n{(cost == null ? selected.CraftGold : cost.Gold):N0}골드 · 강화석 {(cost == null ? selected.CraftStones : cost.Stones)}";
        }

        private static bool IsHeroSkill(SkillTableDataRow[] skills, string skillKey, string heroKey)
        {
            for (var i = 0; i < skills.Length; i++)
            {
                if (skills[i].Key == skillKey && skills[i].HeroKey == heroKey)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetDetailScroll()
        {
            var scroll = Detail.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll == null) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(Detail.rectTransform);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        private void Equip()
        {
            var success = Runes.IsEquipped(Selected) == true ? Runes.TryUnequip(Selected, out var error) : Runes.TryEquip(Selected, out error);
            Message.text = success == true ? "룬 장착 상태를 반영했습니다. 다음 출전부터 적용됩니다." : error;
        }

        private void RequestPurchase()
        {
            var purchase = Runes.GetRank(Selected) == 0 ? RunePurchase.Craft : RunePurchase.Upgrade;
            if (Runes.TryQuote(Selected, purchase, out Quote, out var error) == false)
            {
                Message.text = error;
                return;
            }

            ConfirmationText.text = RunePresentation.GetName(Selected) + (purchase == RunePurchase.Craft ? " 제작" : $" Lv.{Quote.Rank} → {Quote.Rank + 1}")
                + $"\n\n{Quote.Gold:N0}골드 · 강화석 {Quote.Stones}개를 사용합니다.\n\n" + RunePresentation.Describe(RuneRules.GetDefinition(Selected), Quote.Rank + 1);
            UIPanelMotion.Show(Confirmation);
        }

        private void Confirm()
        {
            var quote = Quote;
            Quote = null;
            UIPanelMotion.Hide(Confirmation);
            Message.text = Runes.TryPurchase(quote, out var error) == true ? "룬석 성장을 반영했습니다." : error;
        }

        private void Cancel()
        {
            Runes?.CancelQuote(Quote);
            Quote = null;
            UIPanelMotion.Hide(Confirmation);
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
            if (Runes != null)
            {
                Connect();
            }
        }

        private void OnDisable()
        {
            Cancel();
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
