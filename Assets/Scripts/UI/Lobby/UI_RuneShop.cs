using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using ThreeSouls.Runes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 룬 상품 목록, 구매·판매 확인과 기존 강화·장착 화면을 표시합니다.
    /// </summary>
    public sealed class UI_RuneShop : MonoBehaviour
    {
        [SerializeField] private Button CloseButton;
        [SerializeField] private Button PreviousButton;
        [SerializeField] private Button NextButton;
        [SerializeField] private Button BuyButton;
        [SerializeField] private Button SellButton;
        [SerializeField] private Button ManageButton;
        [SerializeField] private Button ConfirmButton;
        [SerializeField] private Button CancelButton;
        [SerializeField] private Button[] ProductButtons;
        [SerializeField] private TMP_Text[] ProductLabels;
        [SerializeField] private UnityEngine.UI.Image[] ProductImages;
        [SerializeField] private Sprite[] DefinitionIcons;
        [SerializeField] private TMP_Text Wallet;
        [SerializeField] private TMP_Text PageText;
        [SerializeField] private TMP_Text Detail;
        [SerializeField] private TMP_Text Message;
        [SerializeField] private TMP_Text ConfirmText;
        [SerializeField] private GameObject Confirmation;
        [SerializeField] private UI_RuneStones Management;
        private RuneManager Runes;
        private UserManager User;
        private RuneTableDataRow[] Products;
        private int Page;
        private string Selected;
        private RuneQuote BuyQuote;
        private RuneSaleQuote SaleQuote;

        public bool IsShowing() => gameObject.activeSelf;

        public void SetUp(RuneManager runes, UserManager user)
        {
            Runes = runes;
            User = user;
            Products = LobbyTables.Get<RuneTableDataTable>().Items.Where(x => x.Enabled == true).ToArray();
            Selected = Products[0].Key;
            CloseButton.onClick.AddListener(Close);
            PreviousButton.onClick.AddListener(() => ChangePage(-1));
            NextButton.onClick.AddListener(() => ChangePage(1));
            BuyButton.onClick.AddListener(QuoteBuy);
            SellButton.onClick.AddListener(QuoteSale);
            ManageButton.onClick.AddListener(Management.Open);
            ConfirmButton.onClick.AddListener(Confirm);
            CancelButton.onClick.AddListener(Cancel);
            for (var i = 0; i < ProductButtons.Length; i++)
            {
                var index = i;
                ProductButtons[i].onClick.AddListener(() => Select(index));
            }

            Management.SetUp(runes, user);
            if (isActiveAndEnabled == true)
            {
                ConnectEvents();
            }
            Refresh();
        }

        public void Open()
        {
            Cancel();
            Message.text = "루아: 작은 룬에도 함께 돌아올 힘이 담겨 있어요.\n구매에는 골드·강화석이 필요해요. 판매는 골드만 돌려드려요.";
            UIPanelMotion.Show(gameObject);
            transform.SetAsLastSibling();
            User.GetQuestManager().VisitRuneShop();
            Refresh();
        }

        public void Close()
        {
            Cancel();
            Management.Close();
            UIPanelMotion.Hide(gameObject);
        }

        private void ChangePage(int delta)
        {
            Cancel();
            Page = Mathf.Clamp(Page + delta, 0, (Products.Length - 1) / ProductButtons.Length);
            Selected = Products[Page * ProductButtons.Length].Key;
            Refresh();
        }

        private void Select(int index)
        {
            Cancel();
            Selected = Products[Page * ProductButtons.Length + index].Key;
            Refresh();
        }

        private void Refresh()
        {
            Wallet.text = $"골드 {User.UData.Gold:N0}  ·  강화석 {User.UData.EnhancementStones:N0}";
            PageText.text = $"{Page + 1} / {(Products.Length - 1) / ProductButtons.Length + 1}";
            PreviousButton.interactable = Page > 0;
            NextButton.interactable = (Page + 1) * ProductButtons.Length < Products.Length;
            for (var i = 0; i < ProductButtons.Length; i++)
            {
                var index = Page * ProductButtons.Length + i;
                ProductButtons[i].gameObject.SetActive(index < Products.Length);
                if (index >= Products.Length)
                {
                    continue;
                }

                var row = Products[index];
                var rank = Runes.GetRank(row.Key);
                var iconIndex = System.Array.IndexOf(LobbyTables.Get<RuneTableDataTable>().Items, row);
                if (ProductImages != null && i < ProductImages.Length && DefinitionIcons != null && iconIndex >= 0 && iconIndex < DefinitionIcons.Length)
                {
                    ProductImages[i].sprite = DefinitionIcons[iconIndex];
                }
                ProductLabels[i].text = $"{(Selected == row.Key ? "◆ " : "")}{row.DisplayName}\n{(rank == 0 ? $"{row.CraftGold:N0}골드 · 강화석 {row.CraftStones}" : $"보유 {rank}단계{(Runes.IsEquipped(row.Key) == true ? " · 장착" : "")}")}";
            }

            var selected = RuneRules.GetDefinition(Selected);
            var level = Runes.GetRank(Selected);
            Detail.text = (level == 0 && User.UData.Campaign.ClearedProgress < CampaignBalance.RuneUnlock(Selected) ? CampaignBalance.UnlockLabel(CampaignBalance.RuneUnlock(Selected)) + "\n\n" : "") + RunePresentation.Describe(selected, level);
            BuyButton.interactable = Runes.CanPurchase(Selected, RunePurchase.Craft, out _);
            SellButton.interactable = level > 0 && Runes.IsEquipped(Selected) == false;
        }

        private void QuoteBuy()
        {
            Cancel();
            if (Runes.TryQuote(Selected, RunePurchase.Craft, out BuyQuote, out var error) == false)
            {
                Message.text = error;
                return;
            }

            ConfirmText.text = $"{RuneRules.GetDefinition(Selected).DisplayName} 구매\n\n{BuyQuote.Gold:N0}골드 · 강화석 {BuyQuote.Stones}개\n\n1단계 룬을 얻습니다. 장착은 강화·장착에서 선택하세요.";
            UIPanelMotion.Show(Confirmation);
        }

        private void QuoteSale()
        {
            Cancel();
            if (Runes.TryQuoteSale(Selected, out SaleQuote, out var error) == false)
            {
                Message.text = error;
                return;
            }

            ConfirmText.text = $"{RuneRules.GetDefinition(Selected).DisplayName} {SaleQuote.Rank}단계 판매\n\n{SaleQuote.Gold:N0}골드를 받습니다.\n누적 골드 비용의 {LobbyTables.Get<RuneShopRuleTableDataTable>().Items[0].SaleGoldRatio:P0} 환급\n\n룬과 강화 단계가 사라집니다.\n강화석은 반환되지 않습니다.";
            UIPanelMotion.Show(Confirmation);
        }

        private void Confirm()
        {
            string error;
            var success = SaleQuote != null ? Runes.TrySell(SaleQuote, out error) : Runes.TryPurchase(BuyQuote, out error);
            Cancel();
            Message.text = success == true ? "거래가 완료되었습니다." : error;
            Refresh();
        }

        private void Cancel()
        {
            Runes.CancelQuote(BuyQuote);
            Runes.CancelSaleQuote(SaleQuote);
            BuyQuote = null;
            SaleQuote = null;
            UIPanelMotion.Hide(Confirmation);
        }

        private void CurrencyChanged(int gold, int chests)
        {
            Refresh();
        }

        private void StonesChanged(int stones)
        {
            Refresh();
        }

        private void OnDestroy()
        {
            DisconnectEvents();
        }

        private void ConnectEvents()
        {
            Runes.OnRunesChangedEventHandler += Refresh;
            User.OnCombatRewardChangedEventHandler += CurrencyChanged;
            User.OnEnhancementMaterialsChangedEventHandler += StonesChanged;
        }

        private void DisconnectEvents()
        {
            if (Runes != null)
            {
                Runes.CancelQuote(BuyQuote);
                Runes.CancelSaleQuote(SaleQuote);
                BuyQuote = null;
                SaleQuote = null;
                Runes.OnRunesChangedEventHandler -= Refresh;
                User.OnCombatRewardChangedEventHandler -= CurrencyChanged;
                User.OnEnhancementMaterialsChangedEventHandler -= StonesChanged;
            }
        }

        private void OnEnable()
        {
            if (Runes != null)
            {
                ConnectEvents();
                Refresh();
            }
        }

        private void OnDisable()
        {
            DisconnectEvents();
        }
    }
}
