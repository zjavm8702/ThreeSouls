using System;
using System.Collections.Generic;
using ThreeSouls.Combat;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 마을의 바닥과 몸 크기 충돌을 검사하고 기존 A*로 이동 경로를 구합니다.
    /// </summary>
    public sealed class VillageNavigationGrid : MonoBehaviour, IPathGrid
    {
        [SerializeField] private Tilemap[] GroundMaps;
        [SerializeField] private Tilemap BridgeMap;
        [SerializeField] private TileBase BridgeTile;
        [SerializeField] private LayerMask ObstacleLayers = -1;
        private float BodyRadius;
        private PhysicsScene2D PhysicsScene;
        private ContactFilter2D Filter;
        private AStarPathfinder Pathfinder;
        private readonly Collider2D[] Overlaps = new Collider2D[1];
        private readonly RaycastHit2D[] Hits = new RaycastHit2D[1];

        public void Initialize()
        {
            BodyRadius = ThreeSouls.Data.LobbyTables.GetVillageRules().BodyRadius;
            if (GroundMaps == null || GroundMaps.Length == 0 || BodyRadius <= 0)
            {
                throw new InvalidOperationException("마을의 바닥 Tilemap과 이동 반경을 연결해야 합니다.");
            }

            for (var i = 0; i < GroundMaps.Length; i++)
            {
                if (GroundMaps[i] == null || GroundMaps[i].layoutGrid != GroundMaps[0].layoutGrid)
                {
                    throw new InvalidOperationException("마을 바닥은 같은 Grid의 Tilemap이어야 합니다.");
                }
            }

            if (BridgeMap != null && BridgeTile == null)
            {
                throw new InvalidOperationException("다리 Tilemap에는 통행 가능한 다리 타일을 연결해야 합니다.");
            }

            PhysicsScene = gameObject.scene.GetPhysicsScene2D();
            Filter = new ContactFilter2D();
            Filter.SetLayerMask(ObstacleLayers);
            Filter.useTriggers = false;
            Pathfinder = new AStarPathfinder(this);
            Physics2D.SyncTransforms();
        }

        public bool IsWalkable(Vector2Int cell)
        {
            return HasFloor(cell) == true && IsClear(GetCellCenter(cell)) == true;
        }

        public PathNode GetNodeData(Vector2Int cell)
        {
            return IsWalkable(cell) == true ? new PathNode(cell) : null;
        }

        public bool IsPositionWalkable(Vector2 position)
        {
            return HasFloor(GetCell(position)) == true && IsClear(position) == true;
        }

        public bool CanTraverse(Vector2 start, Vector2 destination)
        {
            if (IsPositionWalkable(destination) == false)
            {
                return false;
            }

            var delta = destination - start;
            var distance = delta.magnitude;

            if (distance <= 0.0001f)
            {
                return true;
            }

            if (PhysicsScene.CircleCast(start, BodyRadius, delta / distance, distance, Filter, Hits) > 0)
            {
                return false;
            }

            var samples = Mathf.CeilToInt(distance / 0.25f);

            for (var i = 0; i <= samples; i++)
            {
                if (HasFloor(GetCell(Vector2.Lerp(start, destination, i / (float)samples))) == false)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryFindRoute(Vector2 start, Vector2 destination, out List<Vector2> route)
        {
            if (Pathfinder == null)
            {
                throw new InvalidOperationException("마을 이동 Grid를 먼저 초기화해야 합니다.");
            }

            route = null;

            if (IsPositionWalkable(start) == false || IsPositionWalkable(destination) == false)
            {
                return false;
            }

            if (CanTraverse(start, destination) == true)
            {
                route = new List<Vector2> { start, destination };

                return true;
            }

            if (TryFindAnchor(start, out var from) == false || TryFindAnchor(destination, out var to) == false)
            {
                return false;
            }

            var nodes = Pathfinder.FindPath(GetNodeData(from), GetNodeData(to), null,
                (left, right) => CanTraverse(GetCellCenter(left), GetCellCenter(right)));

            if (nodes == null)
            {
                return false;
            }

            route = new List<Vector2> { start };

            for (var i = 0; i < nodes.Count; i++)
            {
                var position = GetCellCenter(nodes[i].CellPosition);

                if ((position - route[route.Count - 1]).sqrMagnitude > 0.000001f)
                {
                    route.Add(position);
                }
            }

            route.Add(destination);

            return true;
        }

        private bool TryFindAnchor(Vector2 position, out Vector2Int anchor)
        {
            var cell = GetCell(position);
            anchor = cell;
            var best = float.MaxValue;

            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    var candidate = cell + new Vector2Int(x, y);
                    var center = GetCellCenter(candidate);
                    var distance = (center - position).sqrMagnitude;

                    if (distance < best && IsWalkable(candidate) == true && CanTraverse(position, center) == true)
                    {
                        best = distance;
                        anchor = candidate;
                    }
                }
            }

            return best < float.MaxValue;
        }

        private bool HasFloor(Vector2Int cell)
        {
            for (var i = 0; i < GroundMaps.Length; i++)
            {
                if (GroundMaps[i].HasTile((Vector3Int)cell) == true)
                {
                    return true;
                }
            }

            return BridgeMap != null && BridgeMap.GetTile((Vector3Int)cell) == BridgeTile;
        }

        private Vector2Int GetCell(Vector2 position)
        {
            return (Vector2Int)GroundMaps[0].WorldToCell(position);
        }

        private Vector2 GetCellCenter(Vector2Int cell)
        {
            return GroundMaps[0].GetCellCenterWorld((Vector3Int)cell);
        }

        private bool IsClear(Vector2 position)
        {
            return PhysicsScene.OverlapCircle(position, BodyRadius, Filter, Overlaps) == 0;
        }
    }
}
