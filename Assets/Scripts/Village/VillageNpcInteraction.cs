using ThreeSouls.Manager;
using ThreeSouls.UI;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 주민 선택 후 실제 접근, 서비스 화면 열기와 의뢰 표시를 연결합니다.
    /// </summary>
    public sealed class VillageNpcInteraction : MonoBehaviour
    {
        [SerializeField] private VillagePartyController Party;
        [SerializeField] private VillageNpcTarget[] Targets;
        private UI_LobbyTabs Tabs;
        private UI_RuneShop RuneShop;
        private UI_StoryQuest Story;
        private UI_DungeonSelection Map;
        private VillageShopInteraction Equipment;
        private QuestManager Quests;
        private VillageNpcTarget Pending;
        private float Radius;

        public void Configure(UI_LobbyTabs tabs, UI_RuneShop runeShop, UI_StoryQuest story, UI_DungeonSelection map,
            VillageShopInteraction equipment, QuestManager quests, float radius)
        {
            Tabs = tabs;
            RuneShop = runeShop;
            Story = story;
            Map = map;
            Equipment = equipment;
            Quests = quests;
            Radius = radius;
            Quests.OnQuestChangedEventHandler += RefreshMarkers;
            Tabs.OnTabChangedEventHandler += OnTabChanged;
            RefreshMarkers();
        }

        public bool IsInputBlocked() => RuneShop.IsShowing() == true || Story.IsShowing() == true || Map.gameObject.activeSelf == true;

        public bool TryHandleWorldClick(Vector2 point)
        {
            Pending = null;
            for (var i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].ContainsPoint(point) == true)
                {
                    if (Targets[i].GetNpcKey() == "RuneShop")
                    {
                        if (Tabs.IsVillageShowing() && !IsInputBlocked() && !Equipment.IsInputBlocked())
                        {
                            CancelPending();
                            Equipment.CancelPendingOpen();
                            Party.StopMovement();
                            RuneShop.Open();
                        }
                        return true;
                    }
                    RequestTalk(Targets[i].GetNpcKey());
                    return true;
                }
            }

            return false;
        }

        public void RequestTalk(string key)
        {
            if (Tabs.IsVillageShowing() == false || IsInputBlocked() == true || Equipment.IsInputBlocked() == true)
            {
                return;
            }

            CancelPending();
            Equipment.CancelPendingOpen();
            for (var i = 0; i < Targets.Length; i++)
            {
                var target = Targets[i];
                if (target.GetNpcKey() != key)
                {
                    continue;
                }

                if (Vector2.Distance(Party.GetLeaderPosition(), target.GetApproach()) <= Radius)
                {
                    Party.StopMovement();
                    Open(target);
                }
                else if (Party.TryMoveTo(target.GetApproach()) == true)
                {
                    Pending = target;
                }

                return;
            }
        }

        public void CancelPending()
        {
            if (Pending != null)
            {
                Party.StopMovement();
            }

            Pending = null;
        }

        private void Update()
        {
            if (Pending == null)
            {
                return;
            }

            if (Tabs.IsVillageShowing() == false || IsInputBlocked() == true)
            {
                CancelPending();
                return;
            }

            if (Party.IsMoving() == false)
            {
                var target = Pending;
                Pending = null;
                if (Vector2.Distance(Party.GetLeaderPosition(), target.GetApproach()) <= Radius)
                {
                    Open(target);
                }
            }
        }

        private void Open(VillageNpcTarget target)
        {
            if (target.GetNpcKey() == "RuneShop")
            {
                RuneShop.Open();
            }
            else
            {
                Story.OpenNpc(target.GetNpcKey());
            }
        }

        private void RefreshMarkers()
        {
            var row = Quests.GetCurrent();
            var journal = Quests.GetJournal();
            for (var i = 0; i < Targets.Length; i++)
            {
                Targets[i].ShowQuestState(row?.GiverKey, journal.Accepted, journal.ObjectiveComplete);
            }
        }

        private void OnTabChanged(LobbyTab tab)
        {
            CancelPending();
            RuneShop.Close();
            Story.Close();
        }

        private void OnDestroy()
        {
            if (Quests != null)
            {
                Quests.OnQuestChangedEventHandler -= RefreshMarkers;
                Tabs.OnTabChangedEventHandler -= OnTabChanged;
            }
        }

        private void OnEnable()
        {
            if (Quests != null)
            {
                Quests.OnQuestChangedEventHandler += RefreshMarkers;
                Tabs.OnTabChangedEventHandler += OnTabChanged;
                RefreshMarkers();
            }
        }

        private void OnDisable()
        {
            Pending = null;
            if (Quests != null)
            {
                Quests.OnQuestChangedEventHandler -= RefreshMarkers;
                Tabs.OnTabChangedEventHandler -= OnTabChanged;
            }
        }
    }
}
