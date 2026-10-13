using ThreeSouls.Interface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using ThreeSouls.Utility;

namespace ThreeSouls.Manager
{
    public class AddressableManager : BaseManager, IManager
    {
        private OnAddressableStatusDelegate onAddressableStatusEventHandler = null;
        public event OnAddressableStatusDelegate OnAddressableStatusEventHandler
        {
            add
            {
                onAddressableStatusEventHandler -= value;
                onAddressableStatusEventHandler += value;
            }
            remove
            {
                onAddressableStatusEventHandler -= value;
            }
        }

        private readonly List<IResourceLocation> ResourceLocations = new();
        private readonly Dictionary<string, AssetData> LoadedAssets = new(StringComparer.Ordinal);

        private AsyncOperationHandle DownloadHandle;
        private Coroutine DownloadProgressCoroutine = null;

        private bool HasCompletedInitialPatch;
        public bool CompletedInitialPatch => HasCompletedInitialPatch;

        public override void Initialize() { }
        public override void Release()
        {
            StopDownloadProgressCoroutine();
            if (DownloadHandle.IsValid() == true)
            {
                Addressables.Release(DownloadHandle);
            }
            ReleaseAssetAll();
            ResourceLocations.Clear();
            onAddressableStatusEventHandler = null;
        }
        public override void OnSceneChanged() { }

        public static AddressableManager Get()
        {
            return GameManager.Instance?.Get<AddressableManager>();
        }

        /// <summary>
        /// 어드레서블 초기화하고 리소스 목록을 준비
        /// </summary>
        public void AddressableInitializeAsync()
        {
            var handle = Addressables.InitializeAsync(false);
            handle.Completed += OnAddressableInitialize;
        }

        /// <summary>
        /// 지정한 경로의 추가 카탈로그를 로드
        /// </summary>
        /// <param name="path">카탈로그 파일 경로 또는 URL</param>
        public void AddressableLoadCatalog(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError("Addressables catalog path is empty. Call HMAddressableConfig.SetAddressableConfig first.");
                NotifyAddressableStatus(AddressableFunctionType.AddressableLoadCatalog, AddressableStatusKind.Failed);
                return;
            }

            var handle = Addressables.LoadContentCatalogAsync(path, false);
            handle.Completed += OnAddressableLoadCatalog;
        }

        /// <summary>
        /// 카탈로그 업데이트를 확인하고 변경된 카탈로그를 갱신
        /// </summary>
        public void AddressableCheckForCatalogUpdates()
        {   
            var handle = Addressables.CheckForCatalogUpdates(false);
            handle.Completed += OnAddressableCheckForCatalogUpdates;
        }

        /// <summary>
        /// 현재 리소스에 필요한 번들 다운로드 용량을 바이트 단위로 확인
        /// 카탈로그만 업데이트 된 경우 호출하여 번들 받을게 있는지 확인
        /// </summary>
        public void AddressableGetDownloadSizeAsync()
        {
            var handle = Addressables.GetDownloadSizeAsync(ResourceLocations);
            handle.Completed += OnAddressableGetDownloadSizeAsync;
        }

        /// <summary>
        /// 현재 리소스에 필요한 번들을 다운로드하고 진행 상태를 알림
        /// </summary>
        public void AddressableDownloadDependenciesAsync()
        {
            if (DownloadHandle.IsValid())
            {
                Debug.LogWarning("An Addressables dependency download is already in progress.");
                return;
            }

            HasCompletedInitialPatch = false;
            DownloadHandle = Addressables.DownloadDependenciesAsync(ResourceLocations, false);
            StopDownloadProgressCoroutine();
            DownloadProgressCoroutine = StartCoroutine(TrackDownloadProgress());
        }

        /// <summary>
        /// 키에 해당하는 에셋을 비동기로 로드하거나 기존 로드 요청을 재사용
        /// </summary>
        /// <typeparam name="T">로드할 에셋</typeparam>
        /// <param name="key">에셋의 Addressables 키</param>
        /// <param name="sceneChangeRelease">새로 등록하는 에셋을 씬 전환 시 해제 대상에 포함할지 여부</param>
        /// <param name="callback">로드 성공 시 호출할 콜백</param>
        /// <returns>에셋 로드 작업의 핸들</returns>
        /// <exception cref="ArgumentException">키가 비어 있거나 공백인 경우 발생</exception>
        public AsyncOperationHandle AddressableLoadAssetAsync<T>(string key, bool sceneChangeRelease = true, Action<AssetData> callback = null)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Addressable key must not be empty.", nameof(key));
            }

            if (LoadedAssets.TryGetValue(key, out var cached))
            {
                if (cached.AssetHandle.IsValid())
                {
                    if (sceneChangeRelease == false)
                    {
                        cached.RetainForSession();
                    }
                    if (cached.AssetHandle.IsDone && cached.AssetHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        callback?.Invoke(cached);
                    }
                    else
                    {
                        cached.AddCompletedCallback(callback);
                    }

                    return cached.AssetHandle;
                }

                LoadedAssets.Remove(key);
            }

            var handle = Addressables.LoadAssetAsync<T>(key);
            var assetData = new AssetData(key, handle, callback, sceneChangeRelease);
            LoadedAssets.Add(key, assetData);
            handle.Completed += OnAddressableLoadAsset;
            return handle;
        }

        /// <summary>Bounded concurrent loads reuse the same cache and release ownership as single loads.</summary>
        public IEnumerator PreLoadBatch<T>(IEnumerable<string> keys, bool sceneChangeRelease = false, int concurrency = 8)
            where T : UnityEngine.Object
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (concurrency < 1 || concurrency > 32) throw new ArgumentOutOfRangeException(nameof(concurrency));
            var queue = new Queue<string>(keys.Distinct(StringComparer.Ordinal));
            var pending = new List<(string key, AsyncOperationHandle handle)>();
            while (queue.Count > 0 || pending.Count > 0)
            {
                while (queue.Count > 0 && pending.Count < concurrency)
                {
                    var key = queue.Dequeue();
                    pending.Add((key, AddressableLoadAssetAsync<T>(key, sceneChangeRelease)));
                }
                for (var i = pending.Count - 1; i >= 0; i--)
                {
                    var request = pending[i];
                    if (request.handle.IsValid() && !request.handle.IsDone) continue;
                    if (!request.handle.IsValid() || request.handle.Status != AsyncOperationStatus.Succeeded)
                        throw new InvalidOperationException("Required preload failed: " + request.key);
                    pending.RemoveAt(i);
                }
                // Already cached requests need no extra frame; unfinished loads yield to the player loop.
                if (pending.Count > 0) yield return null;
            }
        }

        public T LoadAsset<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key) || LoadedAssets.TryGetValue(key, out var assetData) == false)
            {
                return null;
            }

            return assetData.GetAsset<T>();
        }

        /// <summary>
        /// 지정한 에셋의 핸들을 해제하고 관리 목록에서 제거
        /// </summary>
        /// <param name="key">해제할 에셋의 키입니다.</param>
        public void ReleaseAsset(string key)
        {
            if (string.IsNullOrEmpty(key) || LoadedAssets.TryGetValue(key, out var assetData) == false)
            {
                return;
            }

            if (assetData.AssetHandle.IsValid())
            {
                Addressables.Release(assetData.AssetHandle);
            }

            LoadedAssets.Remove(key);
        }

        /// <summary>
        /// 씬 전환 시 해제 대상으로 지정된 에셋을 해제
        /// </summary>
        public void SceneChangeReleaseAsset()
        {
            var keys = LoadedAssets.Values
                .Where(asset => asset.IsSceneChangeRelease)
                .Select(asset => asset.AssetsKey)
                .ToArray();

            foreach (var key in keys)
            {
                ReleaseAsset(key);
            }
        }

        /// <summary>
        /// 관리 중인 모든 에셋의 핸들을 해제하고 목록을 비움
        /// </summary>
        public void ReleaseAssetAll()
        {
            foreach (var assetData in LoadedAssets.Values)
            {
                if (assetData.AssetHandle.IsValid())
                {
                    Addressables.Release(assetData.AssetHandle);
                }
            }

            LoadedAssets.Clear();
        }

        /// <summary>
        /// 지정한 타입으로 사용할 수 있는 리소스 위치 목록을 반환
        /// </summary>
        public IList<IResourceLocation> GetLocationList<T>() where T : UnityEngine.Object
        {
            return ResourceLocations
                .Where(location => typeof(T).IsAssignableFrom(location.ResourceType))
                .Distinct(new AddressableDistinctItemComparer())
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// 기본 키가 일치하는 첫 리소스 위치를 반환하며, 없으면 null을 반환
        /// </summary>
        public IResourceLocation GetLocation(string key)
        {
            return ResourceLocations.FirstOrDefault(location =>
                string.Equals(location.PrimaryKey, key, StringComparison.Ordinal));
        }

        private void OnAddressableInitialize(AsyncOperationHandle<IResourceLocator> handle)
        {
            var function = AddressableFunctionType.InitializeAsync;

            try
            {
                if (handle.IsValid() == false || handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(handle.IsValid() ? $"{function}: {handle.OperationException}" : $"{function}: Invalid handle");
                    NotifyAddressableStatus(function, AddressableStatusKind.Failed);
                    return;
                }

                RebuildResourceLocations();
                NotifyAddressableStatus(function, AddressableStatusKind.Completed);
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private void OnAddressableCheckForCatalogUpdates(AsyncOperationHandle<List<string>> handle)
        {
            var function = AddressableFunctionType.CheckForCatalogUpdates;

            try
            {
                if (handle.IsValid() == false || handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(handle.IsValid() ? $"{function}: {handle.OperationException}" : $"{function}: Invalid handle");
                    NotifyAddressableStatus(function, AddressableStatusKind.Failed);
                    return;
                }

                if (handle.Result == null || handle.Result.Count == 0)
                {
                    NotifyAddressableStatus(function, AddressableStatusKind.Completed);
                    return;
                }

                var updateHandle = Addressables.UpdateCatalogs(true, handle.Result, false);
                updateHandle.Completed += OnAddressableUpdateCatalogs;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private void OnAddressableUpdateCatalogs(AsyncOperationHandle<List<IResourceLocator>> handle)
        {
            var function = AddressableFunctionType.UpdateCatalogs;

            try
            {
                if (handle.IsValid() == false || handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(handle.IsValid() ? $"{function}: {handle.OperationException}" : $"{function}: Invalid handle");
                    NotifyAddressableStatus(function, AddressableStatusKind.Failed);
                    return;
                }

                RebuildResourceLocations();
                NotifyAddressableStatus(function, AddressableStatusKind.Completed);
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

       
        private void OnAddressableLoadCatalog(AsyncOperationHandle<IResourceLocator> handle)
        {
            var function = AddressableFunctionType.AddressableLoadCatalog;

            try
            {
                if (handle.IsValid() == false || handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(handle.IsValid() ? $"{function}: {handle.OperationException}" : $"{function}: Invalid handle");
                    NotifyAddressableStatus(function, AddressableStatusKind.Failed);
                    return;
                }

                RebuildResourceLocations();
                NotifyAddressableStatus(function, AddressableStatusKind.Completed);
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private void OnAddressableGetDownloadSizeAsync(AsyncOperationHandle<long> handle)
        {
            var function = AddressableFunctionType.GetDownloadSizeAsync;

            try
            {
                if (handle.IsValid() == false || handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(handle.IsValid() ? $"{function}: {handle.OperationException}" : $"{function}: Invalid handle");
                    NotifyAddressableStatus(function, AddressableStatusKind.Failed);
                    return;
                }

                if (handle.Result >= 0)
                {
                    NotifyAddressableStatus(function, AddressableStatusKind.DownloadSizeReady, handle.Result);
                }
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        /// <summary>
        /// 로드된 카탈로그에서 에셋 위치를 모아 중복 없는 리소스 목록을 구성
        /// </summary>
        private void RebuildResourceLocations()
        {
            ResourceLocations.Clear();
            var uniqueLocations = new HashSet<IResourceLocation>(new AddressableDistinctItemComparer());

            foreach (var locator in Addressables.ResourceLocators)
            {
                foreach (var key in locator.Keys)
                {
                    if (locator.Locate(key, typeof(UnityEngine.Object), out var locations) == false || locations == null)
                    {
                        continue;
                    }

                    foreach (var location in locations)
                    {
                        if (uniqueLocations.Add(location))
                        {
                            ResourceLocations.Add(location);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 에셋 로드 성공 시 콜백을 호출하고 실패 시 관리 목록과 핸들을 정리
        /// </summary>
        private void OnAddressableLoadAsset<T>(AsyncOperationHandle<T> handle) where T : UnityEngine.Object
        {
            var pair = LoadedAssets.FirstOrDefault(item => item.Value.AssetHandle.Equals(handle));
            var assetData = pair.Value;

            if (assetData == null)
            {
                return;
            }

            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded && assetData.IsValid())
            {
                assetData.InvokeCompleted();
                return;
            }

            LoadedAssets.Remove(pair.Key);
            Debug.LogError($"Unable to load Addressable asset '{pair.Key}'. {handle.OperationException}");
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        private IEnumerator TrackDownloadProgress()
        {
            var function = AddressableFunctionType.DownloadDependenciesAsync;

            while (DownloadHandle.IsValid() && DownloadHandle.IsDone == false)
            {
                var status = DownloadHandle.GetDownloadStatus();
                NotifyAddressableStatus(function, AddressableStatusKind.DownloadProgress, 0, status.Percent);
                yield return null;
            }

            var isValid = DownloadHandle.IsValid();
            var isSuccess = isValid && DownloadHandle.Status == AsyncOperationStatus.Succeeded;
            var error = isValid ? $"{function}: {DownloadHandle.OperationException}" : $"{function}: Invalid handle";

            if (isValid)
            {
                Addressables.Release(DownloadHandle);
            }

            DownloadHandle = default;
            DownloadProgressCoroutine = null;

            if (isSuccess)
            {
                HasCompletedInitialPatch = true;
                NotifyAddressableStatus(function, AddressableStatusKind.DownloadProgress, 0, 1f);
                NotifyAddressableStatus(function, AddressableStatusKind.Completed, 0, 1f);
            }
            else
            {
                Debug.LogError(error);
                NotifyAddressableStatus(function, AddressableStatusKind.Failed);
            }
        }

        private void StopDownloadProgressCoroutine()
        {
            if (DownloadProgressCoroutine == null)
            {
                return;
            }

            StopCoroutine(DownloadProgressCoroutine);
            DownloadProgressCoroutine = null;
        }

        private void NotifyAddressableStatus(AddressableFunctionType function, AddressableStatusKind kind, long downloadSizeBytes = 0, float progress = 0)
        {
            onAddressableStatusEventHandler?.Invoke(new AddressableStatus(function, kind, downloadSizeBytes, progress));
        }
    }
}
