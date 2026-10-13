using System;
using UnityEngine;

namespace ThreeSouls.Utility
{
    /// <summary>
    /// A* 탐색의 셀, 비용과 이전 노드를 보관합니다.
    /// </summary>
    public sealed class PathNode : IComparable<PathNode>
    {
        public Vector2Int CellPosition { get; }
        public int G { get; private set; } = int.MaxValue;
        public int H { get; private set; }
        public PathNode Parent { get; private set; }

        internal PathNode(Vector2Int cellPosition)
        {
            CellPosition = cellPosition;
        }

        internal void SetPathCost(PathNode parent, int distance, int heuristic)
        {
            Parent = parent;
            G = distance;
            H = heuristic;
        }

        private int GetTotalCost()
        {
            return G + H;
        }

        public int CompareTo(PathNode other)
        {
            if (other == null)
            {
                return 1;
            }

            var comparison = GetTotalCost().CompareTo(other.GetTotalCost());

            if (comparison == 0)
            {
                comparison = H.CompareTo(other.H);
            }

            return comparison;
        }
    }
}
