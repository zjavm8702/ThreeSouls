using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Dungeon;
using ThreeSouls.Manager;
using ThreeSouls.Village;
using TMPro;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 전체 지도→던전 지도→룬 준비 팝업의 표시와 선택 요청을 연결합니다.
    /// </summary>
    public sealed class UI_DungeonSelection : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image Map;
        [SerializeField] private Sprite[] MapTextures;
        [SerializeField] private Sprite[] StageTextures;
        [SerializeField] private UnityEngine.UI.Image[] ListPreviews;
        [SerializeField] private TMP_Text Title;
        [SerializeField] private TMP_Text MapTitle;
        [SerializeField] private TMP_Text Description;
        [SerializeField] private TMP_Text Hint;
        [SerializeField] private GameObject WorldNote;
        [SerializeField] private UnityEngine.UI.Button[] DungeonPoints;
        [SerializeField] private UnityEngine.UI.Button[] StagePoints;
        [SerializeField] private UnityEngine.UI.Button[] ListButtons;
        [SerializeField] private TMP_Text[] ListLabels;
        [SerializeField] private UnityEngine.UI.Button CloseButton;
        [SerializeField] private UnityEngine.UI.Button BackButton;
        [SerializeField] private UI_DungeonPreparation PreparationView;
        private DungeonPreparationManager Preparation;
        private VillageShopInteraction Shop;
        private string DungeonKey;
        private IReadOnlyList<DungeonStageSelectionTableDataRow> Stages;
        private bool Bound;

        public void SetUp(DungeonPreparationManager preparation, RuneManager runes, UserManager user, VillageShopInteraction shop)
        {
            Preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            Shop = shop;
            PreparationView.SetUp(preparation, runes, user);
            if (Bound == false)
            {
                CloseButton.onClick.AddListener(Close);
                BackButton.onClick.AddListener(ShowWorld);
                for (var i = 0; i < DungeonPoints.Length; i++)
                {
                    var index = i;
                    DungeonPoints[i].onClick.AddListener(() => SelectDungeon(index));
                }

                for (var i = 0; i < StagePoints.Length; i++)
                {
                    var index = i;
                    StagePoints[i].onClick.AddListener(() => SelectStage(index));
                    ListButtons[i].onClick.AddListener(() => SelectList(index));
                }

                Bound = true;
            }
        }

        public void Open()
        {
            if (Preparation == null)
            {
                throw new InvalidOperationException("던전 지도에 Manager를 연결하세요.");
            }

            Shop?.CancelPendingOpen();
            UIPanelMotion.Show(gameObject);
            transform.SetAsLastSibling();
            ShowWorld();
        }

        public void Close()
        {
            UIPanelMotion.Hide(gameObject);
        }

        private void ShowWorld()
        {
            PreparationView.Close();
            Preparation.CancelSelection();
            DungeonKey = null;
            Refresh();
        }

        private void SelectDungeon(int index)
        {
            var maps = LobbyTables.Get<DungeonMapTableDataTable>().Items;
            if (index < 0 || index >= maps.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            DungeonKey = maps[index].Key;
            PreparationView.Close();
            Preparation.CancelSelection();
            Refresh();
        }

        private void SelectList(int index)
        {
            if (DungeonKey == null)
            {
                SelectDungeon(index);
            }
            else
            {
                SelectStage(index);
            }
        }

        private void SelectStage(int index)
        {
            if (DungeonKey == null || index < 0 || index >= Stages.Count)
            {
                throw new InvalidOperationException("던전의 스테이지 선택점이 필요합니다.");
            }

            if (Preparation.TrySelectStage(Stages[index].Key, out var error) == false)
            {
                Hint.text = error;
                return;
            }

            PreparationView.Open();
            var dungeon = DungeonSelectionRules.GetDungeon(DungeonKey);
            var previewIndex = (dungeon.RegionNumber - 1) * 5 + Stages[index].StageNumber - 1;
            if (StageTextures != null && previewIndex < StageTextures.Length && StageTextures[previewIndex] != null)
                Map.sprite = StageTextures[previewIndex];
        }

        private void Refresh()
        {
            var world = DungeonKey == null;
            var maps = LobbyTables.Get<DungeonMapTableDataTable>().Items;
            var mapIndex = -1;
            for (var i = 0; i < maps.Length; i++)
            {
                DungeonPoints[i].gameObject.SetActive(world);
                if (maps[i].Key == DungeonKey)
                {
                    mapIndex = i;
                }
            }

            var dungeon = world == true ? null : maps[mapIndex];
            Stages = world == true ? null : DungeonSelectionRules.GetStages(DungeonKey);
            Map.sprite = MapTextures[world == true ? 0 : mapIndex + 1];
            Title.text = world == true ? "던전 지도" : dungeon.DisplayName;
            MapTitle.text = world == true ? "던전 선택" : dungeon.DisplayName;
            Description.text = world == true ? "지도의 던전 지점이나 아래 목록을 선택하세요." : dungeon.Description;
            Hint.text = world == true ? "던전 선택 → 스테이지 선택 → 룬석 장착" : "승리하면 다음 단계가 열립니다. 이전 단계에서 장비·룬을 파밍하세요.";
            WorldNote.SetActive(world);
            BackButton.gameObject.SetActive(world == false);
            for (var i = 0; i < StagePoints.Length; i++)
            {
                StagePoints[i].gameObject.SetActive(world == false);
                ListButtons[i].gameObject.SetActive(world == false || i < maps.Length);
                if (world == false)
                {
                    var stage = Stages[i];
                    if (ListPreviews != null && i < ListPreviews.Length)
                        ListPreviews[i].sprite = StageTextures[(dungeon.RegionNumber - 1) * 5 + stage.StageNumber - 1];
                    var point = (RectTransform)StagePoints[i].transform;
                    point.anchorMin = new Vector2(stage.MapX, stage.MapY);
                    point.anchorMax = point.anchorMin;
                    point.anchoredPosition = Vector2.zero;
                    StagePoints[i].GetComponentInChildren<TMP_Text>().text = dungeon.RegionNumber + "-" + stage.StageNumber;
                    var unlocked = CampaignBalance.CanEnter(UserManager.Get(), dungeon.RegionNumber, stage.StageNumber, out _);
                    StagePoints[i].interactable = stage.Enabled && unlocked;
                    ListButtons[i].interactable = stage.Enabled && unlocked;
                    ListLabels[i].text = dungeon.RegionNumber + "-" + stage.StageNumber + "   " + stage.DisplayName + " · Lv." + CampaignBalance.GetStage(dungeon.RegionNumber, stage.StageNumber).RequiredHeroLevel + (unlocked ? "   ›" : "   [잠김]");
                }
                else if (i < maps.Length)
                {
                    if (ListPreviews != null && i < ListPreviews.Length) ListPreviews[i].sprite = MapTextures[i + 1];
                    ListButtons[i].interactable = true;
                    ListLabels[i].text = "0" + maps[i].RegionNumber + "   " + maps[i].DisplayName + "   ›";
                }
            }
        }

        private void OnDisable()
        {
            PreparationView.Close();
            if (Preparation != null)
            {
                Preparation.CancelSelection();
            }
        }
    }
}
