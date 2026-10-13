using System;
using System.Collections.Generic;
using ThreeSouls.Runes;

namespace ThreeSouls.Dungeon
{
    public enum DungeonPreparationState
    {
        Selecting,
        Preparing,
        Ready
    }

    /// <summary>
    /// 선택과 출전 준비 결과를 보관하며 변경은 담당 Manager로 제한합니다.
    /// </summary>
    public sealed class DungeonPreparationData
    {
        public string SelectedStageKey { get; private set; }
        public DungeonPreparationState State { get; private set; }
        public DungeonDeparturePlan Departure { get; private set; }

        internal void Select(string key)
        {
            SelectedStageKey = key;
            Departure = null;
            State = DungeonPreparationState.Preparing;
        }

        internal void Confirm(DungeonDeparturePlan departure)
        {
            Departure = departure;
            State = DungeonPreparationState.Ready;
        }

        internal void Invalidate()
        {
            Departure = null;
            State = SelectedStageKey == null ? DungeonPreparationState.Selecting : DungeonPreparationState.Preparing;
        }

        internal void Clear()
        {
            SelectedStageKey = null;
            Invalidate();
        }
    }

    /// <summary>
    /// 전투 이동 직전에 사용할 지역·스테이지와 고정 룬 효과입니다. 씬 이동은 요청하지 않습니다.
    /// </summary>
    public sealed class DungeonDeparturePlan
    {
        private readonly IReadOnlyList<DungeonRuneSlot> RuneSlots;
        public string StageKey { get; }
        public string DungeonName { get; }
        public string StageName { get; }
        public int RegionNumber { get; }
        public int StageNumber { get; }
        public RuneCombatSnapshot Runes { get; }

        public IReadOnlyList<DungeonRuneSlot> GetRuneSlots()
        {
            return RuneSlots;
        }

        internal DungeonDeparturePlan(string key, string dungeonName, string stageName, int region, int stage,
            RuneCombatSnapshot runes, DungeonRuneSlot[] slots)
        {
            StageKey = key;
            DungeonName = dungeonName;
            StageName = stageName;
            RegionNumber = region;
            StageNumber = stage;
            Runes = runes;
            RuneSlots = Array.AsReadOnly((DungeonRuneSlot[])slots.Clone());
        }
    }

    public readonly struct DungeonRuneSlot
    {
        public readonly string Socket;
        public readonly string RuneKey;
        public readonly int Rank;

        internal DungeonRuneSlot(string socket, string key, int rank)
        {
            Socket = socket;
            RuneKey = key;
            Rank = rank;
        }
    }
}
