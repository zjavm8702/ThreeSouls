using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ThreeSouls.Combat
{
    public class CombatGroundHandler : IPathGrid
    {
        public Collider2D CameraBounds { get; private set; } = null;

        private GameObject OriginObject = null;
        private GameObject SpawnObject = null;
        private Tilemap SpawnTimemap = null;
        private Prefabs CurrneAssetName;

        public bool CreateStageMap(Prefabs key)
        {
            var asset = AddressableManager.Get().LoadAsset<GameObject>(key.ToAddress());

            if (asset == null)
            {
                ThreeSoulsLog.Error($"에셋 정보를 불러오지 못했습니다 {key.ToAddress()}");
                return false;
            }

            CurrneAssetName = key;
            OriginObject = asset;

            SpawnObject = Object.Instantiate(OriginObject);
            SpawnObject.name = OriginObject.name;
            SpawnObject.transform.position = Vector2.zero;
            SpawnObject.SetActive(true);

            CameraBounds = SpawnObject.GetComponent<BoxCollider2D>();

            if (CameraBounds == null)
            {
                ThreeSoulsLog.Error($"Camera bounds collider null : {key.ToAddress()}");
                Object.Destroy(SpawnObject);
                SpawnObject = null;
                return false;
            }

            var tiles = SpawnObject.GetComponentsInChildren<Tilemap>();

            if (tiles == null || tiles.Length == 0)
            {
                ThreeSoulsLog.Error($"Component null :  {key.ToAddress()}");
                Object.Destroy(SpawnObject);
                SpawnObject = null;
                CameraBounds = null;
                return false;
            }

            SpawnTimemap = tiles.FirstOrDefault(x => x.CompareTag("Traversable"));

            if (SpawnTimemap == null)
            {
                ThreeSoulsLog.Error($"Component null :  {key.ToAddress()}");
                Object.Destroy(SpawnObject);
                SpawnObject = null;
                CameraBounds = null;
                return false;
            }

            return true;
        }

        public void Release()
        {
            if (SpawnObject != null)
            {
                Object.Destroy(SpawnObject);
                SpawnObject = null;
                SpawnTimemap = null;
                CameraBounds = null;
            }

            AddressableManager.Get().ReleaseAsset(CurrneAssetName.ToAddress());
        }

        public List<Vector2Int> GetWalkableCellsAround(Vector3 worldPosition, int radius)
        {
            var result = new List<Vector2Int>();

            if (SpawnTimemap == null)
            {
                ThreeSoulsLog.Error("Traversable Tilemap이 초기화되지 않았습니다.");
                return result;
            }

            var center = WorldToCell(worldPosition);
            radius = Mathf.Max(0, radius);

            //x y 양쪽
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    var cell = center + new Vector2Int(x, y);

                    if (IsWalkable(cell))
                    {
                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        public Vector3 CellToWorldCenter(Vector2Int point)
        {
            return SpawnTimemap.GetCellCenterWorld((Vector3Int)point);
        }

        public Vector2Int WorldToCell(Vector3 pos)
        {
            return (Vector2Int)SpawnTimemap.WorldToCell(pos);
        }

        public bool IsWalkable(Vector2Int pos)
        {
            return SpawnTimemap.HasTile((Vector3Int)pos);
        }

        public PathNode GetNodeData(Vector2Int pos)
        {
            if (IsWalkable(pos) == false)
            {
                return null;
            }

            return new PathNode
            {
                CellPosition = pos
            };
        }
    }
}
