using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ThreeSouls.Combat
{
    internal sealed class CombatGroundHandler : IPathGrid
    {
        public Collider2D CameraBounds { get; private set; } = null;

        private GameObject SpawnObject = null;
        private Tilemap SpawnTilemap = null;
        private ContactFilter2D MoveContactFilter = default;
        private readonly RaycastHit2D[] MoveHitResults = new RaycastHit2D[1];
        private readonly Collider2D[] MoveOverlapResults = new Collider2D[1];

        public void CreateStageMap(Prefabs key)
        {
            var asset = AddressableManager.Get().LoadAsset<GameObject>(key.ToAddress());

            if (asset == null)
            {
                throw new System.InvalidOperationException($"전투 맵 원본이 로드되지 않았습니다 : {key.ToAddress()}");
            }

            SpawnObject = Object.Instantiate(asset);
            SpawnObject.name = asset.name;
            SpawnObject.transform.position = Vector2.zero;
            SpawnObject.SetActive(true);
            CameraBounds = SpawnObject.GetComponent<BoxCollider2D>();

            if (CameraBounds == null)
            {
                throw new System.InvalidOperationException($"전투 맵의 카메라 경계가 없습니다 : {key.ToAddress()}");
            }

            var tiles = SpawnObject.GetComponentsInChildren<Tilemap>();

            if (tiles.Length == 0)
            {
                throw new System.InvalidOperationException($"전투 맵에 Tilemap이 없습니다 : {key.ToAddress()}");
            }

            SpawnTilemap = tiles.FirstOrDefault(x => x.CompareTag("Traversable"));

            if (SpawnTilemap == null)
            {
                throw new System.InvalidOperationException($"전투 맵의 Traversable Tilemap이 없습니다 : {key.ToAddress()}");
            }

            MoveContactFilter.SetLayerMask(LayerMask.GetMask(StringConstants.LAYERMASK_COMBAT_OBSTACLE));
            MoveContactFilter.useTriggers = false;
            var colliderItems = SpawnObject.GetComponentsInChildren<TilemapCollider2D>();

            for (var colliderIndex = 0; colliderIndex < colliderItems.Length; colliderIndex++)
            {
                var collider = colliderItems[colliderIndex];
                collider.ProcessTilemapChanges();
            }

            Physics2D.SyncTransforms();
        }

        /// <summary>
        /// 목적지의 바닥과 캐릭터 몸 크기를 검사해 장애물 없이 직접 이동할 수 있는지 확인
        /// 충돌 여유 거리를 포함한 원으로 목적지와 이동 구간을 검사하며 Trigger는 제외
        /// </summary>
        /// <param name = "start">캐릭터의 시작 월드 좌표</param>
        /// <param name = "destination">이동할 목적지의 월드 좌표</param>
        /// <param name = "radius">월드 크기로 환산한 이동 콜라이더의 반지름</param>
        /// <param name = "offset">캐릭터 위치에서 이동 콜라이더 중심까지의 월드 좌표 오프셋</param>
        /// <returns>목적지가 이동 가능한 바닥이고 몸 크기로 검사한 구간에 장애물이 없으면 true</returns>
        public bool CanMove(Vector2 start, Vector2 destination, float radius, Vector2 offset)
        {
            if (IsWalkable(WorldToCell(destination)) == false)
            {
                return false;
            }

            var direction = destination - start;
            var distance = direction.magnitude;
            radius += Constants.MOVE_COLLISION_MARGIN;

            if (Physics2D.OverlapCircle(destination + offset, radius, MoveContactFilter, MoveOverlapResults) > 0)
            {
                return false;
            }

            if (distance <= 0.0001f)
            {
                return true;
            }

            return Physics2D.CircleCast(start + offset, radius, direction / distance, MoveContactFilter, MoveHitResults, distance) == 0;
        }

        /// <summary>
        /// 시작 셀에서 몸 크기로 통행 가능한 연결된 바닥 셀만 수집한다.
        /// </summary>
        public List<Vector2Int> GetReachableCells(Vector3 position, float radius)
        {
            var result = new List<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var start = WorldToCell(position);

            if (IsWalkable(start) == false)
            {
                return result;
            }

            queue.Enqueue(start);
            visited.Add(start);
            var directions = new[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                result.Add(cell);

                for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
                {
                    var direction = directions[directionIndex];
                    var next = cell + direction;

                    if (visited.Contains(next) == true || IsWalkable(next) == false)
                    {
                        continue;
                    }

                    if (CanMove(CellToWorldCenter(cell), CellToWorldCenter(next), radius, Vector2.zero) == false)
                    {
                        continue;
                    }

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return result;
        }

        public void Release()
        {
            if (SpawnObject != null)
            {
                Object.Destroy(SpawnObject);
                SpawnObject = null;
                SpawnTilemap = null;
                CameraBounds = null;
            }
        }

        /// <summary>
        /// 월드 좌표가 속한 셀을 중심으로 X와 Y 양쪽의 지정 범위에서 이동 가능한 셀을 찾음
        /// </summary>
        /// <param name = "worldPosition">탐색 중심의 월드 좌표</param>
        /// <param name = "radius">중심 셀에서 각 축으로 탐색할 셀 수. 음수는 0으로 처리</param>
        /// <returns>지정한 사각 범위 안의 이동 가능한 셀 좌표 목록</returns>
        /// <exception cref = "System.InvalidOperationException">바닥 Tilemap이 초기화되지 않았을 때 발생</exception>
        public List<Vector2Int> GetWalkableCellsAround(Vector3 worldPosition, int radius)
        {
            var result = new List<Vector2Int>();

            if (SpawnTilemap == null)
            {
                throw new System.InvalidOperationException("전투 맵을 생성한 뒤 스폰 셀을 조회해야 합니다.");
            }

            var center = WorldToCell(worldPosition);
            radius = Mathf.Max(0, radius);

            //x y 양쪽
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    var cell = center + new Vector2Int(x, y);

                    if (IsWalkable(cell) == true)
                    {
                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 타일 셀 좌표를 해당 셀 중심의 월드 좌표로 변환
        /// </summary>
        /// <param name = "point">변환할 타일 셀 좌표</param>
        /// <returns>해당 셀 중심의 월드 좌표</returns>
        public Vector3 CellToWorldCenter(Vector2Int point)
        {
            return SpawnTilemap.GetCellCenterWorld((Vector3Int)point);
        }

        /// <summary>
        /// 월드 좌표를 바닥 Tilemap의 2차원 셀 좌표로 변환
        /// </summary>
        /// <param name = "pos">변환할 월드 좌표</param>
        /// <returns>월드 좌표가 속한 타일 셀 좌표</returns>
        public Vector2Int WorldToCell(Vector3 pos)
        {
            return (Vector2Int)SpawnTilemap.WorldToCell(pos);
        }

        /// <summary>
        /// 셀에 바닥 타일이 있고 셀 중심의 충돌 여유 범위에 장애물이 없는지 확인
        /// 캐릭터 몸 크기와 이동 구간의 검사는 CanMove에서 별도로 수행.
        /// </summary>
        /// <param name = "pos">검사할 타일 셀 좌표</param>
        /// <returns>바닥 타일이 있고 셀 중심에 장애물이 없으면 true</returns>
        public bool IsWalkable(Vector2Int pos)
        {
            if (SpawnTilemap.HasTile((Vector3Int)pos) == false)
            {
                return false;
            }

            return Physics2D.OverlapCircle(CellToWorldCenter(pos), Constants.MOVE_COLLISION_MARGIN, MoveContactFilter, MoveOverlapResults) == 0;
        }

        /// <summary>
        /// 이동 가능한 셀의 A* 탐색 노드를 생성
        /// </summary>
        /// <param name = "pos">노드를 생성할 타일 셀 좌표</param>
        /// <returns>이동 가능한 셀이면 새 PathNode, 이동 불가능한 셀이면 null</returns>
        public PathNode GetNodeData(Vector2Int pos)
        {
            if (IsWalkable(pos) == false)
            {
                return null;
            }

            return new PathNode(pos);
        }
    }
}
