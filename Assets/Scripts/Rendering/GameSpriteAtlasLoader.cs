// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

namespace ThreeSouls.Rendering
{
    public sealed class GameSpriteAtlasLoader : MonoBehaviour
    {
        private sealed class Pending
        {
            public AsyncOperationHandle<SpriteAtlas> Handle;
            public readonly List<Action<SpriteAtlas>> Callbacks = new();
        }
        private readonly Dictionary<string, Pending> Atlases = new(StringComparer.Ordinal);
        public int LoadedCount => Atlases.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var root = new GameObject("Sprite Atlas Loader");
            DontDestroyOnLoad(root); root.AddComponent<GameSpriteAtlasLoader>();
        }
        private void Awake()
        {
            SpriteAtlasManager.atlasRequested += Load;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }
        public void Load(string tag, Action<SpriteAtlas> callback)
        {
            if (!tag.StartsWith("ThreeSoulsAtlas_", StringComparison.Ordinal)) return;
            if (Atlases.TryGetValue(tag, out var pending))
            {
                if (pending.Handle.IsDone && pending.Handle.Status == AsyncOperationStatus.Succeeded) callback?.Invoke(pending.Handle.Result);
                else if (callback != null) pending.Callbacks.Add(callback);
                return;
            }
            pending = new Pending { Handle = Addressables.LoadAssetAsync<SpriteAtlas>(tag) };
            if (callback != null) pending.Callbacks.Add(callback);
            Atlases.Add(tag, pending);
            pending.Handle.Completed += operation =>
            {
                if (!this || !Atlases.TryGetValue(tag, out var current) || current != pending) return;
                if (operation.Status != AsyncOperationStatus.Succeeded)
                {
                    Atlases.Remove(tag); Addressables.Release(operation);
                    Debug.LogError("Sprite atlas load failed: " + tag);
                    return;
                }
                var callbacks = pending.Callbacks.ToArray(); pending.Callbacks.Clear();
                foreach (var ready in callbacks) ready(operation.Result);
            };
        }
        private void SceneUnloaded(Scene scene) => StartCoroutine(ReleaseUnusedWorldAtlases());
        private IEnumerator ReleaseUnusedWorldAtlases()
        {
            yield return null;
            var used = new HashSet<Sprite>();
            foreach (var renderer in FindObjectsByType<SpriteRenderer>())
                if (renderer.enabled && renderer.sprite != null) used.Add(renderer.sprite);
            foreach (var map in FindObjectsByType<Tilemap>())
                foreach (var cell in map.cellBounds.allPositionsWithin)
                { var sprite = map.GetSprite(cell); if (sprite != null) used.Add(sprite); }
            var release = new List<string>();
            foreach (var pair in Atlases)
            {
                if (!pair.Key.StartsWith("ThreeSoulsAtlas_World_", StringComparison.Ordinal) || !pair.Value.Handle.IsDone) continue;
                var keep = false;
                if (pair.Value.Handle.Status == AsyncOperationStatus.Succeeded)
                    foreach (var sprite in used) if (pair.Value.Handle.Result.CanBindTo(sprite)) { keep = true; break; }
                if (!keep) release.Add(pair.Key);
            }
            foreach (var tag in release) { Addressables.Release(Atlases[tag].Handle); Atlases.Remove(tag); }
        }
        private void OnDestroy()
        {
            SpriteAtlasManager.atlasRequested -= Load; SceneManager.sceneUnloaded -= SceneUnloaded;
            foreach (var pending in Atlases.Values) if (pending.Handle.IsValid()) Addressables.Release(pending.Handle);
            Atlases.Clear();
        }
    }
}
