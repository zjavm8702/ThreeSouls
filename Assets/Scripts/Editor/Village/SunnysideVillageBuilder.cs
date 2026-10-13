using System;
using System.Collections.Generic;
using System.IO;
using ThreeSouls.Village;
using ThreeSouls.Utility;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// Sunnyside 원본을 보존하고 별도의 마을 타일맵·장식·환경 클립을 제작합니다.
    /// </summary>
    public static class SunnysideVillageBuilder
    {
        private const string Source = "Assets/Sunnyside_World_ASSET_PACK_V2.1/Sunnyside_World_Assets/";
        private const string Output = "Assets/Village/Sunnyside/";
        private const string ImageOutput = "Assets/Images/Sunnyside/";
        private const string ScenePath = "Assets/Scenes/Scene_Lobby_Sunnyside.unity";
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>();
        private static readonly Dictionary<string, AnimatorController> controllers = new Dictionary<string, AnimatorController>();
        private static Material spriteMaterial;
        private static Transform decorations;
        private static Transform buildings;
        private static Transform atmosphere;
        private static int sequence;

        /// <summary>
        /// 저장본이 없는 경우 별도의 마을 씬과 환경 에셋을 생성합니다.
        /// </summary>
        [MenuItem("Three Souls/Village/Create Sunnyside Lobby")]
        public static void Create()
        {
            if (File.Exists(ScenePath) == true)
            {
                throw new InvalidOperationException("마을 저장본이 이미 존재합니다. 사용자가 편집한 씬을 덮어쓰지 않습니다.");
            }

            Directory.CreateDirectory(ImageOutput);
            Directory.CreateDirectory(Output + "Tiles");
            Directory.CreateDirectory(Output + "Animations");
            Directory.CreateDirectory(Output + "Materials");
            sprites.Clear();
            tiles.Clear();
            controllers.Clear();
            sequence = 0;
            PrepareAtlas();
            PrepareAnimations();
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(Output + "Materials/VillageSprite.mat");

            if (spriteMaterial == null)
            {
                spriteMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(spriteMaterial, Output + "Materials/VillageSprite.mat");
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                GameObject root = new GameObject("Sunnyside Village");
                decorations = Group("03 - Gardens and Props", root.transform);
                buildings = Group("02 - Village Buildings", root.transform);
                atmosphere = Group("04 - Living Village", root.transform);
                BuildGround(root.transform);
                BuildBuildings();
                BuildGardens();
                BuildLife();
                BuildCamera(root.transform);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                ThreeSoulsLog.Info("Sunnyside 마을 씬 제작 완료: " + ScenePath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            ValidateAndPreview();
        }

        private static void PrepareAtlas()
        {
            string path = MoveTexture("Tileset/spr_tileset_sunnysideworld_16px.png", "VillageAtlas.png");
            var rects = new List<SpriteRect>();
            AddRect(rects, "Grass", 16, 16, 16, 16);
            AddRect(rects, "GrassFlowers", 16, 32, 16, 16);
            AddRect(rects, "GrassSpecks", 32, 32, 16, 16);
            AddRect(rects, "Dirt", 160, 128, 16, 16);
            AddRect(rects, "Water", 192, 304, 16, 16);
            AddRect(rects, "Cliff", 176, 64, 16, 16);
            AddRect(rects, "Planks", 640, 200, 16, 16);
            AddRect(rects, "Fence", 608, 16, 16, 32);
            AddRect(rects, "StonePaving", 48, 240, 16, 16);
            AddRect(rects, "BlueHouse", 328, 231, 128, 25);
            AddRect(rects, "GreenHouse", 328, 359, 128, 25);
            AddRect(rects, "OrangeHouse", 328, 487, 128, 25);
            AddRect(rects, "RedHouse", 328, 615, 128, 25);
            AddRect(rects, "PurpleHouse", 328, 743, 128, 25);
            AddRect(rects, "BlueTower", 520, 168, 32, 80);
            AddRect(rects, "Timber", 48, 144, 16, 16);
            AddRect(rects, "Door", 16, 160, 16, 16);
            AddRect(rects, "Window", 16, 160, 16, 16);
            AddRect(rects, "BlueShingles", 328, 160, 16, 16);
            AddRect(rects, "GreenShingles", 328, 288, 16, 16);
            AddRect(rects, "OrangeShingles", 328, 416, 16, 16);
            AddRect(rects, "RedShingles", 328, 544, 16, 16);
            AddRect(rects, "PurpleShingles", 328, 672, 16, 16);
            AddRect(rects, "Statue", 704, 288, 32, 64);
            AddRect(rects, "Well", 640, 304, 32, 40);
            AddRect(rects, "Bench", 720, 192, 48, 32);
            AddRect(rects, "Crates", 568, 144, 48, 48);
            AddRect(rects, "Barrel", 624, 256, 16, 16);
            AddRect(rects, "FlowerPink", 816, 64, 16, 16);
            AddRect(rects, "FlowerYellow", 784, 64, 16, 16);
            AddRect(rects, "Rock", 784, 336, 32, 32);
            AddRect(rects, "Sign", 688, 160, 16, 32);
            ApplyRects(path, rects);
        }

        private static string MoveTexture(string source, string file)
        {
            string path = ImageOutput + file;
            if (File.Exists(path) == false)
            {
                string error = AssetDatabase.MoveAsset(Source + source, path);

                if (string.IsNullOrEmpty(error) == false)
                {
                    throw new IOException("마을 이미지 이동 실패: " + error);
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return path;
        }

        private static void AddRect(List<SpriteRect> rects, string name,
            int x, int top, int width, int height, int textureHeight = 1024)
        {
            rects.Add(new SpriteRect
            {
                name = name,
                rect = new Rect(x, textureHeight - top - height, width, height),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, 0f),
                spriteID = GUID.Generate()
            });
        }

        private static void ApplyRects(string path, List<SpriteRect> rects)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(path));
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] existing = provider.GetSpriteRects();

            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = 0; j < existing.Length; j++)
                {
                    if (rects[i].name == existing[j].name)
                    {
                        rects[i].spriteID = existing[j].spriteID;
                        break;
                    }
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            var pairs = new List<SpriteNameFileIdPair>();

            for (int i = 0; i < rects.Count; i++)
            {
                pairs.Add(new SpriteNameFileIdPair(rects[i].name, rects[i].spriteID));
            }

            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
            provider.Apply();
            ((TextureImporter)AssetImporter.GetAtPath(path)).SaveAndReimport();
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    sprites.Add(sprite.name, sprite);
                }
            }
        }

        private static void PrepareAnimations()
        {
            Strip("Tree", "Elements/Plants/spr_deco_tree_01_strip4.png", 4, 3f);
            Strip("Pine", "Elements/Plants/spr_deco_tree_02_strip4.png", 4, 3f);
            Strip("Windmill", "Elements/Other/spr_deco_windmill_withshadow_strip9.png", 9, 9f);
            Strip("Smoke", "Elements/VFX/Chimney Smoke/chimneysmoke_01_strip30.png", 30, 12f);
            Strip("Fire", "Elements/VFX/Fire/spr_deco_fire_02_strip4.png", 4, 7f);
            Strip("Chicken", "Elements/Animals/spr_deco_chicken_01_strip4.png", 4, 5f);
            Strip("Sheep", "Elements/Animals/spr_deco_sheep_01_strip4.png", 4, 4f);
            Strip("Duck", "Elements/Animals/spr_deco_duck_01_strip4.png", 4, 5f);
            Strip("Bird", "Elements/Animals/spr_deco_bird_01_strip4.png", 4, 7f);
            Strip("Boat", "Elements/Other/spr_deco_coracle_strip4.png", 4, 4f);
            Strip("Glint", "Elements/VFX/Glint/spr_deco_glint_01_strip6.png", 6, 5f);
            PrepareSingle("Wheat", "Elements/Crops/wheat_05.png");
            PrepareSingle("Sunflower", "Elements/Crops/sunflower_05.png");
            PrepareSingle("Carrot", "Elements/Crops/carrot_05.png");
            Resident("Knight");
            Resident("Archer");
            Resident("Wizard");
        }

        private static void PrepareSingle(string name, string source)
        {
            string path = MoveTexture(source, name + ".png");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var rects = new List<SpriteRect>();
            AddRect(rects, name, 0, 0, texture.width, texture.height, texture.height);
            ApplyRects(path, rects);
        }

        private static void Strip(string name, string source, int frames, float fps)
        {
            string path = MoveTexture(source, name + ".png");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var rects = new List<SpriteRect>();
            int width = texture.width / frames;

            for (int i = 0; i < frames; i++)
            {
                AddRect(rects, name + "_" + i.ToString("D2"), i * width, 0,
                    width, texture.height, texture.height);
            }

            ApplyRects(path, rects);
            BuildAnimation(name, frames, fps);
        }

        private static void Resident(string name)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Images/Character/Hero/Hero_" + name + ".png");

            for (int frame = 0; frame < 4; frame++)
            {
                Sprite selected = null;

                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite sprite && sprite.name == "Walking_Right_" + frame)
                    {
                        selected = sprite;
                        break;
                    }
                }

                if (selected == null)
                {
                    throw new InvalidOperationException("마을 주민의 이동 프레임이 없습니다: " + name);
                }

                sprites.Add(name + "_" + frame.ToString("D2"), selected);
            }

            BuildAnimation(name, 4, 6f);
        }

        private static void BuildAnimation(string name, int frames, float fps)
        {
            var clip = new AnimationClip { name = name + "_Ambient", frameRate = fps };
            var keys = new ObjectReferenceKeyframe[frames + 1];

            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / fps,
                    value = sprites[name + "_" + (i % frames).ToString("D2")]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string clipPath = Output + "Animations/" + name + ".anim";
            AnimationClip existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            if (existingClip == null)
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                EditorUtility.CopySerialized(clip, existingClip);
                UnityEngine.Object.DestroyImmediate(clip);
                clip = existingClip;
                EditorUtility.SetDirty(clip);
            }

            AnimatorController existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                Output + "Animations/" + name + ".controller");

            if (existingController != null)
            {
                controllers.Add(name, existingController);
                return;
            }
            var controller = AnimatorController.CreateAnimatorControllerAtPath(
                Output + "Animations/" + name + ".controller");
            AnimatorState state = controller.layers[0].stateMachine.AddState("Ambient");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            controllers.Add(name, controller);
        }

        private static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Tilemap Map(string name, Transform grid, int order)
        {
            Transform group = Group(name, grid);
            Tilemap map = group.gameObject.AddComponent<Tilemap>();
            map.tileAnchor = new Vector3(0.5f, 0f, 0f);
            var renderer = group.gameObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            renderer.sharedMaterial = spriteMaterial;
            return map;
        }

        private static Tile GetTile(string name)
        {
            if (tiles.TryGetValue(name, out Tile tile) == false)
            {
                tile = AssetDatabase.LoadAssetAtPath<Tile>(Output + "Tiles/" + name + ".asset");
                bool create = tile == null;

                if (create == true)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                }

                tile.sprite = sprites[name];
                tile.colliderType = Tile.ColliderType.None;
                if (create == true)
                {
                    AssetDatabase.CreateAsset(tile, Output + "Tiles/" + name + ".asset");
                }
                else
                {
                    EditorUtility.SetDirty(tile);
                }
                tiles.Add(name, tile);
            }

            return tile;
        }

        private static void Put(Tilemap map, int x, int y, string tile)
        {
            map.SetTile(new Vector3Int(x, y, 0), GetTile(tile));
        }

        private static void Fill(Tilemap map, int x, int y, int width, int height, string tile)
        {
            for (int row = y; row < y + height; row++)
            {
                for (int column = x; column < x + width; column++)
                {
                    Put(map, column, row, tile);
                }
            }
        }

        private static void BuildGround(Transform parent)
        {
            Transform grid = Group("01 - Editable Tilemaps", parent);
            grid.gameObject.AddComponent<Grid>();
            Tilemap water = Map("Water", grid, -100);
            Tilemap ground = Map("Grass", grid, -90);
            Tilemap paths = Map("Paths and Plaza", grid, -80);
            Tilemap details = Map("Cliffs and Bridge", grid, -70);
            Fill(water, -40, -26, 80, 54, "Water");
            var random = new System.Random(31415);

            for (int y = -17; y <= 19; y++)
            {
                int inset = Mathf.Max(0, Mathf.Abs(y - 1) - 12);

                for (int x = -30 + inset; x <= 29 - inset; x++)
                {
                    bool pond = x >= 19 && x <= 26 && y >= -9 && y <= 1
                        && Mathf.Abs(x - 22) + Mathf.Abs(y + 4) < 9;

                    if (pond == false)
                    {
                        int choice = random.Next(18);
                        Put(ground, x, y, choice == 0 ? "GrassFlowers" : choice == 1 ? "GrassSpecks" : "Grass");
                    }
                }

                if (y == -17)
                {
                    Fill(details, -30 + inset, y - 2, 60 - inset * 2, 2, "Cliff");
                }
            }

            Fill(paths, -3, -17, 6, 32, "Dirt");
            Fill(paths, -19, 3, 37, 3, "Dirt");
            Fill(paths, -20, -10, 38, 3, "Dirt");
            Fill(paths, -6, -5, 12, 12, "StonePaving");
            Fill(paths, -20, -8, 3, 12, "Dirt");
            Fill(paths, 14, -8, 3, 12, "Dirt");
            Fill(details, -2, -23, 4, 8, "Planks");
            Fill(paths, -3, 12, 6, 4, "StonePaving");
            Fill(paths, -27, -8, 7, 8, "Dirt");
            Box("Water Boundary - South", parent, new Vector2(0f, -20f), new Vector2(44f, 2f));
            Box("Pond Boundary", parent, new Vector2(22f, -4f), new Vector2(7f, 8f));
        }

        private static SpriteRenderer Prop(string name, string sprite, Vector2 position,
            Transform parent, float scale = 1f, int extraOrder = 0)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = Vector3.one * scale;
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[sprite];
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = 300 - Mathf.RoundToInt(position.y * 10f) + extraOrder;
            return renderer;
        }

        private static SpriteRenderer Ambient(string name, Vector2 position,
            float scale = 1f, int extraOrder = 0)
        {
            SpriteRenderer renderer = Prop(name + " " + sequence, name + "_00",
                position, atmosphere, scale, extraOrder);
            Animator animator = renderer.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controllers[name];
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            renderer.gameObject.AddComponent<VillageAnimationPhase>().Configure(animator,
                (sequence * 0.618034f) % 1f, 0.85f + (sequence % 4) * 0.1f);
            sequence++;
            return renderer;
        }

        private static void Box(string name, Transform parent, Vector2 position, Vector2 size)
        {
            Transform item = Group(name, parent);
            item.localPosition = position;
            item.gameObject.AddComponent<BoxCollider2D>().size = size;
        }

        private static void House(string name, string sprite, Vector2 position)
        {
            Transform house = Group(name, buildings);
            house.localPosition = position;
            var collider = house.gameObject.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0f, 1.2f);
            collider.size = new Vector2(8f, 2.4f);
            int order = 300 - Mathf.RoundToInt(position.y * 10f);

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    SpriteRenderer wall = Prop("Timber Wall", "Timber",
                        new Vector2(-4.375f + column * 1.25f, row * 1.25f), house, 1.25f);
                    wall.sortingOrder = order;
                }
            }

            string shingles = sprite.Replace("House", "Shingles");

            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    SpriteRenderer roofing = Prop("Roof Shingles", shingles,
                        new Vector2(-4.375f + column * 1.25f, 3.7f + row * 1.25f), house, 1.25f);
                    roofing.sortingOrder = order + 1;
                }
            }

            SpriteRenderer roof = Prop("Roof", sprite, new Vector2(0f, 3f), house, 1.25f);
            roof.transform.localScale = new Vector3(1.25f, 2f, 1f);
            roof.sortingOrder = order + 3;
            SpriteRenderer door = Prop("Door", "Door", Vector2.zero, house, 1.25f);
            door.transform.localScale = new Vector3(1.25f, 1.7f, 1f);
            door.color = new Color(0.72f, 0.65f, 0.58f);
            door.sortingOrder = order + 2;
            SpriteRenderer leftWindow = Prop("Window Left", "Window", new Vector2(-2.8f, 0.6f), house);
            leftWindow.color = new Color(0.75f, 0.9f, 1f);
            leftWindow.sortingOrder = order + 2;
            SpriteRenderer rightWindow = Prop("Window Right", "Window", new Vector2(2.8f, 0.6f), house);
            rightWindow.color = new Color(0.75f, 0.9f, 1f);
            rightWindow.sortingOrder = order + 2;
            Transform anchor = Group("Entrance - " + name, buildings);
            anchor.localPosition = position + Vector2.down * 0.5f;
            Prop(name + " Sign", "Sign", position + new Vector2(4.8f, 0.3f), decorations);
            Ambient("Smoke", position + new Vector2(-2.7f, 6f), 1.2f, 90);
        }

        private static void BuildBuildings()
        {
            House("Guild Hall", "BlueHouse", new Vector2(-14f, 7f));
            House("Forge", "RedHouse", new Vector2(12f, 7f));
            House("Apothecary", "PurpleHouse", new Vector2(12f, -6f));
            House("Market", "OrangeHouse", new Vector2(-13f, -6f));
            House("Garden Cottage", "GreenHouse", new Vector2(15f, 14f));
            Prop("Shrine of Three Souls", "Statue", new Vector2(0f, 1f), buildings, 1.6f);
            Box("Shrine Footprint", buildings, new Vector2(0f, 1.8f), new Vector2(2.2f, 1.4f));
            Prop("Watch Tower", "BlueTower", new Vector2(5f, 11f), buildings, 1.15f);
            Prop("Village Well", "Well", new Vector2(-6f, -4f), decorations);
            Prop("Bench West", "Bench", new Vector2(-5f, 0f), decorations);
            Prop("Bench East", "Bench", new Vector2(5f, 0f), decorations);
            Prop("Market Supplies", "Crates", new Vector2(-18f, -5f), decorations);
            Prop("Forge Supplies", "Crates", new Vector2(17f, 7f), decorations);
            Ambient("Fire", new Vector2(8.7f, 7.1f), 2f, 20);
            Ambient("Fire", new Vector2(-3.5f, -15f), 1.5f);
            Ambient("Fire", new Vector2(3.5f, -15f), 1.5f);
            Group("Departure - Connect Combat Here", buildings).localPosition = new Vector3(0f, -16f);
        }

        private static void BuildGardens()
        {
            var random = new System.Random(2718);

            for (int i = 0; i < 300; i++)
            {
                float x = random.Next(-28, 29) + 0.5f;
                float y = random.Next(-14, 20);
                bool edge = Mathf.Abs(x) > 24f || y > 17f || (y < -12f && Mathf.Abs(x) > 7f);

                if (edge == true)
                {
                    Ambient(i % 3 == 0 ? "Pine" : "Tree", new Vector2(x, y),
                        i % 4 == 0 ? 1.35f : 1.1f);
                }
            }

            for (int i = 0; i < 42; i++)
            {
                Vector2 position = new Vector2(random.Next(-25, 26), random.Next(-13, 17));
                bool clear = Mathf.Abs(position.x) > 7f && (position.y < -10f || position.y > 15f);

                if (clear == true)
                {
                    Prop("Wildflowers " + i, i % 2 == 0 ? "FlowerPink" : "FlowerYellow",
                        position, decorations);
                }
            }

            Prop("Windmill Tower", "BlueTower", new Vector2(-23f, 1f), buildings, 1.25f);
            Ambient("Windmill", new Vector2(-23f, 3f), 1.1f, 70);

            for (int y = -7; y <= -2; y++)
            {
                for (int x = -26; x <= -22; x++)
                {
                    string crop = x < -24 ? "Wheat" : x < -22 ? "Carrot" : "Sunflower";
                    Prop("Farm " + x + " " + y, crop, new Vector2(x, y), decorations, 0.85f);
                }
            }

            for (int i = 0; i < 9; i++)
            {
                Prop("Farm Fence " + i, "Fence", new Vector2(-26f + i, -9f), decorations);
            }

            for (int i = 0; i < 7; i++)
            {
                Prop("Departure Rail L " + i, "Fence", new Vector2(-2.5f, -22f + i), decorations);
                Prop("Departure Rail R " + i, "Fence", new Vector2(2.5f, -22f + i), decorations);
            }

            Prop("Pond Stones", "Rock", new Vector2(20f, 1f), decorations);
            Prop("Pond Stones South", "Rock", new Vector2(25f, -11f), decorations);
            Prop("Waterfront Bench", "Bench", new Vector2(17f, -11f), decorations);

            for (int i = 0; i < 12; i++)
            {
                Prop("Shrine Flowers West " + i, "Sunflower", new Vector2(-7.5f, -4f + i * 0.8f), decorations, 0.8f);
                Prop("Shrine Flowers East " + i, "Sunflower", new Vector2(7.5f, -4f + i * 0.8f), decorations, 0.8f);
            }

            for (int i = 0; i < 10; i++)
            {
                Prop("Cottage Garden " + i, "Sunflower", new Vector2(9f + i, 12f), decorations, 0.9f);
            }

            Prop("Market Barrel", "Barrel", new Vector2(-18f, -7f), decorations, 1.4f);
            Prop("Forge Barrel", "Barrel", new Vector2(17f, 8f), decorations, 1.4f);
        }

        private static void Wander(string animal, Vector2 position, Vector2 offset,
            float duration, float phase)
        {
            SpriteRenderer renderer = Ambient(animal, position);
            renderer.gameObject.AddComponent<VillageAmbientMotion>().Configure(renderer,
                offset, duration, phase);
        }

        private static void BuildLife()
        {
            Wander("Chicken", new Vector2(-21f, -3f), new Vector2(2f, -2f), 7f, 0f);
            Wander("Chicken", new Vector2(-26f, -11f), new Vector2(3f, 0f), 10f, 4f);
            Wander("Sheep", new Vector2(-21f, 12f), new Vector2(3f, 1f), 12f, 2f);
            Wander("Sheep", new Vector2(-24f, 10f), new Vector2(2f, 0f), 9f, 6f);
            Wander("Duck", new Vector2(22f, -4f), new Vector2(2f, -2f), 9f, 1f);
            Wander("Duck", new Vector2(20f, -5f), new Vector2(2f, 1f), 11f, 5f);
            Wander("Boat", new Vector2(20f, -8f), new Vector2(2f, 1f), 16f, 3f);
            SpriteRenderer knight = Ambient("Knight", new Vector2(-3f, -5f), 2.2f);
            knight.gameObject.AddComponent<VillageAmbientMotion>().Configure(knight,
                new Vector3(5f, 0f), 8f, 1f, 0f);
            SpriteRenderer archer = Ambient("Archer", new Vector2(-12f, -9f), 2.2f);
            archer.gameObject.AddComponent<VillageAmbientMotion>().Configure(archer,
                new Vector3(7f, 0f), 11f, 4f, 0f);
            SpriteRenderer wizard = Ambient("Wizard", new Vector2(6f, 4f), 2.2f);
            wizard.gameObject.AddComponent<VillageAmbientMotion>().Configure(wizard,
                new Vector3(7f, 0f), 10f, 2f, 0f);
            Wander("Bird", new Vector2(-9f, 10f), new Vector2(13f, 4f), 7f, 2f);
            Wander("Bird", new Vector2(5f, -2f), new Vector2(12f, 4f), 8f, 5f);

            for (int i = 0; i < 22; i++)
            {
                Vector2 position = i < 7
                    ? new Vector2(20f + (i % 4), -7f + (i % 6))
                    : new Vector2(-31f + i * 3f, -23f + (i % 3));
                Ambient("Glint", position, 1f, -700);
            }

            Ambient("Glint", new Vector2(-0.5f, 3.1f), 1.2f, 200);
            Ambient("Glint", new Vector2(0.7f, 4.2f), 1.2f, 200);
            BuildShrineMotes();
        }

        private static void BuildShrineMotes()
        {
            string path = Output + "Materials/ShrineMotes.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
                material.SetTexture("_BaseMap", sprites["Glint_00"].texture);
                AssetDatabase.CreateAsset(material, path);
            }

            Transform item = Group("Shrine - Floating Golden Motes", atmosphere);
            item.localPosition = new Vector3(0f, 3f, -0.1f);
            ParticleSystem particles = item.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startColor = new Color(1f, 0.9f, 0.5f, 0.6f);
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = true;
            var emission = particles.emission;
            emission.rateOverTime = 5f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3f, 3f, 0.01f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;

            for (int i = 0; i < 6; i++)
            {
                sheet.AddSprite(sprites["Glint_" + i.ToString("D2")]);
            }

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 700;
        }

        private static void BuildCamera(Transform parent)
        {
            Transform cameraObject = Group("Main Camera", parent);
            cameraObject.gameObject.tag = "MainCamera";
            cameraObject.localPosition = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 23f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.39f, 0.47f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            cameraObject.gameObject.AddComponent<AudioListener>();
            cameraObject.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.gameObject.AddComponent<VillageCameraFrame>().Configure(camera,
                new Vector2(68f, 46f));
        }

        /// <summary>
        /// 닫힌 마을 저장본의 연결과 프레임 전환을 검사하고 격리 카메라로 미리보기를 만듭니다.
        /// </summary>
        [MenuItem("Three Souls/Village/Validate and Render Sunnyside Lobby")]
        public static void ValidateAndPreview()
        {
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded == true)
            {
                throw new InvalidOperationException("마을 씬을 닫은 뒤 저장본 미리보기를 실행하세요. 열린 편집본은 변경하지 않습니다.");
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                var renderers = new List<SpriteRenderer>();
                var animators = new List<Animator>();
                var maps = new List<Tilemap>();
                Camera camera = null;
                GameObject[] roots = scene.GetRootGameObjects();
                int missing = 0;

                for (int i = 0; i < roots.Length; i++)
                {
                    renderers.AddRange(roots[i].GetComponentsInChildren<SpriteRenderer>(true));
                    animators.AddRange(roots[i].GetComponentsInChildren<Animator>(true));
                    maps.AddRange(roots[i].GetComponentsInChildren<Tilemap>(true));
                    Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);

                    for (int j = 0; j < transforms.Length; j++)
                    {
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[j].gameObject);
                    }

                    Camera candidate = roots[i].GetComponentInChildren<Camera>();

                    if (candidate != null)
                    {
                        camera = candidate;
                    }
                }

                if (missing != 0 || camera == null || maps.Count != 4)
                {
                    throw new InvalidOperationException("마을 씬의 필수 연결 검사 실패");
                }

                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i].sprite == null || renderers[i].sharedMaterial == null)
                    {
                        throw new InvalidOperationException("마을 장식의 스프라이트 또는 머티리얼 누락");
                    }
                }

                int sampledClips = 0;

                for (int i = 0; i < animators.Count; i++)
                {
                    AnimationClip[] clips = animators[i].runtimeAnimatorController.animationClips;

                    if (clips.Length != 1 || clips[0].length <= 0f)
                    {
                        throw new InvalidOperationException("환경 클립 연결 검사 실패");
                    }

                    SpriteRenderer sprite = animators[i].GetComponent<SpriteRenderer>();
                    clips[0].SampleAnimation(animators[i].gameObject, 0f);
                    Sprite firstFrame = sprite.sprite;
                    clips[0].SampleAnimation(animators[i].gameObject, 1.1f / clips[0].frameRate);

                    if (sprite.sprite == firstFrame)
                    {
                        throw new InvalidOperationException("환경 클립의 프레임 변경 검사 실패: " + clips[0].name);
                    }

                    sampledClips++;
                    clips[0].SampleAnimation(animators[i].gameObject, (i * 0.173f) % clips[0].length);
                }

                Directory.CreateDirectory("Docs/Previews/Village");
                for (int i = 0; i < roots.Length; i++)
                {
                    Transform[] items = roots[i].GetComponentsInChildren<Transform>(true);

                    for (int j = 0; j < items.Length; j++)
                    {
                        items[j].gameObject.layer = 31;
                    }
                }

                ParticleSystem[] systems = roots[0].GetComponentsInChildren<ParticleSystem>();

                for (int i = 0; i < systems.Length; i++)
                {
                    systems[i].Simulate(2.5f, true, true);
                }

                camera.cullingMask = 1 << 31;
                Render(camera, "Docs/Previews/Village/Sunnyside_Lobby.png");
                File.WriteAllText("Docs/Previews/Village/Validation.txt",
                    "Scene=" + ScenePath + "\nBuildTarget=" + EditorUserBuildSettings.activeBuildTarget
                    + "\nTilemaps=" + maps.Count + "\nSpriteRenderers=" + renderers.Count
                    + "\nAnimators=" + animators.Count + "\nMissingScripts=" + missing
                    + "\nAnimationFrameChecks=" + sampledClips + "\nParticleSystems=" + systems.Length
                    + "\nPlayMode=false (Edit scene validation and clip sampling only)\n");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(1800, 1200, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var texture = new Texture2D(1800, 1200, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1800, 1200), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
