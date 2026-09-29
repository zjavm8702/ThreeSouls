using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Interface
{
    public interface IPathGrid
    {
        bool IsWalkable(Vector2Int pos);
        PathNode GetNodeData(Vector2Int pos);
    }
}