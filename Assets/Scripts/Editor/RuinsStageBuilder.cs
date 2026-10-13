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
    /// <summary>유적의 석조 야영지, 다리, 회랑, 감시탑, 성채를 편집 가능한 맵으로 만듭니다.</summary>
    public static class RuinsStageBuilder
    {
        private const string Art = "Assets/Images/Environment/Ruins/RoguelikeDungeon.png";
        private const string Tiles = "Assets/Tiles/Ruins";

        [MenuItem("Tools/Three Souls/Dungeons/Create Missing Ruins Maps")]
        public static void CreateMissingMaps()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit 모드에서 유적 맵을 제작하세요.");
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().ToDictionary(s => s.name);
            if (sprites.Count == 0) throw new InvalidOperationException("유적 타일 시트를 먼저 분할하세요.");
            if (!AssetDatabase.IsValidFolder(Tiles)) AssetDatabase.CreateFolder("Assets/Tiles", "Ruins");
            for (var stage = 1; stage <= 5; stage++)
            {
                var path = MapPath(stage);
                // 완성 후 사용자가 편집한 맵은 다시 생성하지 않습니다.
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Grid/Grid_Cave.prefab");
                try
                {
                    root.name = "Grid_Ruins_" + stage.ToString("00");
                    BuildLayout(root, stage, sprites);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("Prefabs");
            if (group == null) throw new InvalidOperationException("기존 Prefabs 그룹이 필요합니다.");
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            for (var stage = 1; stage <= 5; stage++)
            {
                var path = MapPath(stage);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
            }
            // 새 타일은 기존 Tiles 폴더 주소의 하위 에셋으로 등록됩니다.
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AddressableKeysGenerator.Generate(false);
        }

        public static string MapPath(int stage)
        {
            if (stage < 1 || stage > 5) throw new ArgumentOutOfRangeException(nameof(stage));
            return "Assets/Prefabs/Grid/Grid_Ruins_" + stage.ToString("00") + ".prefab";
        }

        private static string SpriteName(int column, int row) => "Ruin_" + column.ToString("00") + "_" + row.ToString("00");

        private static Tile TileFor(Dictionary<string, Sprite> sprites, int column, int row)
        {
            var name = SpriteName(column, row);
            var path = Tiles + "/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile != null) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprites[name];
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.LockTransform;
            AssetDatabase.CreateAsset(tile, path);
            return tile;
        }

        private static void BuildLayout(GameObject root, int stage, Dictionary<string, Sprite> sprites)
        {
            var maps = root.GetComponentsInChildren<Tilemap>();
            var ground = maps.Single(m => m.CompareTag("Traversable"));
            var walls = maps.Single(m => m.name == "CaveWalls");
            var obstacles = maps.Single(m => m.name == "CombatObstacles");
            walls.name = "Ruins Stone Walls";
            ground.ClearAllTiles(); walls.ClearAllTiles(); obstacles.ClearAllTiles();
            ground.color = Color.white; walls.color = new Color(0.72f, 0.77f, 0.81f);
            var stone = new[] { TileFor(sprites, 16, 10), TileFor(sprites, 17, 10), TileFor(sprites, 18, 10), TileFor(sprites, 19, 10) };
            var paving = TileFor(sprites, 20, 11);
            var sand = TileFor(sprites, 17, 14);
            var bridge = TileFor(sprites, 20, 15);
            var wallFace = TileFor(sprites, 9, 2);
            var wallTop = TileFor(sprites, 9, 0);
            var waterTile = TileFor(sprites, 2, 11);
            var obstacleTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/CombatObstacle.asset");
            if (obstacleTile == null) throw new InvalidOperationException("공용 충돌 타일이 필요합니다.");
            var floor = new HashSet<Vector2Int>();
            var pillars = new List<Vector2Int>();
            Vector2Int hero;
            Vector2Int[] shrines;
            if (stage == 1)
            {
                Room(floor, -19, -11, 19, 11);
                Cut(floor, -4, -2, 0, 3); Cut(floor, 4, -1, 6, 2);
                pillars.AddRange(new[] { new Vector2Int(-4, 3), new Vector2Int(5, 2) });
                hero = new Vector2Int(-13, -8);
                shrines = new[] { new Vector2Int(-12, 6), new Vector2Int(12, 6), new Vector2Int(12, -6) };
            }
            else if (stage == 2)
            {
                Room(floor, -20, -11, -5, 11); Room(floor, 5, -11, 20, 11);
                Room(floor, -5, -7, 5, -3); Room(floor, -5, 4, 5, 8);
                hero = new Vector2Int(-13, -7);
                shrines = new[] { new Vector2Int(-13, 6), new Vector2Int(13, 6), new Vector2Int(13, -6) };
            }
            else if (stage == 3)
            {
                Room(floor, -21, -11, -10, -3); Room(floor, -20, 2, -9, 12);
                Room(floor, -4, 3, 7, 13); Room(floor, 10, -10, 21, 1);
                Connect(floor, new Vector2Int(-15, -7), new Vector2Int(-14, 7));
                Connect(floor, new Vector2Int(-14, 7), new Vector2Int(1, 8));
                Connect(floor, new Vector2Int(1, 8), new Vector2Int(15, -5));
                pillars.AddRange(new[] { new Vector2Int(-8, 12), new Vector2Int(8, 1), new Vector2Int(-8, -5) });
                hero = new Vector2Int(-15, -7);
                shrines = new[] { new Vector2Int(-14, 7), new Vector2Int(1, 8), new Vector2Int(15, -5) };
            }
            else if (stage == 4)
            {
                Room(floor, -19, -12, 19, 12);
                Cut(floor, -4, -4, 4, 4); // 감시탑 안쪽은 이동 불가, 둘레의 넓은 동선으로 진입합니다.
                pillars.AddRange(new[] { new Vector2Int(-4, 4), new Vector2Int(4, 4) });
                hero = new Vector2Int(0, -9);
                shrines = new[] { new Vector2Int(-12, 5), new Vector2Int(0, 9), new Vector2Int(12, 5) };
            }
            else
            {
                Room(floor, -5, -14, 5, -5); Room(floor, -21, -8, -9, 5);
                Room(floor, -21, 7, 21, 14); Room(floor, 9, -8, 21, 5); Room(floor, -5, -2, 5, 4);
                Connect(floor, new Vector2Int(0, -10), new Vector2Int(-15, -2));
                Connect(floor, new Vector2Int(-15, -2), new Vector2Int(-15, 10));
                Connect(floor, new Vector2Int(15, 10), new Vector2Int(15, -2));
                Connect(floor, new Vector2Int(15, -2), new Vector2Int(0, 1));
                pillars.AddRange(new[] { new Vector2Int(-7, 5), new Vector2Int(7, 5), new Vector2Int(-7, -3), new Vector2Int(7, -3) });
                hero = new Vector2Int(0, -11);
                shrines = new[] { new Vector2Int(-15, -1), new Vector2Int(15, -1), new Vector2Int(0, 1) };
            }
            foreach (var cell in pillars) floor.Remove(cell);
            var minimum = new Vector2Int(floor.Min(c => c.x) - 2, floor.Min(c => c.y) - 2);
            var maximum = new Vector2Int(floor.Max(c => c.x) + 2, floor.Max(c => c.y) + 2);
            var water = NewMap("Ruins Water", ground, 0);
            var details = NewMap("Ruins Floor Details", ground, 1);
            water.color = new Color(0.55f, 0.74f, 0.77f);
            for (var x = minimum.x; x <= maximum.x; x++)
            for (var y = minimum.y; y <= maximum.y; y++)
            {
                var cell = new Vector2Int(x, y);
                var position = new Vector3Int(x, y, 0);
                var hash = (uint)(x * 73856093 ^ y * 19349663);
                if (floor.Contains(cell))
                {
                    var onBridge = stage == 2 && x >= -5 && x <= 5;
                    var onRoad = (Mathf.Abs(x) <= 2 || Mathf.Abs(y) <= 1);
                    var tile = onBridge ? bridge : stage == 1 && !onRoad ? sand : onRoad ? paving : stone[(int)(hash % stone.Length)];
                    ground.SetTile(position, tile);
                    if (hash % 43 == 0 && !shrines.Any(s => (s - cell).sqrMagnitude < 20) && (hero - cell).sqrMagnitude > 9)
                        details.SetTile(position, TileFor(sprites, 10, 10));
                }
                else
                {
                    if (stage == 2 && x >= -4 && x <= 4 && y >= -11 && y <= 11)
                        water.SetTile(position, waterTile);
                    else
                    {
                        walls.SetTile(position, floor.Contains(cell + Vector2Int.down) ? wallTop : wallFace);
                        walls.SetColor(position, floor.Contains(cell + Vector2Int.down) ? Color.white : new Color(0.55f, 0.58f, 0.62f));
                    }
                    obstacles.SetTile(position, obstacleTile);
                }
            }
            ground.CompressBounds(); walls.CompressBounds(); obstacles.CompressBounds();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            transforms.Single(t => t.CompareTag("HeroSpawn")).position = ground.GetCellCenterWorld((Vector3Int)hero);
            var markers = transforms.Where(t => t.CompareTag("MonsterSpawn")).OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
            if (markers.Length != 3) throw new InvalidOperationException("성소 지점은 세 개여야 합니다.");
            for (var i = 0; i < markers.Length; i++) markers[i].position = ground.GetCellCenterWorld((Vector3Int)shrines[i]);
            var bounds = root.GetComponent<BoxCollider2D>();
            bounds.offset = new Vector2((minimum.x + maximum.x + 1) * 0.5f, (minimum.y + maximum.y + 1) * 0.5f);
            bounds.size = new Vector2(maximum.x - minimum.x + 1, maximum.y - minimum.y + 1);
            var oldDecor = transforms.Single(t => t.name == "CaveDecor");
            UnityEngine.Object.DestroyImmediate(oldDecor.gameObject);
            var decor = new GameObject("Ruins Columns").transform; decor.SetParent(root.transform, false);
            foreach (var cell in pillars)
            for (var part = 0; part < 3; part++)
            {
                var column = new GameObject("Stone Column " + cell + " " + part, typeof(SpriteRenderer));
                column.transform.SetParent(decor, false);
                column.transform.position = ground.GetCellCenterWorld((Vector3Int)(cell + Vector2Int.up * part));
                var renderer = column.GetComponent<SpriteRenderer>();
                renderer.sprite = part == 0 ? sprites[SpriteName(1, 6)]
                    : part == 1 ? sprites[SpriteName(3, 6)] : sprites[SpriteName(1, 5)];
                renderer.sharedMaterial = ground.GetComponent<TilemapRenderer>().sharedMaterial;
                renderer.sortingLayerID = ground.GetComponent<TilemapRenderer>().sortingLayerID;
                renderer.sortingOrder = 3;
            }
            var lightColor = stage == 1 ? new Color(1, 0.90f, 0.77f) : stage == 2 ? new Color(0.80f, 0.93f, 1)
                : stage == 5 ? new Color(0.91f, 0.87f, 1) : new Color(0.94f, 0.96f, 1);
            foreach (var light in root.GetComponentsInChildren<Light2D>()) light.color = lightColor;
            foreach (var collider in root.GetComponentsInChildren<TilemapCollider2D>()) collider.ProcessTilemapChanges();
        }

        private static Tilemap NewMap(string name, Tilemap source, int orderOffset)
        {
            var map = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            map.transform.SetParent(source.transform.parent, false);
            var renderer = map.GetComponent<TilemapRenderer>();
            var sourceRenderer = source.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder + orderOffset;
            return map;
        }

        private static void Room(HashSet<Vector2Int> floor, int left, int bottom, int right, int top)
        {
            for (var x = left; x <= right; x++)
            for (var y = bottom; y <= top; y++) floor.Add(new Vector2Int(x, y));
        }

        private static void Cut(HashSet<Vector2Int> floor, int left, int bottom, int right, int top)
            => floor.RemoveWhere(c => c.x >= left && c.x <= right && c.y >= bottom && c.y <= top);

        private static void Connect(HashSet<Vector2Int> floor, Vector2Int from, Vector2Int to)
        {
            Room(floor, Mathf.Min(from.x, to.x) - 3, from.y - 3, Mathf.Max(from.x, to.x) + 3, from.y + 3);
            Room(floor, to.x - 3, Mathf.Min(from.y, to.y) - 3, to.x + 3, Mathf.Max(from.y, to.y) + 3);
        }
    }
}
