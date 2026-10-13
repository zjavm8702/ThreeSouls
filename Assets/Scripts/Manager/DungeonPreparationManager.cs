using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Dungeon;
using ThreeSouls.Runes;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 스테이지 선택과 출전 준비 확정을 담당합니다. 재화 소비·전투 잠금·씬 이동은 하지 않습니다.
    /// </summary>
    public sealed class DungeonPreparationManager
    {
        public delegate void OnPreparationChangedDelegate();
        private OnPreparationChangedDelegate onPreparationChangedEventHandler;
        public event OnPreparationChangedDelegate OnPreparationChangedEventHandler
        {
            add
            {
                onPreparationChangedEventHandler -= value;
                onPreparationChangedEventHandler += value;
            }
            remove
            {
                onPreparationChangedEventHandler -= value;
            }
        }

        private readonly DungeonPreparationData Data;
        private readonly RuneManager Runes;
        private bool Ready;
        private bool LobbySession;

        internal DungeonPreparationManager(DungeonPreparationData data, RuneManager runes)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Runes = runes ?? throw new ArgumentNullException(nameof(runes));
        }

        internal void ApplyTableRules()
        {
            DungeonSelectionRules.Validate();
            Ready = true;
        }

        public void BeginLobbySession()
        {
            RequireReady();
            Data.Clear();
            LobbySession = true;
            Runes.OnRunesChangedEventHandler += OnRunesChanged;
        }

        public bool TrySelectStage(string key, out string error)
        {
            if (CanChange(out error) == false)
            {
                return false;
            }

            var stage = DungeonSelectionRules.GetStage(key);
            if (stage == null || stage.Enabled == false)
            {
                error = "선택 가능한 스테이지를 눌러 주세요.";
                return false;
            }

            var map = DungeonSelectionRules.GetDungeon(stage.DungeonKey);
            if (!CampaignBalance.CanEnter(UserManager.Get(), map.RegionNumber, stage.StageNumber, out error))
            {
                return false;
            }
            Data.Select(stage.Key);
            error = string.Empty;
            onPreparationChangedEventHandler?.Invoke();
            return true;
        }

        public bool TryPrepareDeparture(out string error)
        {
            if (CanChange(out error) == false)
            {
                return false;
            }

            if (Data.State != DungeonPreparationState.Preparing)
            {
                error = Data.State == DungeonPreparationState.Ready ? "이미 출전 준비를 완료했습니다." : "스테이지를 먼저 선택하세요.";
                return false;
            }

            var stage = DungeonSelectionRules.GetStage(Data.SelectedStageKey);
            var map = stage == null ? null : DungeonSelectionRules.GetDungeon(stage.DungeonKey);
            if (stage == null || stage.Enabled == false || map == null)
            {
                Data.Clear();
                error = "선택한 스테이지가 변경됐습니다. 다시 선택하세요.";
                onPreparationChangedEventHandler?.Invoke();
                return false;
            }

            var plan = new DungeonDeparturePlan(stage.Key, map.DisplayName, stage.DisplayName,
                map.RegionNumber, stage.StageNumber, Runes.CreatePreview(), CaptureRuneSlots());
            Data.Confirm(plan);
            error = string.Empty;
            onPreparationChangedEventHandler?.Invoke();
            return true;
        }

        public bool TryResumePreparation(out string error)
        {
            if (CanChange(out error) == false)
            {
                return false;
            }

            if (Data.State != DungeonPreparationState.Ready)
            {
                error = "수정할 출전 준비가 없습니다.";
                return false;
            }

            Data.Invalidate();
            error = string.Empty;
            onPreparationChangedEventHandler?.Invoke();
            return true;
        }

        public void CancelSelection()
        {
            Data.Clear();
            onPreparationChangedEventHandler?.Invoke();
        }

        public void EndLobbySession()
        {
            Runes.OnRunesChangedEventHandler -= OnRunesChanged;
            LobbySession = false;
            Data.Clear();
        }

        internal void Release()
        {
            EndLobbySession();
            Ready = false;
            onPreparationChangedEventHandler = null;
        }

        private void OnRunesChanged()
        {
            if (Data.SelectedStageKey != null)
            {
                Data.Invalidate();
                onPreparationChangedEventHandler?.Invoke();
            }
        }

        private DungeonRuneSlot[] CaptureRuneSlots()
        {
            var rows = LobbyTables.Get<RuneTableDataTable>().Items;
            var sockets = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<DungeonRuneSlot>();
            for (var i = 0; i < rows.Length; i++)
            {
                var socket = RuneRules.GetSocket(rows[i]);
                if (rows[i].Enabled == true && sockets.Add(socket) == true)
                {
                    var key = Runes.GetEquipped(socket);
                    result.Add(new DungeonRuneSlot(socket, key, key == null ? 0 : Runes.GetRank(key)));
                }
            }

            return result.ToArray();
        }

        private bool CanChange(out string error)
        {
            RequireReady();
            error = LobbySession == true ? string.Empty : "출전 준비는 마을에서만 가능합니다.";
            return LobbySession;
        }

        private void RequireReady()
        {
            if (Ready == false)
            {
                throw new InvalidOperationException("던전 선택 테이블을 적용하세요.");
            }
        }
    }
}
