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
    /// <summary>숲과 설원 각 다섯 맵을 기존 전투 스폰/충돌 규약에 맞춰 만듭니다.</summary>
    public static class WildernessStageBuilder
    {
        private const string ArtFolder = "Assets/Images/Environment/Wilderness/";
        private const string TileFolder = "Assets/Tiles/Wilderness";

        [MenuItem("Tools/Three Souls/Dungeons/Create Missing Forest & Frost Maps")]
        public static void CreateMissingMaps()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit 모드에서 맵을 제작하세요.");
            if (!AssetDatabase.IsValidFolder(TileFolder)) AssetDatabase.CreateFolder("Assets/Tiles", "Wilderness");
            var art = LoadSprites();
            for (var region = 3; region <= 4; region++)
            for (var stage = 1; stage <= 5; stage++)
            {
                var path = MapPath(region, stage);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Grid/Grid_Cave.prefab");
                try
                {
                    root.name = MapName(region, stage);
                    BuildLayout(root, region, stage, art);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("Prefabs");
            if (group == null) throw new InvalidOperationException("기존 Prefabs 그룹이 필요합니다.");
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            for (var region = 3; region <= 4; region++)
            for (var stage = 1; stage <= 5; stage++)
            {
                var path = MapPath(region, stage);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AddressableKeysGenerator.Generate(false);
        }

        public static string MapPath(int region, int stage) => "Assets/Prefabs/Grid/" + MapName(region, stage) + ".prefab";

        private static string MapName(int region, int stage)
        {
            if (region < 3 || region > 4 || stage < 1 || stage > 5) throw new ArgumentOutOfRangeException();
            return (region == 3 ? "Grid_Forest_" : "Grid_Frost_") + stage.ToString("00");
        }

        private static Dictionary<string, Sprite> LoadSprites()
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (var path in new[] { ArtFolder + "RoguelikeRPG.png", ArtFolder + "WinterTiles.png" })
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()) sprites.Add(sprite.name, sprite);
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Tiles/Tiles_Frozen.png").OfType<Sprite>())
                sprites.Add("Frozen_" + sprite.name, sprite);
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Tiles/Plants And Crystals.png").OfType<Sprite>())
                sprites.Add(sprite.name, sprite);
            var cave = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Environment/Ruins/RoguelikeDungeon.png").OfType<Sprite>().ToArray();
            sprites.Add("Wilderness_Dirt", cave.Single(s => s.name == "Ruin_17_12"));
            sprites.Add("Wilderness_Waterfall", cave.Single(s => s.name == "Ruin_00_14"));
            return sprites;
        }

        private static Tile TileFor(Dictionary<string, Sprite> sprites, string name)
        {
            var path = TileFolder + "/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile != null) return tile;
            var sprite = sprites[name];
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.LockTransform;
            // 기존 빙벽의 하단 Pivot도 셀 중앙에 정확히 맞춥니다. 원본 Importer는 유지합니다.
            var shift = new Vector3((sprite.pivot.x - sprite.rect.width * .5f) / sprite.pixelsPerUnit,
                (sprite.pivot.y - sprite.rect.height * .5f) / sprite.pixelsPerUnit, 0);
            tile.transform = Matrix4x4.Translate(shift);
            AssetDatabase.CreateAsset(tile, path);
            return tile;
        }

        private static void BuildLayout(GameObject root, int region, int stage, Dictionary<string, Sprite> art)
        {
            var forest = region == 3;
            var maps = root.GetComponentsInChildren<Tilemap>();
            var ground = maps.Single(m => m.CompareTag("Traversable"));
            var walls = maps.Single(m => m.name == "CaveWalls");
            var obstacles = maps.Single(m => m.name == "CombatObstacles");
            ground.ClearAllTiles(); walls.ClearAllTiles(); obstacles.ClearAllTiles();
            ground.color = Color.white;
            walls.name = forest ? "Forest Thickets" : "Frost Cliffs";
            var baseTile = TileFor(art, forest ? "Wild_01_16" : "Snow_00_00");
            var alternate = TileFor(art, forest ? "Wild_01_16" : "Snow_01_00");
            var pathTile = TileFor(art, forest ? "Wilderness_Dirt" : "Snow_04_02");
            var bridgeTile = forest ? AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Ruins/Ruin_20_15.asset") : TileFor(art, "Snow_03_04");
            var rockTile = TileFor(art, forest ? "Wild_01_16" : "Frozen_6");
            var waterTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Ruins/Ruin_02_11.asset");
            var waterfallTile = TileFor(art, "Wilderness_Waterfall");
            var snowMaterial = forest ? null : AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wilderness/SnowProps.mat");
            if (!forest && snowMaterial == null) throw new InvalidOperationException("눈 소품 Material이 필요합니다.");
            var obstacleTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/CombatObstacle.asset");
            if (bridgeTile == null || waterTile == null || obstacleTile == null)
                throw new InvalidOperationException("기존 물/다리/충돌 타일을 확인하세요.");
            var floor = new HashSet<Vector2Int>();
            var waterCells = new HashSet<Vector2Int>();
            Vector2Int hero;
            Vector2Int[] shrines;
            Layout(forest, stage, floor, waterCells, out hero, out shrines);
            var minimum = new Vector2Int(floor.Min(c => c.x) - 2, floor.Min(c => c.y) - 2);
            var maximum = new Vector2Int(floor.Max(c => c.x) + 2, floor.Max(c => c.y) + 2);
            var water = NewMap(forest ? "Forest Water" : "Frozen Chasm", ground, 0);
            var details = NewMap("Wilderness Floor Details", ground, 1);
            water.color = forest ? new Color(.64f, .84f, .90f) : new Color(.22f, .40f, .64f);
            var decor = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "CaveDecor");
            UnityEngine.Object.DestroyImmediate(decor.gameObject);
            var props = new GameObject(forest ? "Forest Trees" : "Snowy Rocks & Pines").transform;
            props.SetParent(root.transform, false);
            var groundRenderer = ground.GetComponent<TilemapRenderer>();
            for (var x = minimum.x; x <= maximum.x; x++)
            for (var y = minimum.y; y <= maximum.y; y++)
            {
                var cell = new Vector2Int(x, y);
                var position = new Vector3Int(x, y, 0);
                var hash = (uint)(x * 73856093 ^ y * 19349663);
                if (floor.Contains(cell))
                {
                    var riverCenter = stage == 2 ? -3 : y >= 0 ? -2 : 2;
                    var crossing = forest && (stage == 2 || stage == 3) && Mathf.Abs(x - riverCenter) <= 2
                        && ((y >= -7 && y <= -3) || (y >= 6 && y <= 10));
                    crossing |= !forest && stage == 2 && Mathf.Abs(y) <= 2;
                    var onPath = forest && (Mathf.Abs(x - hero.x) <= 1 || Mathf.Abs(y - hero.y) <= 1);
                    var tile = crossing ? bridgeTile : onPath ? pathTile : hash % 7 == 0 ? alternate : baseTile;
                    ground.SetTile(position, tile);
                    var brightness = .92f + (hash % 4) * .025f;
                    ground.SetColor(position, forest ? new Color(.74f * brightness, .88f * brightness, .68f * brightness)
                        : stage == 3 ? new Color(.70f * brightness, .84f * brightness, brightness) : new Color(brightness, brightness, brightness));
                    if (forest && hash % 59 == 0 && !shrines.Any(s => (s - cell).sqrMagnitude < 25))
                    {
                        details.SetTile(position, TileFor(art, "Wild_16_08")); // 길 가장자리의 횃불, 충돌 없음
                    }
                }
                else
                {
                    var wet = waterCells.Contains(cell) || (!forest && stage == 4 && y < 2 && x > -8 && x < 8);
                    if (wet) water.SetTile(position, forest && stage == 2 && y >= 10 ? waterfallTile : waterTile);
                    else
                    {
                        walls.SetTile(position, rockTile);
                        walls.SetColor(position, forest ? new Color(.26f, .43f, .29f) : floor.Contains(cell + Vector2Int.down) ? new Color(.90f, .97f, 1) : new Color(.53f, .66f, .83f));
                    }
                    obstacles.SetTile(position, obstacleTile);
                    // 통행 경계를 따라 나무/빙석을 배치합니다. 성소 정렬 공간은 가리지 않습니다.
                    if (!wet && x % 3 == 0 && y % 3 == 0 && floor.Any(c => (c - cell).sqrMagnitude <= 8)
                        && !shrines.Any(s => (s - cell).sqrMagnitude < 25))
                    {
                        var sprite = art[forest ? hash % 2 == 0 ? "Wild_13_09" : "Wild_13_10" : hash % 3 == 0 ? "Snow_Rock" : "Snow_Pine"];
                        Prop(props, sprite, ground.GetCellCenterWorld(position), forest ? 2.5f : 1, groundRenderer,
                            forest ? stage == 5 ? new Color(.68f, .94f, .85f) : Color.white : new Color(.84f, .93f, 1), snowMaterial);
                    }
                    if (!forest && (stage == 3 || stage == 5) && x % 4 == 0 && y % 4 == 0
                        && floor.Any(c => (c - cell).sqrMagnitude <= 4))
                        Prop(props, art["Plants And Crystals_24"], ground.GetCellCenterWorld(position), .125f, groundRenderer, new Color(.70f, .90f, 1));
                }
            }
            ground.CompressBounds(); walls.CompressBounds(); obstacles.CompressBounds();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            transforms.Single(t => t.CompareTag("HeroSpawn")).position = ground.GetCellCenterWorld((Vector3Int)hero);
            var markers = transforms.Where(t => t.CompareTag("MonsterSpawn")).OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
            if (markers.Length != 3) throw new InvalidOperationException("성소 지점은 세 개여야 합니다.");
            for (var i = 0; i < markers.Length; i++) markers[i].position = ground.GetCellCenterWorld((Vector3Int)shrines[i]);
            var bounds = root.GetComponent<BoxCollider2D>();
            bounds.offset = new Vector2((minimum.x + maximum.x + 1) * .5f, (minimum.y + maximum.y + 1) * .5f);
            bounds.size = new Vector2(maximum.x - minimum.x + 1, maximum.y - minimum.y + 1);
            foreach (var light in root.GetComponentsInChildren<Light2D>())
                light.color = forest ? stage == 5 ? new Color(.75f, 1, .93f) : new Color(.88f, 1, .88f) : new Color(.84f, .93f, 1);
            foreach (var collider in root.GetComponentsInChildren<TilemapCollider2D>()) collider.ProcessTilemapChanges();
        }

        private static void Layout(bool forest, int stage, HashSet<Vector2Int> floor, HashSet<Vector2Int> water,
            out Vector2Int hero, out Vector2Int[] shrines)
        {
            if (forest && stage == 1)
            {
                Room(floor, -20, -11, -8, 3); Room(floor, -14, 3, 1, 13);
                Room(floor, 5, 0, 20, 12); Room(floor, 5, -11, 20, -2);
                Connect(floor, new Vector2Int(-14, -7), new Vector2Int(-7, 8));
                Connect(floor, new Vector2Int(-7, 8), new Vector2Int(13, 7));
                Connect(floor, new Vector2Int(13, 7), new Vector2Int(13, -7));
                hero = new Vector2Int(-14, -7);
                shrines = new[] { new Vector2Int(-7, 8), new Vector2Int(13, 7), new Vector2Int(13, -7) };
            }
            else if (forest && (stage == 2 || stage == 3))
            {
                Room(floor, -20, -12, 20, 13);
                for (var y = -12; y <= 13; y++)
                {
                    var center = stage == 2 ? -3 : y >= 0 ? -2 : 2;
                    if ((y >= -7 && y <= -3) || (y >= 6 && y <= 10)) continue;
                    for (var x = center - 2; x <= center + 2; x++) { var c = new Vector2Int(x, y); floor.Remove(c); water.Add(c); }
                }
                hero = new Vector2Int(-13, -8);
                shrines = new[] { new Vector2Int(-12, 6), new Vector2Int(12, 6), new Vector2Int(12, -7) };
            }
            else if (forest && stage == 4)
            {
                Room(floor, -20, -12, -8, -2); Room(floor, -20, 2, -8, 12);
                Room(floor, -3, 4, 8, 13); Room(floor, 9, -9, 21, 3);
                Connect(floor, new Vector2Int(-14, -7), new Vector2Int(-14, 7));
                Connect(floor, new Vector2Int(-14, 7), new Vector2Int(3, 8));
                Connect(floor, new Vector2Int(3, 8), new Vector2Int(15, -3));
                hero = new Vector2Int(-14, -8);
                shrines = new[] { new Vector2Int(-14, 7), new Vector2Int(3, 8), new Vector2Int(15, -3) };
            }
            else if (forest)
            {
                Room(floor, -20, -14, 20, 14);
                floor.RemoveWhere(c => c.x * c.x / 400f + c.y * c.y / 196f > 1);
                Cut(floor, -5, -3, 5, 2);
                hero = new Vector2Int(0, -11);
                shrines = new[] { new Vector2Int(-12, 5), new Vector2Int(12, 5), new Vector2Int(0, 9) };
            }
            else if (stage == 1)
            {
                Room(floor, -19, -11, 19, 11); Cut(floor, -4, -3, -2, 2); Cut(floor, 4, 0, 6, 3);
                hero = new Vector2Int(-13, -8);
                shrines = new[] { new Vector2Int(-12, 6), new Vector2Int(12, 6), new Vector2Int(12, -6) };
            }
            else if (stage == 2)
            {
                Room(floor, -20, -12, 20, 13);
                for (var y = -2; y <= 2; y++)
                for (var x = -20; x <= 20; x++)
                {
                    if ((x >= -14 && x <= -10) || (x >= 9 && x <= 13)) continue;
                    var c = new Vector2Int(x, y); floor.Remove(c); water.Add(c);
                }
                hero = new Vector2Int(-13, -8);
                shrines = new[] { new Vector2Int(-13, 7), new Vector2Int(13, 7), new Vector2Int(13, -7) };
            }
            else if (stage == 3)
            {
                Room(floor, -20, -11, -8, -1); Room(floor, -18, 2, -5, 13);
                Room(floor, 1, 3, 14, 13); Room(floor, 7, -11, 21, 0);
                Connect(floor, new Vector2Int(-14, -6), new Vector2Int(-11, 7));
                Connect(floor, new Vector2Int(-11, 7), new Vector2Int(7, 8));
                Connect(floor, new Vector2Int(7, 8), new Vector2Int(14, -6));
                hero = new Vector2Int(-14, -8);
                shrines = new[] { new Vector2Int(-11, 7), new Vector2Int(7, 8), new Vector2Int(14, -6) };
            }
            else if (stage == 4)
            {
                Room(floor, -21, -12, -9, -2); Room(floor, -3, -12, 9, -2);
                Room(floor, 9, 2, 21, 13); Room(floor, -20, 3, -8, 13);
                Connect(floor, new Vector2Int(-15, -7), new Vector2Int(3, -7));
                Connect(floor, new Vector2Int(3, -7), new Vector2Int(15, 8));
                Connect(floor, new Vector2Int(15, 8), new Vector2Int(-14, 8));
                hero = new Vector2Int(-15, -8);
                shrines = new[] { new Vector2Int(3, -7), new Vector2Int(15, 8), new Vector2Int(-14, 8) };
            }
            else
            {
                Room(floor, -5, -14, 5, -5); Room(floor, -21, -8, -9, 4);
                Room(floor, -16, 6, 16, 14); Room(floor, 9, -8, 21, 4); Room(floor, -5, -1, 5, 6);
                Connect(floor, new Vector2Int(0, -10), new Vector2Int(-15, -2));
                Connect(floor, new Vector2Int(-15, -2), new Vector2Int(-10, 10));
                Connect(floor, new Vector2Int(-10, 10), new Vector2Int(15, 0));
                Connect(floor, new Vector2Int(15, 0), new Vector2Int(0, 3));
                hero = new Vector2Int(0, -11);
                shrines = new[] { new Vector2Int(-15, 0), new Vector2Int(15, 0), new Vector2Int(0, 3) };
            }
        }

        private static void Prop(Transform parent, Sprite sprite, Vector3 position, float scale, TilemapRenderer source, Color color, Material material = null)
        {
            var root = new GameObject(sprite.name, typeof(SpriteRenderer)); root.transform.SetParent(parent, false);
            // Sprite 원본의 Pivot을 그대로 사용하며 나무 밑동을 셀 하단에 맞춥니다.
            var baseShift = sprite.pivot.y / sprite.pixelsPerUnit * scale - .5f;
            root.transform.position = position + Vector3.up * baseShift;
            root.transform.localScale = Vector3.one * scale;
            var renderer = root.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color;
            renderer.sharedMaterial = material != null ? material : source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = 3;
        }

        private static Tilemap NewMap(string name, Tilemap source, int order)
        {
            var map = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            map.transform.SetParent(source.transform.parent, false);
            var renderer = map.GetComponent<TilemapRenderer>(); var original = source.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = original.sharedMaterial; renderer.sortingLayerID = original.sortingLayerID; renderer.sortingOrder = original.sortingOrder + order;
            return map;
        }

        private static void Room(HashSet<Vector2Int> cells, int left, int bottom, int right, int top)
        {
            for (var x = left; x <= right; x++) for (var y = bottom; y <= top; y++) cells.Add(new Vector2Int(x, y));
        }

        private static void Cut(HashSet<Vector2Int> cells, int left, int bottom, int right, int top)
            => cells.RemoveWhere(c => c.x >= left && c.x <= right && c.y >= bottom && c.y <= top);

        private static void Connect(HashSet<Vector2Int> cells, Vector2Int from, Vector2Int to)
        {
            Room(cells, Mathf.Min(from.x, to.x) - 3, from.y - 3, Mathf.Max(from.x, to.x) + 3, from.y + 3);
            Room(cells, to.x - 3, Mathf.Min(from.y, to.y) - 3, to.x + 3, Mathf.Max(from.y, to.y) + 3);
        }
    }
}
