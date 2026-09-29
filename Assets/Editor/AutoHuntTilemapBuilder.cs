using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;

public static class AutoHuntTilemapBuilder
{
    private const string ScenePath = "Assets/Scenes/AutoHuntCrypt.unity";

    [MenuItem("Tools/Auto Hunt/Build Crypt Tilemap")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.008f, 0.012f, 0.02f);

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8.3f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.backgroundColor = new Color(0.004f, 0.006f, 0.012f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraGo.AddComponent<AudioListener>();

        var grid = new GameObject("Crypt Grid");
        var gridComponent = grid.AddComponent<Grid>();
        gridComponent.cellSize = Vector3.one;

        var floor = CreateTilemap(grid.transform, "Floor - Haunted Moss", 0);
        var walls = CreateTilemap(grid.transform, "Walls - Ruined Bricks", 5);
        var accents = CreateTilemap(grid.transform, "Rune Accents", 8);

        var grassTiles = LoadTiles("Assets/Tiles/TerrainAssets_");
        var mossTiles = LoadTiles("Assets/Tiles/TX Tileset Grass_");
        var brickTiles = LoadTiles("Assets/Tiles/Tiles_BricksB_");
        var stoneTiles = LoadTiles("Assets/Tiles/Mainlevbuild_");
        if (grassTiles.Count == 0) grassTiles = mossTiles;
        if (brickTiles.Count == 0) brickTiles = stoneTiles;

        int width = 30, height = 18;
        for (int x = -width / 2; x < width / 2; x++)
        {
            for (int y = -height / 2; y < height / 2; y++)
            {
                var tile = grassTiles[(Mathf.Abs(x * 17 + y * 7) + 3) % grassTiles.Count];
                floor.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        // Ruined perimeter, with two openings for automated patrol routes.
        for (int x = -width / 2; x < width / 2; x++)
        {
            PlaceWall(walls, brickTiles, x, -height / 2);
            PlaceWall(walls, brickTiles, x, height / 2 - 1);
        }
        for (int y = -height / 2 + 1; y < height / 2 - 1; y++)
        {
            PlaceWall(walls, brickTiles, -width / 2, y);
            PlaceWall(walls, brickTiles, width / 2 - 1, y);
        }
        // Interior choke points create readable auto-hunt lanes.
        for (int x = -10; x <= -5; x++) PlaceWall(walls, brickTiles, x, 2);
        for (int x = 5; x <= 10; x++) PlaceWall(walls, brickTiles, x, -3);
        for (int y = -5; y <= -1; y++) PlaceWall(walls, brickTiles, 0, y);

        if (stoneTiles.Count > 0)
        {
            for (int x = -4; x <= 4; x++)
            {
                accents.SetTile(new Vector3Int(x, 5, 0), stoneTiles[(x + 20) % stoneTiles.Count]);
            }
        }

        CreateDecorations();
        CreateSpawnMarkers();
        CreateLighting();
        CreateOverlay();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = grid;
        Debug.Log("Auto Hunt Crypt tilemap created at " + ScenePath);
    }

    private static Tilemap CreateTilemap(Transform parent, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var tilemap = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
        renderer.sortingOrder = order;
        return tilemap;
    }

    private static void PlaceWall(Tilemap map, List<TileBase> tiles, int x, int y)
    {
        if (tiles.Count > 0) map.SetTile(new Vector3Int(x, y, 0), tiles[Mathf.Abs(x * 11 + y * 3) % tiles.Count]);
    }

    private static List<TileBase> LoadTiles(string prefix)
    {
        var result = new List<TileBase>();
        var guids = AssetDatabase.FindAssets("t:Tile", new[] { "Assets/Tiles" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith(prefix)) continue;
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile != null) result.Add(tile);
        }
        return result;
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            if (obj is Sprite s) return s;
        return null;
    }

    private static void CreateDecorations()
    {
        var props = new[] { "Assets/Image/Tiles/Plants And Crystals.png", "Assets/Image/Tiles/Mushrooms.png", "Assets/Image/Tiles/Altar.png" };
        var positions = new[] { new Vector3(-11, 6, -1), new Vector3(11, 5, -1), new Vector3(-12, -6, -1), new Vector3(12, -6, -1) };
        for (int i = 0; i < props.Length; i++)
        {
            var sprite = LoadSprite(props[i]);
            if (sprite == null) continue;
            var go = new GameObject("Crypt Prop " + i);
            go.transform.position = positions[i % positions.Length];
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.color = new Color(0.65f, 0.8f, 0.78f, 1f); sr.sortingOrder = 12;
        }
    }

    private static void CreateSpawnMarkers()
    {
        string[] paths = { "Assets/Image/Character/Goblin.png", "Assets/Image/Character/Wolf.png", "Assets/Image/Character/Skeleton.png" };
        Vector3[] spots = { new Vector3(-7, 5, -1), new Vector3(8, 5, -1), new Vector3(7, -6, -1) };
        for (int i = 0; i < paths.Length; i++)
        {
            var sprite = LoadSprite(paths[i]);
            if (sprite == null) continue;
            var go = new GameObject("Auto Hunt Spawn " + i);
            go.transform.position = spots[i];
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.color = new Color(0.55f, 0.65f, 0.72f, 1f); sr.sortingOrder = 15;
            go.AddComponent<AutoHuntPulse>();
        }
    }

    private static void CreateLighting()
    {
        var global = new GameObject("Global Moonlight");
        var globalLight = global.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.color = new Color(0.12f, 0.18f, 0.32f);
        globalLight.intensity = 0.30f;

        Vector3[] lanterns = { new Vector3(-12, 6, -2), new Vector3(12, 6, -2), new Vector3(-12, -6, -2), new Vector3(12, -6, -2), new Vector3(0, 0, -2) };
        Color[] colors = { new Color(0.25f, 0.55f, 1f), new Color(0.25f, 0.55f, 1f), new Color(0.6f, 0.25f, 1f), new Color(0.6f, 0.25f, 1f), new Color(1f, 0.35f, 0.12f) };
        for (int i = 0; i < lanterns.Length; i++)
        {
            var go = new GameObject("Soul Lantern " + i);
            go.transform.position = lanterns[i];
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = colors[i]; light.intensity = i == 4 ? 1.8f : 1.15f;
            light.pointLightOuterRadius = i == 4 ? 4.5f : 3.2f;
            light.pointLightInnerRadius = 0.4f;
            light.falloffIntensity = 0.8f;
            go.AddComponent<AutoHuntFlicker>();
        }
    }

    private static void CreateOverlay()
    {
        var overlay = new GameObject("Auto Hunt Readability Overlay");
        var sr = overlay.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite(); sr.color = new Color(0.01f, 0.015f, 0.03f, 0.08f); sr.sortingOrder = 50;
        overlay.transform.position = new Vector3(0, 0, 2);
        overlay.transform.localScale = new Vector3(30, 18, 1);
    }

    private static Sprite CreateSolidSprite()
    {
        var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
    }
}

public class AutoHuntFlicker : MonoBehaviour
{
    private Light2D light2D; private float seed;
    private void Awake() { light2D = GetComponent<Light2D>(); seed = Random.value * 10f; }
    private void Update() { if (light2D != null) light2D.intensity *= 0.97f + Mathf.PerlinNoise(seed, Time.time * 2.5f) * 0.06f; }
}

public class AutoHuntPulse : MonoBehaviour
{
    private SpriteRenderer spriteRenderer; private Color baseColor;
    private void Awake() { spriteRenderer = GetComponent<SpriteRenderer>(); baseColor = spriteRenderer.color; }
    private void Update() { var glow = 0.88f + Mathf.Sin(Time.time * 2.2f + transform.position.x) * 0.12f; spriteRenderer.color = baseColor * glow; }
}
