using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ThreeSouls.Utility
{
    public class AddressableDistinctItemComparer : IEqualityComparer<IResourceLocation>
    {
        public bool Equals(IResourceLocation x, IResourceLocation y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            return string.Equals(x.PrimaryKey, y.PrimaryKey, StringComparison.Ordinal) && x.ResourceType == y.ResourceType;
        }

        public int GetHashCode(IResourceLocation obj)
        {
            if (obj == null)
            {
                return 0;
            }

            return HashCode.Combine(obj.PrimaryKey, obj.ResourceType);
        }
    }

    public class PathNode : IComparable<PathNode>
    {
        //해당 위치
        public Vector2Int CellPosition;
        //gCost 시작지점 -> 현재노드 실제 이동한 비용
        public int G = int.MaxValue;
        //hCost 현재노드 -> 목표노드까지 "예상" 비용
        public int H = 0;
        //fCost 실제로 우선순위를 판단할 때 사용되는 값
        public int F => G + H;
        //이전 노드 정보
        public PathNode Parent = null;

        public int CompareTo(PathNode other)
        {
            if (other == null)
            {
                return 1;
            }

            var compare = F.CompareTo(other.F);

            // F값이 같다면 목표노드까지 "예상" 비용 작은 노드가 우선순위가 높음
            if (compare == 0)
            {
                compare = H.CompareTo(other.H);
            }

            return compare;
        }
    }
}
