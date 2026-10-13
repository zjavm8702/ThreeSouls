using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using ThreeSouls.Village;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 현재 이야기 안내와 NPC의 수락·진행·보고 대화를 표시합니다.
    /// </summary>
    public sealed class UI_StoryQuest : MonoBehaviour
    {
        [SerializeField] private GameObject DialoguePanel;
        [SerializeField] private TMP_Text Tracker;
        [SerializeField] private TMP_Text Speaker;
        [SerializeField] private UnityEngine.UI.Image Portrait;
        [SerializeField] private Sprite[] NpcPortraits;
        [SerializeField] private Sprite JournalIcon;
        [SerializeField] private TMP_Text Title;
        [SerializeField] private TMP_Text Dialogue;
        [SerializeField] private TMP_Text Objective;
        [SerializeField] private TMP_Text Reward;
        [SerializeField] private TMP_Text ActionLabel;
        [SerializeField] private Button TrackerButton;
        [SerializeField] private Button CloseButton;
        [SerializeField] private Button ActionButton;
        [SerializeField] private Button MapButton;
        private QuestManager Quests;
        private VillageNpcInteraction Interaction;
        private UI_DungeonSelection Map;
        private string Giver;
        private bool JournalMode;
        private StoryQuestTableDataRow Reported;

        public bool IsShowing() => DialoguePanel.activeSelf;

        public void SetUp(QuestManager quests, VillageNpcInteraction interaction, UI_DungeonSelection map)
        {
            Quests = quests;
            Interaction = interaction;
            Map = map;
            TrackerButton.onClick.AddListener(OpenJournal);
            CloseButton.onClick.AddListener(Close);
            ActionButton.onClick.AddListener(Act);
            MapButton.onClick.AddListener(OpenMap);
            Quests.OnQuestChangedEventHandler += Refresh;
            Refresh();
        }

        public void OpenNpc(string giver)
        {
            Giver = giver;
            JournalMode = false;
            Reported = null;
            UIPanelMotion.Show(DialoguePanel);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void OpenJournal()
        {
            JournalMode = true;
            Reported = null;
            UIPanelMotion.Show(DialoguePanel);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            UIPanelMotion.Hide(DialoguePanel);
            Reported = null;
        }

        private void Refresh()
        {
            var row = Quests.GetCurrent();
            var journal = Quests.GetJournal();
            if (Portrait != null)
            {
                var portraitGiver = JournalMode ? row?.GiverKey : Giver;
                var portraitIndex = portraitGiver switch { "Quest_1" => 0, "Quest_2" => 1, "Quest_3" => 2, _ => 3 };
                Portrait.sprite = row == null ? JournalIcon : NpcPortraits[portraitIndex];
                Portrait.preserveAspect = true;
            }
            var state = journal.Accepted == false ? "의뢰 가능" : journal.ObjectiveComplete == true ? "보고 가능" : "진행 중";
            Tracker.text = row == null ? "세 영혼의 새벽\n이야기 완료 · 다시 도전할 수 있어요" : $"{row.Chapter}\n{row.Title} · {state}\n{(journal.Accepted == false ? row.GiverName + "에게 대화" : journal.ObjectiveComplete == true ? "재화 사용 후 보상 받기" : row.ObjectiveText)}";
            if (IsShowing() == false)
            {
                return;
            }

            if (Reported != null)
            {
                Speaker.text = Reported.GiverName + " · 보고 완료";
                Title.text = Reported.Title;
                Dialogue.text = Reported.CompleteDialogue;
                Objective.text = row == null ? "마을에 새 아침이 찾아왔습니다." : $"다음: {row.Title}\n{row.GiverName}에게 말을 걸어 주세요.";
                Reward.text = $"받은 보상: {Reported.RewardGold:N0}골드 · 강화석 {Reported.RewardStones}";
                ActionButton.interactable = true;
                ActionLabel.text = "다음 이야기";
                MapButton.gameObject.SetActive(false);
                return;
            }

            if (row == null)
            {
                Speaker.text = "성소 마을";
                Title.text = "세 영혼의 새벽";
                Dialogue.text = "용기와 믿음, 돌봄과 희망이 다시 이어졌습니다. 세 영웅은 마을로 돌아와 새로운 아침을 맞습니다.";
                Objective.text = $"이야기 {journal.CompletedCount}/{Quests.GetTotalCount()} 완료";
                Reward.text = "던전과 룬 준비는 계속 이용할 수 있어요.";
                ActionButton.interactable = false;
                ActionLabel.text = "이야기 완료";
                MapButton.gameObject.SetActive(true);
                return;
            }

            Speaker.text = JournalMode == true ? $"이야기 수첩 · {journal.CompletedCount}/{Quests.GetTotalCount()}" : GetNpcName(Giver);
            Title.text = row.Chapter + "\n" + row.Title;
            Objective.text = "목표: " + row.ObjectiveText;
            Reward.text = $"자동 완료 보상: {row.RewardGold:N0}골드 · 강화석 {row.RewardStones}";
            var correctNpc = Giver == row.GiverKey;
            if (JournalMode == true)
            {
                Dialogue.text = journal.Accepted == false ? row.OfferDialogue : journal.ObjectiveComplete == true
                    ? "목표를 완료했습니다. 재화를 사용하면 보상이 자동 지급됩니다." : row.ActiveDialogue;
                ActionLabel.text = journal.Accepted == false ? row.GiverName + " 찾아가기"
                    : journal.ObjectiveComplete == true ? "자동 보상 대기" : "목표 진행 중";
                ActionButton.interactable = journal.Accepted == false;
            }
            else if (correctNpc == false)
            {
                Dialogue.text = $"지금은 {row.GiverName}의 이야기를 이어갈 때예요.\n\n{row.Title} · {state}\n마을에서 그분을 만나 주세요.";
                ActionLabel.text = "현재 의뢰 NPC 찾기";
                ActionButton.interactable = true;
            }
            else
            {
                Dialogue.text = journal.Accepted == false ? row.OfferDialogue : journal.ObjectiveComplete == true ? "목표를 마쳤군요. 이야기를 듣고 보상을 받아 주세요." : row.ActiveDialogue;
                ActionLabel.text = journal.Accepted == false ? "의뢰 수락" : journal.ObjectiveComplete == true ? "보고 · 보상 받기" : "목표 진행 중";
                ActionButton.interactable = journal.Accepted == false || journal.ObjectiveComplete == true;
            }

            MapButton.gameObject.SetActive(journal.Accepted == true && journal.ObjectiveComplete == false && row.Objective == "Victory");
        }

        private static string GetNpcName(string key)
        {
            return key switch
            {
                "Quest_1" => "정찰꾼 린",
                "Quest_2" => "기록관 오르",
                "Quest_3" => "약초사 미아",
                _ => "등불지기 세라"
            };
        }

        private void Act()
        {
            var row = Quests.GetCurrent();
            if (Reported != null)
            {
                Reported = null;
                if (row != null && row.GiverKey == Giver)
                {
                    Refresh();
                }
                else
                {
                    Close();
                }

                return;
            }

            if (row == null)
            {
                return;
            }

            if (JournalMode == true || Giver != row.GiverKey)
            {
                if (JournalMode && Quests.GetJournal().Accepted) return;
                Reported = null;
                UIPanelMotion.Hide(DialoguePanel, () => Interaction.RequestTalk(row.GiverKey));
                return;
            }

            if (Quests.GetJournal().Accepted == false)
            {
                if (Quests.TryAccept(Giver, row.Key, out var error) == false)
                {
                    Dialogue.text = error;
                }
            }
            else if (Quests.TryReport(row.GiverKey, row.Key, out var error) == true)
            {
                Reported = row;
                Refresh();
            }
            else
            {
                Dialogue.text = error;
            }
        }

        private void OpenMap()
        {
            Close();
            Interaction.CancelPending();
            Map.Open();
        }

        private void OnDestroy()
        {
            if (Quests != null)
            {
                Quests.OnQuestChangedEventHandler -= Refresh;
            }
        }

        private void OnEnable()
        {
            if (Quests != null)
            {
                Quests.OnQuestChangedEventHandler += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (Quests != null)
            {
                Quests.OnQuestChangedEventHandler -= Refresh;
            }
        }
    }
}
