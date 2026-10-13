using System;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Quests;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// NPC 이야기 수락, 목표 검증과 자동 완료 보상을 관리합니다.
    /// </summary>
    public sealed class QuestManager
    {
        public delegate void OnQuestChangedDelegate();
        private OnQuestChangedDelegate onQuestChangedEventHandler;
        public event OnQuestChangedDelegate OnQuestChangedEventHandler
        {
            add
            {
                onQuestChangedEventHandler -= value;
                onQuestChangedEventHandler += value;
            }
            remove
            {
                onQuestChangedEventHandler -= value;
            }
        }

        private readonly UserManager User;
        private readonly QuestJournal Journal;
        private StoryQuestTableDataRow[] Rows;
        private bool Lobby;

        internal QuestManager(UserManager user, QuestJournal journal)
        {
            User = user;
            Journal = journal;
        }

        public QuestJournal GetJournal() => Journal;
        public StoryQuestTableDataRow GetCurrent() => Journal.CompletedCount < Rows.Length ? Rows[Journal.CompletedCount] : null;
        public int GetTotalCount() => Rows.Length;

        internal void ApplyTableRules()
        {
            Rows = LobbyTables.Get<StoryQuestTableDataTable>().Items;
            var stages = LobbyTables.Get<DungeonStageSelectionTableDataTable>().Items;
            var maps = LobbyTables.Get<DungeonMapTableDataTable>().Items;
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < Rows.Length; i++)
            {
                var row = Rows[i];
                if (row.Order != i || keys.Add(row.Key) == false || row.RewardGold <= 0 || row.RewardStones < 0 ||
                    string.IsNullOrEmpty(row.OfferDialogue) == true || string.IsNullOrEmpty(row.CompleteDialogue) == true ||
                    row.GiverKey != "Quest_1" && row.GiverKey != "Quest_2" && row.GiverKey != "Quest_3" && row.GiverKey != "Quest_4")
                {
                    throw new InvalidOperationException("이야기 테이블의 순서/NPC/보상을 확인하세요: " + row.Key);
                }

                if (row.Objective == "Victory")
                {
                    var found = false;
                    string dungeon = null;
                    for (var j = 0; j < maps.Length; j++)
                    {
                        if (maps[j].RegionNumber == row.RegionNumber)
                        {
                            dungeon = maps[j].Key;
                        }
                    }
                    for (var j = 0; j < stages.Length; j++)
                    {
                        if (stages[j].DungeonKey == dungeon && stages[j].StageNumber == row.StageNumber && stages[j].Enabled == true)
                        {
                            found = true;
                        }
                    }

                    if (found == false)
                    {
                        throw new InvalidOperationException("이야기의 던전 스테이지가 없습니다: " + row.Key);
                    }
                }
                else if (row.Objective != "RuneShop" && row.Objective != "EquipWard" && row.Objective != "Talk")
                {
                    throw new InvalidOperationException("알 수 없는 이야기 목표: " + row.Objective);
                }
            }

            User.GetRuneManager().OnRunesChangedEventHandler += CheckWard;
            User.OnCombatRewardChangedEventHandler += OnGoldChanged;
            User.OnEnhancementMaterialsChangedEventHandler += OnStonesChanged;
        }

        public void BeginLobbySession()
        {
            Lobby = true;
            if (Journal.ObjectiveComplete) TryClaimReward(out _);
            CheckWard();
        }

        public void EndLobbySession()
        {
            Lobby = false;
        }

        public bool TryAccept(string giver, string key, out string error)
        {
            var row = GetCurrent();
            if (Lobby == false || row == null || row.GiverKey != giver || row.Key != key || Journal.Accepted == true)
            {
                error = "현재 의뢰의 NPC에게 말을 걸어 주세요.";
                return false;
            }

            Journal.Accept();
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Book);
            if (row.Objective == "Talk")
            {
                Journal.CompleteObjective();
                TryClaimReward(out _);
            }

            CheckWard();
            onQuestChangedEventHandler?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool TryReport(string giver, string key, out string error)
        {
            var row = GetCurrent();
            if (Lobby == false || row == null || row.GiverKey != giver || row.Key != key ||
                Journal.Accepted == false || Journal.ObjectiveComplete == false)
            {
                error = "목표를 마친 뒤 의뢰한 NPC에게 보고하세요.";
                return false;
            }

            return TryClaimReward(out error);
        }

        private bool TryClaimReward(out string error)
        {
            var row = GetCurrent();
            if (row == null || !Journal.Accepted || !Journal.ObjectiveComplete)
            {
                error = "아직 의뢰 목표를 완료하지 않았습니다.";
                return false;
            }
            if ((long)User.UData.Gold + row.RewardGold > int.MaxValue ||
                (long)User.UData.EnhancementStones + row.RewardStones > int.MaxValue)
            {
                error = "재화가 상한에 도달했습니다. 재화를 사용하면 보상이 자동 지급됩니다.";
                return false;
            }

            Journal.Advance();
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Quest);
            User.CreditShopGold(row.RewardGold);
            User.CreditDismantleStones(row.RewardStones);
            User.NotifyEnhancementCurrencyChanged(true);
            onQuestChangedEventHandler?.Invoke();
            error = string.Empty;
            return true;
        }

        public void VisitRuneShop()
        {
            if (Lobby == true)
            {
                Complete("RuneShop", 0, 0);
            }
        }

        internal void RecordVictory(int region, int stage)
        {
            Complete("Victory", region, stage);
        }

        private void CheckWard()
        {
            if (Lobby == true && User.GetRuneManager().IsEquipped("Shrine_Ward") == true)
            {
                Complete("EquipWard", 0, 0);
            }
        }

        private void Complete(string objective, int region, int stage)
        {
            var row = GetCurrent();
            if (row == null || Journal.Accepted == false || Journal.ObjectiveComplete == true ||
                row.Objective != objective || row.RegionNumber != region || row.StageNumber != stage)
            {
                return;
            }

            Journal.CompleteObjective();
            TryClaimReward(out _);
            onQuestChangedEventHandler?.Invoke();
        }

        internal void Release()
        {
            User.GetRuneManager().OnRunesChangedEventHandler -= CheckWard;
            User.OnCombatRewardChangedEventHandler -= OnGoldChanged;
            User.OnEnhancementMaterialsChangedEventHandler -= OnStonesChanged;
            Lobby = false;
            onQuestChangedEventHandler = null;
        }

        private void OnGoldChanged(int gold, int chests)
        {
            if (Journal.ObjectiveComplete) TryClaimReward(out _);
        }

        private void OnStonesChanged(int stones)
        {
            if (Journal.ObjectiveComplete) TryClaimReward(out _);
        }
    }
}
