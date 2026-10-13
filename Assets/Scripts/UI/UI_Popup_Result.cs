using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 확정된 승리·패배 결과와 획득 보상을 표시하고 닫기 요청을 전달합니다.
    /// </summary>
    public sealed class UI_Popup_Result : BasePopupUI
    {
        public delegate void OnConfirmRequestedDelegate();
        private OnConfirmRequestedDelegate onConfirmRequestedEventHandler;
        public event OnConfirmRequestedDelegate OnConfirmRequestedEventHandler
        {
            add
            {
                onConfirmRequestedEventHandler -= value;
                onConfirmRequestedEventHandler += value;
            }
            remove
            {
                onConfirmRequestedEventHandler -= value;
            }
        }

        [Serializable]
        private sealed class EquipmentIcon
        {
            public string ItemKey;
            public Sprite Icon;
        }

        [SerializeField] private CanvasGroup DimGroup;
        [SerializeField] private CanvasGroup ContentsGroup;
        [SerializeField] private RectTransform Title;
        [SerializeField] private RectTransform GoldIcon;
        [SerializeField] private Text OutcomeLabel;
        [SerializeField] private Text OutcomeTitle;
        [SerializeField] private Image OutcomeBanner;
        [SerializeField] private Image PanelFrame;
        [SerializeField] private Image TopAccent;
        [SerializeField] private Image[] TitleDecorations = Array.Empty<Image>();
        [SerializeField] private Text StageText;
        [SerializeField] private Text ClearTimeText;
        [SerializeField] private Text GoldText;
        [SerializeField] private Text ItemCountText;
        [SerializeField] private GameObject EmptyItems;
        [SerializeField] private Text EmptyItemsTitle;
        [SerializeField] private Text EmptyItemsHint;
        [SerializeField] private Text RewardNotice;
        [SerializeField] private ScrollRect ItemScroll;
        [SerializeField] private RectTransform ItemContainer;
        [SerializeField] private UI_Item_ResultReward ItemTemplate;
        [SerializeField] private Button ConfirmButton;
        [SerializeField] private EquipmentIcon[] ItemIcons = Array.Empty<EquipmentIcon>();

        private readonly List<UI_Item_ResultReward> ItemViews = new();
        private readonly List<UI_Item_ResultReward> ActiveItems = new();
        private CombatResultTween Presentation;

        public override void Initialize()
        {
            base.Initialize();
            Presentation ??= new CombatResultTween(DimGroup, ContentsGroup, (RectTransform)Contents.transform, Title, GoldIcon);
            ConfirmButton.onClick.RemoveListener(RequestConfirm);
            ConfirmButton.onClick.AddListener(RequestConfirm);
            ItemTemplate.gameObject.SetActive(false);
        }

        public void ShowResult(CombatResultData result, CombatOutcome outcome, bool playAnimation = true)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            Presentation.Clear();
            ConfirmButton.interactable = true;
            BindOutcome(outcome);
            var victory = outcome == CombatOutcome.Victory;
            GetComponentInChildren<ThreeSouls.Ads.UI_RewardedOffer>(true)?.BindResult(result,victory);
            if (victory == true && (result.HeroExperience > 0 || result.GrowthStones > 0))
            {
                RewardNotice.text = $"영웅별 경험치 +{result.HeroExperience:N0}  ·  성장석 +{result.GrowthStones:N0} · 강화석 +{result.EnhancementStones:N0}" + (string.IsNullOrEmpty(result.RuneReward) ? "" : "\n" + result.RuneReward);
            }

            var gold = victory == true ? result.Gold : 0;
            var itemCount = victory == true ? result.ItemCount : 0;
            StageText.text = $"{result.RegionNumber}지역  ·  {result.StageNumber}스테이지";
            var seconds = Mathf.FloorToInt(result.ElapsedSeconds);
            ClearTimeText.text = $"전투 시간  {seconds / 60:00}:{seconds % 60:00}";
            ItemCountText.text = $"총 {itemCount:N0}개";
            BindItems(victory == true ? result.GetItems() : Array.Empty<CombatResultItem>());
            EmptyItems.SetActive(itemCount == 0);
            base.Show(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(ItemContainer);
            ItemScroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            ItemScroll.verticalNormalizedPosition = 1f;

            if (playAnimation == true)
            {
                Presentation.Play(gold, DisplayGold, ActiveItems, outcome);
            }
            else
            {
                DisplayGold(gold);
            }
        }

        public override void Hide(bool playAnimation = true)
        {
            Presentation?.Clear();
            base.Hide(false);
        }

        public override void Release()
        {
            Presentation?.Clear();
            ConfirmButton.onClick.RemoveListener(RequestConfirm);
            onConfirmRequestedEventHandler = null;
        }

        public override void OnEscapeEvent()
        {
            RequestConfirm();
        }

        protected override void OnDisable()
        {
            Presentation?.Clear();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Release();
            base.OnDestroy();
        }

        private void BindItems(IReadOnlyList<CombatResultItem> items)
        {
            ActiveItems.Clear();

            for (var i = 0; i < ItemViews.Count; i++)
            {
                ItemViews[i].gameObject.SetActive(false);
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var definition = TableManager.Get().GetEquipmentTableData(item.ItemKey);

                if (definition == null)
                {
                    throw new InvalidOperationException($"결과창 장비 테이블 누락 : {item.ItemKey}");
                }

                var view = i < ItemViews.Count ? ItemViews[i] : Instantiate(ItemTemplate, ItemContainer, false);

                if (i >= ItemViews.Count)
                {
                    ItemViews.Add(view);
                }

                view.gameObject.SetActive(true);
                view.Bind(GetItemIcon(item.ItemKey), TableManager.Get().GetEquipmentName(item.ItemKey), definition.Rarity, item.Count);
                ActiveItems.Add(view);
            }
        }

        private void BindOutcome(CombatOutcome outcome)
        {
            var victory = outcome == CombatOutcome.Victory;
            var accent = victory == true ? new Color(1f, 0.82f, 0.35f) : new Color(1f, 0.42f, 0.39f);
            OutcomeLabel.text = victory == true ? "던전 클리어" : "던전 도전 실패";
            EmptyItemsTitle.text = victory == true ? "획득한 아이템이 없습니다" : "획득한 보상이 없습니다";
            EmptyItemsHint.text = victory == true ? "다음 던전에서 새로운 장비를 찾아보세요" : "던전 클리어 시 보상을 받을 수 있습니다";
            RewardNotice.text = victory == true ? "획득한 보상이 반영되었습니다" : "패배 시 골드와 아이템은 지급되지 않습니다";
            OutcomeTitle.text = victory == true ? "VICTORY" : "DEFEAT";
            OutcomeTitle.color = accent;
            TopAccent.color = accent;
            PanelFrame.color = victory == true ? new Color(0.55f, 0.52f, 0.69f) : new Color(0.58f, 0.40f, 0.48f);
            OutcomeBanner.color = victory == true ? new Color(0.86f, 0.82f, 1f) : new Color(0.76f, 0.47f, 0.53f);

            for (var i = 0; i < TitleDecorations.Length; i++)
            {
                TitleDecorations[i].color = accent;
                TitleDecorations[i].gameObject.SetActive(victory);
            }
        }

        private Sprite GetItemIcon(string itemKey)
        {
            return LobbyPresentationData.GetItemIcon(itemKey);
        }

        private void DisplayGold(int gold)
        {
            GoldText.text = gold.ToString("N0");
        }

        private void RequestConfirm()
        {
            if(ThreeSouls.Ads.RewardedAds.Current?.Busy==true)return;
            if (ConfirmButton.interactable == false) return;
            if (Application.isPlaying == true && Presentation != null)
            {
                ConfirmButton.interactable = false;
                Presentation.PlayExit(() => onConfirmRequestedEventHandler?.Invoke());
            }
            else
            {
                onConfirmRequestedEventHandler?.Invoke();
            }
        }
    }
}
