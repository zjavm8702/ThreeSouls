using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace ThreeSouls.Editor
{
    /// <summary>기존 폐광 아트와 전투 태그를 재사용하는 2~5스테이지 편집용 맵을 만듭니다.</summary>
    public static class MineStageBuilder
    {
        private const string Source = "Assets/Prefabs/Grid/Grid_Cave.prefab";

        [MenuItem("Tools/Three Souls/Dungeons/Create Missing Mine Maps")]
        public static void CreateMissingMaps()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit 모드에서 폐광 맵을 제작하세요.");
            for (var stage = 2; stage <= 5; stage++)
            {
                var path = MapPath(stage);
                // 완성 후 직접 편집한 맵은 메뉴 재실행으로 덮어쓰지 않습니다.
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var root = PrefabUtility.LoadPrefabContents(Source);
                try
                {
                    root.name = "Grid_Cave_" + stage.ToString("00");
                    BuildLayout(root, stage);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("Prefabs");
            if (group == null) throw new InvalidOperationException("기존 Prefabs Addressables 그룹이 필요합니다.");
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            for (var stage = 1; stage <= 5; stage++)
            {
                var path = MapPath(stage);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AddressableKeysGenerator.Generate(false);
        }

        public static string MapPath(int stage)
        {
            if (stage < 1 || stage > 5) throw new ArgumentOutOfRangeException(nameof(stage));
            return stage == 1 ? Source : "Assets/Prefabs/Grid/Grid_Cave_" + stage.ToString("00") + ".prefab";
        }

        private static void BuildLayout(GameObject root, int stage)
        {
            var maps = root.GetComponentsInChildren<Tilemap>();
            var ground = maps.Single(map => map.CompareTag("Traversable"));
            var walls = maps.Single(map => map.name == "CaveWalls");
            var obstacles = maps.Single(map => map.name == "CombatObstacles");
            var floorTiles = new List<TileBase>();
            foreach (var cell in ground.cellBounds.allPositionsWithin)
            {
                var tile = ground.GetTile(cell);
                if (tile != null && !floorTiles.Contains(tile)) floorTiles.Add(tile);
            }
            var obstacleTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/CombatObstacle.asset");
            var rock = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/MainLev2.0_324.asset");
            var darkRock = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/MainLev2.0_3.asset");
            var waterTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Water.asset");
            if (floorTiles.Count == 0 || obstacleTile == null || rock == null || darkRock == null || waterTile == null)
                throw new InvalidOperationException("기존 폐광 타일을 확인하세요.");
            ground.ClearAllTiles(); walls.ClearAllTiles(); obstacles.ClearAllTiles();
            var floor = new HashSet<Vector2Int>();
            Vector2Int hero;
            Vector2Int[] shrines;
            if (stage == 2)
            {
                Room(floor, -18, -10, -9, -3); Room(floor, -18, 0, -7, 9);
                Room(floor, -2, 1, 8, 11); Room(floor, 9, -9, 19, 2);
                Connect(floor, new Vector2Int(-13, -6), new Vector2Int(-12, 4));
                Connect(floor, new Vector2Int(-12, 4), new Vector2Int(3, 6));
                Connect(floor, new Vector2Int(3, 6), new Vector2Int(14, -4));
                hero = new Vector2Int(-13, -7);
                shrines = new[] { new Vector2Int(-12, 4), new Vector2Int(3, 6), new Vector2Int(14, -4) };
            }
            else if (stage == 3)
            {
                Room(floor, -18, -10, 18, 11);
                floor.RemoveWhere(cell => cell.x >= -3 && cell.x <= 2 &&
                    !(cell.y >= -6 && cell.y <= -3) && !(cell.y >= 5 && cell.y <= 8));
                hero = new Vector2Int(-13, -7);
                shrines = new[] { new Vector2Int(-12, 6), new Vector2Int(12, 6), new Vector2Int(12, -6) };
            }
            else if (stage == 4)
            {
                Room(floor, -20, -11, 20, 12);
                floor.RemoveWhere(cell => cell.x >= -5 && cell.x <= 5 && cell.y >= -3 && cell.y <= 3);
                hero = new Vector2Int(-14, -8);
                shrines = new[] { new Vector2Int(-13, 6), new Vector2Int(13, 6), new Vector2Int(13, -6) };
            }
            else
            {
                Room(floor, -5, -13, 5, -5); Room(floor, -20, -7, -9, 4);
                Room(floor, -15, 6, 8, 14); Room(floor, 10, -2, 21, 12); Room(floor, -5, -1, 5, 5);
                Connect(floor, new Vector2Int(0, -9), new Vector2Int(-14, -2));
                Connect(floor, new Vector2Int(-14, -2), new Vector2Int(-8, 9));
                Connect(floor, new Vector2Int(-8, 9), new Vector2Int(15, 7));
                Connect(floor, new Vector2Int(15, 7), new Vector2Int(0, 2));
                hero = new Vector2Int(0, -10);
                shrines = new[] { new Vector2Int(-14, -1), new Vector2Int(15, 7), new Vector2Int(0, 2) };
            }
            var minimum = new Vector2Int(floor.Min(cell => cell.x) - 2, floor.Min(cell => cell.y) - 2);
            var maximum = new Vector2Int(floor.Max(cell => cell.x) + 2, floor.Max(cell => cell.y) + 2);
            Tilemap water = null;
            if (stage == 3)
            {
                water = new GameObject("Mine Water Channel", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
                water.transform.SetParent(ground.transform.parent, false);
                var renderer = water.GetComponent<TilemapRenderer>();
                var sourceRenderer = ground.GetComponent<TilemapRenderer>();
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder;
                water.color = new Color(0.42f, 0.67f, 0.78f);
            }
            for (var x = minimum.x; x <= maximum.x; x++)
            for (var y = minimum.y; y <= maximum.y; y++)
            {
                var cell = new Vector2Int(x, y);
                var position = new Vector3Int(x, y, 0);
                if (floor.Contains(cell))
                {
                    ground.SetTile(position, floorTiles[(int)((uint)(x * 73856093 ^ y * 19349663) % floorTiles.Count)]);
                }
                else
                {
                    var isWater = stage == 3 && x >= -3 && x <= 2 && y >= -10 && y <= 11;
                    if (isWater) water.SetTile(position, waterTile);
                    else walls.SetTile(position, floor.Contains(cell + Vector2Int.down) ? rock : darkRock);
                    obstacles.SetTile(position, obstacleTile);
                }
            }
            ground.CompressBounds(); walls.CompressBounds(); obstacles.CompressBounds();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            transforms.Single(item => item.CompareTag("HeroSpawn")).position = ground.GetCellCenterWorld((Vector3Int)hero);
            var markers = transforms.Where(item => item.CompareTag("MonsterSpawn")).OrderBy(item => item.name, StringComparer.Ordinal).ToArray();
            if (markers.Length != 3) throw new InvalidOperationException("폐광 성소 지점은 세 개여야 합니다.");
            for (var i = 0; i < markers.Length; i++) markers[i].position = ground.GetCellCenterWorld((Vector3Int)shrines[i]);
            var bounds = root.GetComponent<BoxCollider2D>();
            bounds.offset = new Vector2((minimum.x + maximum.x + 1) * 0.5f, (minimum.y + maximum.y + 1) * 0.5f);
            bounds.size = new Vector2(maximum.x - minimum.x + 1, maximum.y - minimum.y + 1);
            var tint = stage == 3 ? new Color(0.58f, 0.80f, 1) : stage == 4 ? new Color(1, 0.82f, 0.58f)
                : stage == 5 ? new Color(0.86f, 0.64f, 1) : new Color(0.90f, 0.86f, 1);
            ground.color = tint;
            foreach (var light in root.GetComponentsInChildren<Light2D>()) light.color = tint;
            var decor = transforms.Single(item => item.name == "CaveDecor");
            for (var i = 0; i < decor.childCount; i++)
            {
                var marker = shrines[i % shrines.Length];
                var side = i % 2 == 0 ? -3 : 3;
                var cell = marker + new Vector2Int(side, 3 + i % 3);
                decor.GetChild(i).position = ground.GetCellCenterWorld((Vector3Int)cell);
            }
            foreach (var collider in root.GetComponentsInChildren<TilemapCollider2D>()) collider.ProcessTilemapChanges();
        }

        private static void Room(HashSet<Vector2Int> floor, int left, int bottom, int right, int top)
        {
            for (var x = left; x <= right; x++)
            for (var y = bottom; y <= top; y++) floor.Add(new Vector2Int(x, y));
        }

        private static void Connect(HashSet<Vector2Int> floor, Vector2Int from, Vector2Int to)
        {
            Room(floor, Mathf.Min(from.x, to.x) - 3, from.y - 3, Mathf.Max(from.x, to.x) + 3, from.y + 3);
            Room(floor, to.x - 3, Mathf.Min(from.y, to.y) - 3, to.x + 3, Mathf.Max(from.y, to.y) + 3);
        }
    }
}
