using System;
using System.Collections.Generic;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Combat
{
    public class AStarPathfinder
    {
        public AStarPathfinder(IPathGrid grid)
        {
            CombatGrid = grid;
        }

        private readonly Vector2Int[] Directions =
        {
            new Vector2Int(0, 1), //위
            new Vector2Int(1, 0), //오
            new Vector2Int(0, -1), //아래
            new Vector2Int(-1, 0) //왼
        };
        private readonly IPathGrid CombatGrid = null;

        /// <summary>
        /// 목적지 까지 거리 찾기
        /// </summary>
        /// <param name = "startPathNode">시작 지점</param>
        /// <param name = "goalPathNode">도착 지점</param>
        /// <returns>해당 목적지 까지 가야 할 노드</returns>
        public List<PathNode> FindPath(PathNode startPathNode, PathNode goalPathNode)
        {
            return FindPath(startPathNode, goalPathNode, null, null);
        }

        /// <summary>
        /// 몸 크기로 통행을 확인하며 공격 가능한 셀까지 우회한다.
        /// </summary>
        public List<PathNode> FindPath(
            PathNode startPathNode,
            PathNode goalPathNode,
            Func<Vector2Int, bool> isDestination,
            Func<Vector2Int, Vector2Int, bool> canMove)
        {
            if (startPathNode == null || goalPathNode == null)
            {
                return null;
            }

            //탐색 예정
            var open = new List<PathNode>();
            //탐색 완료
            var close = new HashSet<Vector2Int>();
            //같은 셀의 노드를 탐색 중 재사용(Cell 값으로 비교하면 open리스트 갱신문제 등)
            var nodeContainer = new Dictionary<Vector2Int, PathNode>();

            if (CombatGrid.IsWalkable(startPathNode.CellPosition) == false || CombatGrid.IsWalkable(goalPathNode.CellPosition) == false)
            {
                return null;
            }

            startPathNode.SetPathCost(null, 0,
                isDestination == null ? GetHeuristic(startPathNode.CellPosition, goalPathNode.CellPosition) : 0);
            nodeContainer.Add(startPathNode.CellPosition, startPathNode);
            open.Add(startPathNode);

            while (open.Count > 0)
            {
                //F값이 더 낮은 값으로 정렬
                open.Sort();
                //현재 노드 설정
                var curNode = open[0];
                //탐색 노드 땡기기
                open.RemoveAt(0);
                //완료 노드 현재 노드로 추가
                close.Add(curNode.CellPosition);

                if ((isDestination == null ? curNode.CellPosition == goalPathNode.CellPosition : isDestination(curNode.CellPosition)) == true)
                {
                    //목적지 도착
                    return RetracePath(startPathNode, curNode);
                }

                //근접 노드 찾기
                var neighbords = GetNeighbors(curNode, nodeContainer);

                //근접한 노드 검사
                for (int i = 0; i < neighbords.Count; i++)
                {
                    var neighbor = neighbords[i];

                    //완료 노드일때
                    if (close.Contains(neighbor.CellPosition) == true)
                    {
                        continue;
                    }

                    if (canMove != null && canMove(curNode.CellPosition, neighbor.CellPosition) == false)
                    {
                        continue;
                    }

                    var newGCost = curNode.G + GetHeuristic(curNode.CellPosition, neighbor.CellPosition);

                    //목표까지 코스트가 낮으면
                    if (newGCost < neighbor.G)
                    {
                        neighbor.SetPathCost(curNode, newGCost,
                            isDestination == null ? GetHeuristic(neighbor.CellPosition, goalPathNode.CellPosition) : 0);

                        if (open.Contains(neighbor) == false)
                        {
                            open.Add(neighbor);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 근접한 노드 찾기
        /// </summary>
        /// <param name = "curNode">현재 노드 정보</param>
        /// <returns>Directions 기준 노드 정보</returns>
        private List<PathNode> GetNeighbors(PathNode curNode, Dictionary<Vector2Int, PathNode> nodeContainer)
        {
            var neighbors = new List<PathNode>();

            for (int i = 0; i < Directions.Length; i++)
            {
                var nextPos = curNode.CellPosition + Directions[i];

                if (nodeContainer.TryGetValue(nextPos, out var neighbor) == false)
                {
                    neighbor = CombatGrid.GetNodeData(nextPos);

                    if (neighbor != null)
                    {
                        nodeContainer.Add(nextPos, neighbor);
                    }
                }

                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// 맨해튼 거리를 휴리스틱으로 사용
        /// 타일이라 대각선 이동이 자유롭지 않음
        /// </summary>
        /// <param name = "current"></param>
        /// <param name = "goal"></param>
        /// <returns></returns>
        private int GetHeuristic(Vector2Int current, Vector2Int goal)
        {
            return GetManhattanDistance(current, goal);
        }

        private int GetManhattanDistance(Vector2Int a, Vector2Int b)
        {
            var dx = Mathf.Abs(a.x - b.x);
            var dy = Mathf.Abs(a.y - b.y);

            return dx + dy;
        }

        /// <summary>
        /// 시작 -> 목적지 까지 가야할 노드
        /// </summary>
        /// <param name = "startNode">시작 지점</param>
        /// <param name = "goalNode">도착 지점</param>
        /// <returns>해당 도착 지점까지 거처가야 할 결과</returns>
        private List<PathNode> RetracePath(PathNode startNode, PathNode goalNode)
        {
            var path = new List<PathNode>();
            var currentNode = goalNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode);
                currentNode = currentNode.Parent;
            }

            path.Add(startNode);
            path.Reverse();

            return path;
        }
    }
}
