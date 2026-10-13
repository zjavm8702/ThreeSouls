using System;
using ThreeSouls.Data;
using System.Text;
using ThreeSouls.Dungeon;
using ThreeSouls.Manager;
using ThreeSouls.Runes;
using TMPro;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 선택된 스테이지의 룬 화면과 출전 준비 결과를 표시합니다.
    /// </summary>
    public sealed class UI_DungeonPreparation : MonoBehaviour
    {
        [SerializeField] private UI_RuneStones RuneView;
        [SerializeField] private TMP_Text Title;
        [SerializeField] private TMP_Text Message;
        [SerializeField] private TMP_Text ReadySummary;
        [SerializeField] private GameObject ReadyCard;
        [SerializeField] private UnityEngine.UI.Button StartButton;
        [SerializeField] private TMP_Text StartLabel;
        [SerializeField] private UnityEngine.UI.Button ResumeButton;
        [SerializeField] private UnityEngine.UI.Button CancelButton;
        private DungeonPreparationManager Preparation;
        private DungeonPreparationData Data;
        private bool Bound;
        private DungeonTravelManager Travel;

        public void ConnectTravel(DungeonTravelManager travel)
        {
            Travel = travel ?? throw new ArgumentNullException(nameof(travel));
        }

        public void SetUp(DungeonPreparationManager preparation, RuneManager runes, UserManager user)
        {
            Disconnect();
            Preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            Data = user.UData.DungeonPreparation;
            RuneView.SetUp(runes, user);
            if (Bound == false)
            {
                StartButton.onClick.AddListener(PrepareDeparture);
                ResumeButton.onClick.AddListener(Resume);
                CancelButton.onClick.AddListener(Close);
                Bound = true;
            }

            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        public void Open()
        {
            if (Preparation == null || Data.SelectedStageKey == null)
            {
                throw new InvalidOperationException("스테이지를 선택한 뒤 준비 화면을 여세요.");
            }

            UIPanelMotion.Show(gameObject);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            UIPanelMotion.Hide(gameObject);
        }

        private void PrepareDeparture()
        {
            if (Preparation.TryPrepareDeparture(out var error) == false)
            {
                Message.text = error;
                return;
            }
            if (Travel != null && Travel.TryDepart(Data.Departure, out error) == false)
            {
                Preparation.TryResumePreparation(out _);
                Message.text = error;
                return;
            }
            Message.text = Travel == null ? "출전 준비 완료. 선택한 스테이지와 룬 구성을 확인하세요." : "전투를 준비하고 있습니다.";
        }

        private void Resume()
        {
            Message.text = Preparation.TryResumePreparation(out var error) == true
                ? "룬석을 변경하고 전투 시작을 다시 눌러 주세요." : error;
        }

        private void Refresh()
        {
            var stage = DungeonSelectionRules.GetStage(Data.SelectedStageKey);
            if (stage == null)
            {
                Close();
                return;
            }

            var map = DungeonSelectionRules.GetDungeon(stage.DungeonKey);
            Title.text = $"{map.DisplayName} · {map.RegionNumber}-{stage.StageNumber}\n{stage.DisplayName}";
            var balance = CampaignBalance.GetStage(map.RegionNumber, stage.StageNumber);
            Message.text = $"부대 Lv.{balance.RequiredHeroLevel} · 권장 장비 +{balance.RecommendedUpgrade} · 일반 장비 확률 {balance.EquipmentChance:P1}\n승리: 강화석 +{balance.EnhancementStones} · 룬 발견 {balance.RuneChance:P1}";
            var ready = Data.State == DungeonPreparationState.Ready;
            StartButton.interactable = ready == false;
            StartLabel.text = ready == true ? "출전 준비 완료" : "전투 시작";
            ResumeButton.gameObject.SetActive(ready);
            ReadyCard.SetActive(ready);
            if (ready == true)
            {
                RuneView.Close();
                ReadySummary.text = DescribeDeparture(Data.Departure);
            }
            else
            {
                RuneView.Open();
            }
        }

        private static string DescribeDeparture(DungeonDeparturePlan plan)
        {
            var text = new StringBuilder("룬 구성 확정\n\n");
            text.AppendLine($"성소 장비 +{plan.Runes.EquipmentDropMultiplier - 1:P0} · 골드 +{plan.Runes.GoldMultiplier - 1:P0}");
            text.AppendLine($"무작위 능력 감소 -{plan.Runes.DebuffRatio:P0} · 보석 준비 중\n");
            var slots = plan.GetRuneSlots();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var label = slot.RuneKey == null ? "미장착" : RunePresentation.GetName(slot.RuneKey) + " Lv." + slot.Rank;
                text.AppendLine(GetSocketName(slot.Socket) + " · " + label);
            }

            text.Append("\n승리하면 장비와 성장 재료가 지급됩니다.");
            return text.ToString();
        }

        private static string GetSocketName(string socket)
        {
            const string sanctuaryPrefix = "Sanctuary/";
            if (socket.StartsWith(sanctuaryPrefix, StringComparison.Ordinal) == true)
            {
                var effect = Enum.Parse<RuneEffect>(socket.Substring(sanctuaryPrefix.Length));
                return effect switch
                {
                    RuneEffect.EquipmentDrop => "성소 장비 드롭",
                    RuneEffect.Gold => "성소 골드",
                    RuneEffect.DebuffWard => "성소 디버프 완화",
                    _ => throw new InvalidOperationException("출전 준비에 지원하지 않는 성소 슬롯입니다.")
                };
            }

            var definition = RuneRules.GetDefinition(socket.Substring("Skill/".Length) + "_Power");
            return definition.DisplayName.Split('·')[0].Trim();
        }

        private void Connect()
        {
            Refresh();
            Preparation.OnPreparationChangedEventHandler += Refresh;
        }

        private void Disconnect()
        {
            if (Preparation != null)
            {
                Preparation.OnPreparationChangedEventHandler -= Refresh;
            }
        }

        private void OnEnable()
        {
            if (Preparation != null)
            {
                Message.text = "성소와 스킬 룬을 장착한 뒤 전투 시작을 눌러 주세요.";
                Connect();
            }
        }

        private void OnDisable()
        {
            Disconnect();
            RuneView.Close();
            if (Preparation != null)
            {
                Preparation.CancelSelection();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
