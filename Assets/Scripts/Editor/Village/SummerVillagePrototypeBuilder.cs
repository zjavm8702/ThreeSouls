using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThreeSouls.UI;
using ThreeSouls.Village;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 무료 Summer Village 아트로 기존 마을과 분리된 성소 광장 테스트 씬을 조립합니다.
    /// </summary>
    public static class SummerVillagePrototypeBuilder
    {
        public const string ScenePath = "Assets/(T)Scene/(T)Scene_Village_Summer.unity";
        private const string ArtPath = "Assets/Images/VillageSummer/";
        private const string WorldName = "Summer Village - Sanctuary Square";
        private const string DataPath = "Assets/Village/Summer/";
        private static Material Material;
        private static Material GrassMaterial;
        private static TMPro.TMP_FontAsset SignsFont;

        [MenuItem("Three Souls/Village/Create Summer Village Test Scene")]
        public static void Create()
        {
            RequireEditMode();
            if (File.Exists(ScenePath) == true)
            {
                throw new InvalidOperationException("새 테스트 씬이 이미 있습니다. 열린 Summer 씬의 Rebuild Summer World 메뉴를 사용하세요.");
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty == true)
                {
                    throw new InvalidOperationException("현재 미저장 씬을 먼저 저장한 뒤 새 테스트 씬을 만드세요.");
                }
            }

            var source = EditorSceneManager.OpenScene(VillageLobbyUIBuilder.ScenePath, OpenSceneMode.Additive);
            if (EditorSceneManager.SaveScene(source, ScenePath, true) == false)
            {
                throw new IOException("새 테스트 씬 사본을 저장하지 못했습니다.");
            }
            EditorSceneManager.CloseScene(source, true);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = GetTransforms(scene);
            var oldWorld = scene.GetRootGameObjects().Single(root => root.name == "Sunnyside Village");
            var camera = oldWorld.GetComponentInChildren<Camera>(true);
            camera.transform.SetParent(null, true);
            var members = oldWorld.GetComponentsInChildren<VillagePartyMember>(true);
            for (var i = 0; i < members.Length; i++)
            {
                members[i].transform.SetParent(null, true);
                members[i].transform.localScale = Vector3.one;
            }

            var motes = all.Single(item => item.name == "Shrine - Floating Golden Motes");
            motes.SetParent(null, true);
            motes.position = new Vector3(0, 1.75f, -0.1f);
            motes.localScale = Vector3.one * 0.35f;
            UnityEngine.Object.DestroyImmediate(oldWorld);
            RemoveRoot(scene, "CinemachineCamera");
            RemoveRoot(scene, "T_Camera");
            RemoveRoot(scene, "VillageCameraBounds");
            var brain = camera.GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                UnityEngine.Object.DestroyImmediate(brain);
            }
            var framing = camera.GetComponent<VillageCameraFrame>();
            if (framing != null)
            {
                UnityEngine.Object.DestroyImmediate(framing);
            }

            ConfigureCamera(camera);
            CompactVillageUI(scene);
            RebuildWorld();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Summer Village test scene created: " + ScenePath);
        }

        [MenuItem("Three Souls/Village/Rebuild Summer World (Current Summer Test Only)")]
        public static void RebuildWorld()
        {
            RequireEditMode();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                throw new InvalidOperationException("Summer 테스트 씬에서만 새 월드를 재조립할 수 있습니다.");
            }
            PrepareAssets();
            RemoveRoot(scene, WorldName);
            var world = new GameObject(WorldName).transform;
            var grid = new GameObject("01 - Editable Tilemaps", typeof(Grid)).GetComponent<Grid>();
            grid.transform.SetParent(world, false);
            grid.cellSize = new Vector3(0.5f, 0.5f, 1);
            var scenery = MakeMap(grid.transform, "Forest Floor - Visual", -30, Color.white);
            var ground = MakeMap(grid.transform, "Walkable Grass", -20, Color.white);
            scenery.GetComponent<TilemapRenderer>().sharedMaterial = GrassMaterial;
            ground.GetComponent<TilemapRenderer>().sharedMaterial = GrassMaterial;
            var water = MakeMap(grid.transform, "River", -10, new Color(0.82f, 0.92f, 0.98f));
            var paths = MakeMap(grid.transform, "Cobblestone Paths and Plaza", 0, Color.white);
            var bridge = MakeMap(grid.transform, "Bridge Walkable Floor", 1, new Color(1, 1, 1, 0));
            var grassTile = GetTile("environment", "environment_1_9");
            var bridgeTile = GetTile("cobblestone_tiles_grey", "cobblestone_tiles_grey_1_1");
            var pathCells = new HashSet<Vector3Int>();

            for (var x = -36; x < 36; x++)
            {
                for (var y = -44; y < 44; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    scenery.SetTile(cell, grassTile);
                    if (x >= -16 && x < 16 && y >= -26 && y < 26)
                    {
                        ground.SetTile(cell, grassTile);
                    }
                }
            }
            for (var x = -36; x < 36; x++)
            {
                for (var y = -22; y <= -17; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    ground.SetTile(cell, null);
                    var row = y == -17 ? 0 : y == -22 ? 2 : 1;
                    water.SetTile(cell, GetTile("environment", "environment_7_" + row));
                    if (x >= -2 && x <= 3)
                    {
                        bridge.SetTile(cell, bridgeTile);
                    }
                }
            }

            AddPath(pathCells, -5, -5, 6, 4);
            AddPath(pathCells, -1, -26, 1, 15);
            AddPath(pathCells, -8, -11, 9, -10);
            AddPath(pathCells, -9, 1, 9, 2);
            AddPath(pathCells, 4, 3, 10, 4);
            AddPath(pathCells, -14, 16, 13, 17);
            AddPath(pathCells, -14, 3, -13, 17);
            AddPath(pathCells, 11, 4, 12, 17);
            using (var iterator = pathCells.GetEnumerator())
            {
                while (iterator.MoveNext() == true)
                {
                    var cell = iterator.Current;
                    if (cell.y >= -22 && cell.y <= -17)
                    {
                        continue;
                    }
                    var col = pathCells.Contains(cell + Vector3Int.left) == false ? 0
                        : pathCells.Contains(cell + Vector3Int.right) == false ? 2 : 1;
                    var row = pathCells.Contains(cell + Vector3Int.up) == false ? 0
                        : pathCells.Contains(cell + Vector3Int.down) == false ? 2 : 1;
                    paths.SetTile(cell, GetTile("cobblestone_tiles_grey", "cobblestone_tiles_grey_" + col + "_" + row));
                }
            }

            var buildings = new GameObject("02 - Buildings and Entrances").transform;
            buildings.SetParent(world, false);
            Building(buildings, "Sanctuary Hall", "houses_04", new Vector2(0.25f, 4), 1);
            Building(buildings, "Blacksmith", "houses_08", new Vector2(-2.45f, 0.75f), 0.75f);
            Building(buildings, "Wayfarer Inn", "houses_04", new Vector2(2.75f, 1.75f), 1);
            var market = Building(buildings, "Equipment Market", "houses_05", new Vector2(-2.65f, -2.75f), 1);
            Building(buildings, "Apothecary", "houses_03", new Vector2(2.75f, -3.4f), 0.9f);
            Building(buildings, "Northwest Residence", "houses_00", new Vector2(-3.25f, 4.7f), 0.9f, true);
            Building(buildings, "Northeast Residence", "houses_03", new Vector2(3.1f, 4.5f), 1, true);
            Building(buildings, "West Cottage", "houses_05", new Vector2(-3.1f, 2.65f), 0.9f, true);
            ConnectShop(scene, market);

            var props = new GameObject("03 - Shrine, Gardens and Market Props").transform;
            props.SetParent(world, false);
            BuildShrine(props, paths);
            var stoneBridge = Prop(props, "Stone Bridge", "nature_assets", "nature_assets_20", new Vector2(0.25f, -5.5f), false);
            stoneBridge.transform.localScale = new Vector3(2, 2.7f, 1);
            Prop(props, "Market Stall - Red", "market_assets", "market_assets_01", new Vector2(-3.2f, -4.15f) * 0.75f, true);
            Prop(props, "Apothecary Stall - Green", "market_assets", "market_assets_00", new Vector2(3.15f, -4.0f) * 0.75f, true);
            Prop(props, "Smith Logs", "nature_assets", "nature_assets_17", new Vector2(-3.95f, 0.3f) * 0.75f, true);
            Prop(props, "Market Barrels", "market_assets", "market_assets_09", new Vector2(-3.65f, -3.2f) * 0.75f, true);
            Prop(props, "Smith Crates", "market_assets", "market_assets_11", new Vector2(-1.6f, 0.4f) * 0.75f, true);
            Prop(props, "Inn Bench", "nature_assets", "nature_assets_19", new Vector2(2.65f, 1.2f) * 0.75f, true);
            Prop(props, "Shrine Bench West", "nature_assets", "nature_assets_19", new Vector2(-1.35f, 1.05f), true);
            Prop(props, "Shrine Bench East", "nature_assets", "nature_assets_19", new Vector2(1.85f, 1.1f), true);
            var forest = new GameObject("04 - Forest Edge and Riverbank").transform;
            forest.SetParent(world, false);
            BuildForest(forest);
            BuildGroundDetails(props);
            BuildingLabel(buildings, "성소 회관", new Vector2(0.25f, 3.85f));
            BuildingLabel(buildings, "대장간", new Vector2(-2.65f, 0.55f));
            BuildingLabel(buildings, "여관", new Vector2(2.75f, 1.6f));
            BuildingLabel(buildings, "장비 상점", new Vector2(-2.65f, -2.9f));
            BuildingLabel(buildings, "약방", new Vector2(2.75f, -3.55f));
            LimitMovementToOuterHouses();
            PositionParty(scene);
            ConfigureExpandedCamera(scene);
            var motes = scene.GetRootGameObjects().Single(item => item.name == "Shrine - Floating Golden Motes");
            motes.transform.position = new Vector3(0.5f, 3.5f, -0.1f);
            Physics2D.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void BuildShrine(Transform parent, Tilemap paths)
        {
            for (var x = -2; x <= 3; x++)
            {
                for (var y = 4; y <= 9; y++)
                {
                    paths.SetTile(new Vector3Int(x, y, 0), GetTile("cobblestone_tiles_brown", "cobblestone_tiles_brown_1_1"));
                }
            }
            var statue = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Sunnyside/VillageAtlas.png")
                .OfType<Sprite>().Single(sprite => sprite.name == "Statue");
            var shrine = SpriteObject(parent, "Shrine of Three Souls", statue, new Vector2(0.25f, 1.25f));
            shrine.transform.localScale = Vector3.one * 0.6f;
            var block = shrine.gameObject.AddComponent<BoxCollider2D>();
            block.size = new Vector2(1.5f, 0.7f);
            block.offset = new Vector2(0, 0.25f);
            for (var i = 0; i < 5; i++)
            {
                Prop(parent, "Shrine Garden West " + i, "nature_assets", "nature_assets_06", new Vector2(-1.25f, 2 + i * 0.4f), false);
                Prop(parent, "Shrine Garden East " + i, "nature_assets", "nature_assets_10", new Vector2(1.65f, 2 + i * 0.4f), false);
            }
        }

        private static void BuildForest(Transform parent)
        {
            var trees = new[]
            {
                new Vector2(-4.6f, 4.8f), new Vector2(-3.7f, 5.8f), new Vector2(-2.7f, 6.3f),
                new Vector2(-1.5f, 7.3f), new Vector2(1.8f, 6.5f), new Vector2(3.2f, 6.1f),
                new Vector2(4.7f, 5.4f), new Vector2(5.0f, 3.2f), new Vector2(-5.1f, 2.6f),
                new Vector2(-5.0f, 0.4f), new Vector2(-4.8f, -2.6f), new Vector2(4.7f, -1.1f),
                new Vector2(4.9f, -3.2f), new Vector2(-4.5f, -6.5f), new Vector2(-3.2f, -6.8f),
                new Vector2(2.4f, -6.7f), new Vector2(3.7f, -6.3f), new Vector2(5.2f, -6.5f),
                new Vector2(-6.2f, 5.7f), new Vector2(6.3f, 6.0f), new Vector2(-5.7f, 7.1f)
            };
            for (var i = 0; i < trees.Length; i++)
            {
                Prop(parent, "Forest Tree " + i, "nature_assets", i % 3 == 0 ? "nature_assets_05" : "nature_assets_00", trees[i], true);
            }
            for (var i = 0; i < 9; i++)
            {
                var side = i < 5 ? -1 : 1;
                var position = new Vector2(side * (2.4f + i % 3 * 0.65f), -4.7f + i % 2 * 0.1f);
                Prop(parent, "Riverbank Rock " + i, "nature_assets", i % 2 == 0 ? "nature_assets_01" : "nature_assets_07", position, true);
            }
            var gardens = new[] { new Vector2(-3.6f, 4.4f), new Vector2(3.6f, 4.7f), new Vector2(-4.2f, -0.65f), new Vector2(4.2f, 0.6f), new Vector2(2.9f, -0.85f), new Vector2(-2.4f, -4.1f) };
            for (var i = 0; i < gardens.Length; i++)
            {
                Prop(parent, "Garden Bush " + i, "nature_assets", "nature_assets_06", gardens[i], false);
                Prop(parent, "Garden Flowers " + i, "nature_assets", "nature_assets_18", gardens[i] + new Vector2(0.4f, 0.1f), false);
            }
        }

        private static void BuildGroundDetails(Transform parent)
        {
            var patches = new[]
            {
                new Vector2(-2.0f, 3.7f), new Vector2(-2.9f, 4.5f), new Vector2(-3.6f, 3.9f),
                new Vector2(2.1f, 5.3f), new Vector2(3.0f, 4.4f), new Vector2(3.5f, 0.3f),
                new Vector2(-2.7f, -1.2f), new Vector2(-3.7f, -0.6f), new Vector2(2.7f, -0.9f),
                new Vector2(3.6f, -1.3f), new Vector2(-1.4f, -4.0f), new Vector2(1.9f, -4.1f),
                new Vector2(-2.5f, -5.9f), new Vector2(2.4f, -6.0f)
            };
            for (var i = 0; i < patches.Length; i++)
            {
                Prop(parent, "Wildflower Patch " + i, "nature_assets", "GroundFlowers", patches[i], false);
                Prop(parent, "Grass Tuft " + i, "nature_assets", "GroundGrass", patches[i] + new Vector2(0.5f, -0.2f), false);
            }
        }

        private static void BuildingLabel(Transform parent, string text, Vector2 position)
        {
            position *= 1.5f;
            var root = new GameObject("Sign - " + text, typeof(SpriteRenderer));
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(position.x, position.y, 0);
            var background = root.GetComponent<SpriteRenderer>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.drawMode = SpriteDrawMode.Sliced;
            background.size = new Vector2(1.22f, 0.25f);
            background.sortingOrder = 600;
            background.sharedMaterial = Material;
            background.color = new Color32(25, 40, 42, 225);
            var caption = new GameObject("Caption", typeof(RectTransform)).AddComponent<TMPro.TextMeshPro>();
            caption.transform.SetParent(root.transform, false);
            var captionRect = caption.rectTransform;
            captionRect.sizeDelta = new Vector2(1.15f, 0.24f);
            caption.font = SignsFont;
            caption.text = text;
            caption.fontSize = 2.7f;
            caption.enableAutoSizing = false;
            caption.alignment = TMPro.TextAlignmentOptions.Center;
            caption.color = new Color32(248, 236, 201, 255);
            caption.GetComponent<MeshRenderer>().sortingOrder = 601;
        }

        private static SpriteRenderer Building(Transform parent, string name, string sprite, Vector2 position, float scale, bool outerDistrict = false)
        {
            if (outerDistrict == false)
            {
                position *= 0.75f;
            }
            var renderer = SpriteObject(parent, name, GetSprite("houses", sprite), position);
            renderer.transform.localScale = Vector3.one * scale;
            var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(renderer.sprite.rect.width / 64f * 0.82f, 0.9f);
            collider.offset = new Vector2(0, 0.5f);
            var entrance = new GameObject("Entrance - " + name).transform;
            entrance.SetParent(renderer.transform, false);
            entrance.localPosition = new Vector3(0, -0.5f, 0);
            return renderer;
        }

        private static SpriteRenderer Prop(Transform parent, string name, string atlas, string sprite, Vector2 position, bool obstacle)
        {
            var renderer = SpriteObject(parent, name, GetSprite(atlas, sprite), position);
            if (obstacle == true)
            {
                var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(Mathf.Min(renderer.sprite.rect.width / 64f * 0.65f, 0.65f), 0.23f);
                collider.offset = new Vector2(0, 0.14f);
            }
            return renderer;
        }

        private static SpriteRenderer SpriteObject(Transform parent, string name, Sprite sprite, Vector2 position)
        {
            position *= 2;
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.transform.position = new Vector3(Mathf.Round(position.x * 64) / 64, Mathf.Round(position.y * 64) / 64, 0);
            renderer.sprite = sprite;
            renderer.sharedMaterial = Material;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderer.sortingOrder = 300 - Mathf.RoundToInt(position.y * 10);
            return renderer;
        }

        private static void ConnectShop(Scene scene, SpriteRenderer market)
        {
            var target = market.gameObject.AddComponent<VillageShopTarget>();
            var hit = new GameObject("Equipment Shop Hit Area", typeof(BoxCollider2D)).GetComponent<BoxCollider2D>();
            hit.transform.SetParent(market.transform, false);
            hit.isTrigger = true;
            hit.size = market.sprite.bounds.size;
            hit.offset = market.sprite.bounds.center;
            var sign = Prop(market.transform.parent, "Equipment Market Sign", "nature_assets", "nature_assets_14", new Vector2(-1.5f, -2.8f) * 0.75f, false);
            var signArea = sign.gameObject.AddComponent<BoxCollider2D>();
            signArea.isTrigger = true;
            signArea.size = new Vector2(0.5f, 0.7f);
            signArea.offset = new Vector2(0, 0.3f);
            var serialized = new SerializedObject(target);
            serialized.FindProperty("Entrance").objectReferenceValue = market.transform.GetChild(0);
            var areas = serialized.FindProperty("HitAreas");
            areas.arraySize = 2;
            areas.GetArrayElementAtIndex(0).objectReferenceValue = hit;
            areas.GetArrayElementAtIndex(1).objectReferenceValue = signArea;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var interaction = Find<VillageShopInteraction>(scene);
            serialized = new SerializedObject(interaction);
            serialized.FindProperty("Target").objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Three Souls/Village/Limit Movement to Outer Houses (Current Summer Test Only)")]
        public static void LimitMovementToOuterHouses()
        {
            RequireEditMode();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                throw new InvalidOperationException("Summer 테스트 씬에서만 이동 경계를 설정할 수 있습니다.");
            }
            var all = GetTransforms(scene);
            var ground = all.Single(item => item.name == "Walkable Grass").GetComponent<Tilemap>();
            var paths = all.Single(item => item.name == "Cobblestone Paths and Plaza").GetComponent<Tilemap>();
            var corners = new[] { "Northwest Residence", "Northeast Residence", "Equipment Market", "Apothecary" }
                .Select(name => all.Single(item => item.name == name)).ToArray();
            Physics2D.SyncTransforms();
            var footprints = corners.Select(item => item.GetComponent<BoxCollider2D>().bounds).ToArray();
            // 집 앞 통행 여유만 남기고 배경 잔디/강/다리는 이동 바닥에서 제외합니다.
            var bounds = Rect.MinMaxRect(footprints.Min(item => item.min.x) - 0.5f,
                corners.Min(item => item.GetChild(0).position.y) - 0.75f,
                footprints.Max(item => item.max.x) + 0.5f, footprints.Max(item => item.max.y) + 0.5f);
            var existing = ground.transform.parent.Find("Village Movement Limits");
            var limits = existing != null ? existing.GetComponent<Tilemap>()
                : MakeMap(ground.transform.parent, "Village Movement Limits", 0, new Color(1, 1, 1, 0));
            limits.GetComponent<TilemapRenderer>().enabled = false;
            limits.ClearAllTiles();
            var sourceBounds = ground.cellBounds;
            foreach (var cell in sourceBounds.allPositionsWithin)
            {
                if (bounds.Contains(ground.GetCellCenterWorld(cell)) && (ground.HasTile(cell) || paths.HasTile(cell)))
                {
                    limits.SetTile(cell, ground.HasTile(cell) ? ground.GetTile(cell) : paths.GetTile(cell));
                }
            }
            limits.CompressBounds();
            var serialized = new SerializedObject(Find<VillageNavigationGrid>(scene));
            var floors = serialized.FindProperty("GroundMaps");
            floors.arraySize = 1;
            floors.GetArrayElementAtIndex(0).objectReferenceValue = limits;
            serialized.FindProperty("BridgeMap").objectReferenceValue = null;
            serialized.FindProperty("BridgeTile").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void PositionParty(Scene scene)
        {
            var serialized = new SerializedObject(Find<VillagePartyController>(scene));
            var members = serialized.FindProperty("Members");
            for (var i = 0; i < members.arraySize; i++)
            {
                var member = (VillagePartyMember)members.GetArrayElementAtIndex(i).objectReferenceValue;
                member.transform.localScale = Vector3.one;
                member.transform.position = new Vector3(0.25f, -1 - 1.15f * i, 0);
                member.transform.Find("Body").GetComponent<SpriteRenderer>().sharedMaterial = Material;
            }
        }

        private static void ConfigureCamera(Camera camera)
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset == false)
            {
                throw new InvalidOperationException("이 프로토타입의 픽셀 카메라는 기존 URP 프로젝트용입니다.");
            }
            camera.transform.position = new Vector3(0.25f, 0.1f, -10);
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.backgroundColor = new Color32(73, 103, 77, 255);
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            var pixel = camera.gameObject.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = 64;
            pixel.refResolutionX = 540;
            pixel.refResolutionY = 960;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixel.cropFrame = PixelPerfectCamera.CropFrame.None;
        }

        private static void ConfigureExpandedCamera(Scene scene)
        {
            var camera = Find<Camera>(scene);
            var follow = camera.GetComponent<SummerVillageCameraFollow>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<SummerVillageCameraFollow>();
            }
            var serialized = new SerializedObject(Find<VillagePartyController>(scene));
            var leader = (VillagePartyMember)serialized.FindProperty("Members").GetArrayElementAtIndex(0).objectReferenceValue;
            follow.Configure(camera, leader.transform, new Rect(-18, -22, 36, 44));
            camera.transform.position = new Vector3(leader.transform.position.x, leader.transform.position.y + 2.5f, -10);
        }

        private static void CompactVillageUI(Scene scene)
        {
            var root = Find<UI_LobbyTabs>(scene).transform;
            SetRect(root.Find("Header"), 0.035f, 0.927f, 0.965f, 0.985f);
            SetRect(root.Find("Navigation"), 0.035f, 0.018f, 0.965f, 0.080f);
            var village = root.Find("Village");
            village.Find("Welcome").gameObject.SetActive(false);
            village.Find("OpenEquipmentShop").gameObject.SetActive(false);
            var header = root.Find("Header").GetComponent<UnityEngine.UI.Image>();
            header.color = new Color32(25, 40, 42, 235);
            var navigation = root.Find("Navigation").GetComponent<UnityEngine.UI.Image>();
            navigation.color = new Color32(25, 40, 42, 245);
            var brand = root.Find("Header/Brand").GetComponent<UnityEngine.UI.Text>();
            brand.fontSize = 32;
            var location = root.Find("Header/Location").GetComponent<UnityEngine.UI.Text>();
            location.text = "성소 광장 · 숲속 마을";
            location.fontSize = 21;
            var guide = village.Find("Welcome/Eyebrow").GetComponent<UnityEngine.UI.Text>();
            guide.fontSize = 21;
            var components = root.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(components[i]);
                }
            }
        }

        private static void SetRect(Transform target, float left, float bottom, float right, float top)
        {
            var rect = (RectTransform)target;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Tilemap MakeMap(Transform parent, string name, int order, Color tint)
        {
            var map = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            map.transform.SetParent(parent, false);
            map.color = tint;
            var renderer = map.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            renderer.sharedMaterial = Material;
            return map;
        }

        private static Sprite GetSprite(string atlas, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ArtPath + atlas + ".png").OfType<Sprite>().Single(sprite => sprite.name == name);
        }

        private static Tile GetTile(string atlas, string name)
        {
            var path = DataPath + "Tiles/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = GetSprite(atlas, name);
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, path);
            }
            return tile;
        }

        private static void AddPath(HashSet<Vector3Int> cells, int left, int bottom, int right, int top)
        {
            for (var x = left; x <= right; x++)
            {
                for (var y = bottom; y <= top; y++)
                {
                    cells.Add(new Vector3Int(x, y, 0));
                }
            }
        }

        private static void PrepareAssets()
        {
            EnsureFolder("Assets/Village");
            EnsureFolder("Assets/Village/Summer");
            EnsureFolder("Assets/Village/Summer/Tiles");
            var path = DataPath + "SummerVillageSprite.mat";
            Material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (Material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null)
                {
                    throw new InvalidOperationException("기존 URP 2D Unlit Shader가 필요합니다.");
                }
                Material = new Material(shader);
                AssetDatabase.CreateAsset(Material, path);
            }
            var grassPath = DataPath + "SummerVillageGrass.mat";
            GrassMaterial = AssetDatabase.LoadAssetAtPath<Material>(grassPath);
            if (GrassMaterial == null)
            {
                var grassShader = Shader.Find("Three Souls/Village/Pixel Grass");
                if (grassShader == null)
                {
                    throw new InvalidOperationException("새 마을의 픽셀 잔디 Shader를 가져와야 합니다.");
                }
                GrassMaterial = new Material(grassShader);
                AssetDatabase.CreateAsset(GrassMaterial, grassPath);
            }
            PrepareSignsFont();
        }

        private static void PrepareSignsFont()
        {
            var path = DataPath + "SummerVillageSigns.asset";
            SignsFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
            if (SignsFont != null)
            {
                return;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Mulmaru.ttf");
            SignsFont = TMPro.TMP_FontAsset.CreateFontAsset(source, 42, 6,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 512, 512,
                TMPro.AtlasPopulationMode.Dynamic, false);
            SignsFont.name = "Summer Village Signs";
            if (SignsFont.TryAddCharacters("성소 회관대장간여장비상점약방", out var missing) == false)
            {
                throw new InvalidOperationException("마을 이름표 폰트의 글자가 없습니다: " + missing);
            }
            SignsFont.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(SignsFont, path);
            AssetDatabase.AddObjectToAsset(SignsFont.material, SignsFont);
            var textures = SignsFont.atlasTextures;
            for (var i = 0; i < textures.Length; i++)
            {
                textures[i].name = "Summer Village Signs Atlas " + i;
                AssetDatabase.AddObjectToAsset(textures[i], SignsFont);
            }
            EditorUtility.SetDirty(SignsFont);
            AssetDatabase.SaveAssetIfDirty(SignsFont);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path) == false)
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
            }
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        }

        private static Transform[] GetTransforms(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        }

        private static void RemoveRoot(Scene scene, string name)
        {
            var root = scene.GetRootGameObjects().SingleOrDefault(item => item.name == name);
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || EditorApplication.isCompiling == true)
            {
                throw new InvalidOperationException("컴파일 완료 후 Edit 모드에서 마을 테스트 씬을 조립하세요.");
            }
        }
    }
}
