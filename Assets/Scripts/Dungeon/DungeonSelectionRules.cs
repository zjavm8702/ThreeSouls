using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Dungeon
{
    /// <summary>
    /// 지도 정의와 선택 가능한 스테이지를 조회하고 참조·좌표를 검증합니다.
    /// </summary>
    public static class DungeonSelectionRules
    {
        public static DungeonMapTableDataRow GetDungeon(string key)
        {
            var rows = LobbyTables.Get<DungeonMapTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Key == key)
                {
                    return rows[i];
                }
            }

            return null;
        }

        public static DungeonStageSelectionTableDataRow GetStage(string key)
        {
            var rows = LobbyTables.Get<DungeonStageSelectionTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Key == key)
                {
                    return rows[i];
                }
            }

            return null;
        }

        public static IReadOnlyList<DungeonStageSelectionTableDataRow> GetStages(string dungeonKey)
        {
            var rows = LobbyTables.Get<DungeonStageSelectionTableDataTable>().Items;
            var result = new List<DungeonStageSelectionTableDataRow>();
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].DungeonKey == dungeonKey)
                {
                    result.Add(rows[i]);
                }
            }

            result.Sort((left, right) => left.StageNumber.CompareTo(right.StageNumber));
            return result.AsReadOnly();
        }

        public static void Validate()
        {
            var maps = LobbyTables.Get<DungeonMapTableDataTable>().Items;
            var stages = LobbyTables.Get<DungeonStageSelectionTableDataTable>().Items;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var regions = new HashSet<int>();
            if (maps.Length != 4)
            {
                throw new InvalidOperationException("던전 지도 UI는 네 던전 정의가 필요합니다.");
            }

            for (var i = 0; i < maps.Length; i++)
            {
                var map = maps[i];
                if (string.IsNullOrWhiteSpace(map.Key) == true || keys.Add(map.Key) == false ||
                    map.RegionNumber <= 0 || regions.Add(map.RegionNumber) == false ||
                    string.IsNullOrWhiteSpace(map.DisplayName) == true ||
                    map.TexturePath != "Assets/Images/Map/Previews/NativeDungeon_" + map.RegionNumber + ".png" ||
                    IsCoordinate(map.WorldX) == false || IsCoordinate(map.WorldY) == false)
                {
                    throw new InvalidOperationException("던전 지도 정의 오류: " + map.Key);
                }

                var selections = GetStages(map.Key);
                if (selections.Count != 5)
                {
                    throw new InvalidOperationException("던전마다 선택 지점 다섯 개를 정의하세요: " + map.Key);
                }

                for (var j = 0; j < selections.Count; j++)
                {
                    if (selections[j].StageNumber != j + 1)
                    {
                        throw new InvalidOperationException("스테이지 번호가 중복되거나 누락됐습니다: " + map.Key);
                    }
                }
            }

            keys.Clear();
            for (var i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                if (string.IsNullOrWhiteSpace(stage.Key) == true || keys.Add(stage.Key) == false ||
                    GetDungeon(stage.DungeonKey) == null || string.IsNullOrWhiteSpace(stage.DisplayName) == true ||
                    IsCoordinate(stage.MapX) == false || IsCoordinate(stage.MapY) == false)
                {
                    throw new InvalidOperationException("스테이지 선택 정의 오류: " + stage.Key);
                }
            }
        }

        private static bool IsCoordinate(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false && value >= 0 && value <= 1;
        }
    }
}
