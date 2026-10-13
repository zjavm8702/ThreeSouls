using System;
using ThreeSouls.Interface;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ThreeSouls.Data
{
    public delegate void OnAddressableStatusDelegate(AddressableStatus addressableStatus);

    public readonly struct AddressableStatus
    {
        public AddressableFunctionType FunctionType { get; }
        public AddressableStatusKind StatusKind { get; }
        public long DownloadSizeBytes { get; }
        public float Progress { get; }

        public AddressableStatus(AddressableFunctionType functionType, AddressableStatusKind statusKind, long downloadSizeBytes = 0, float progress = 0)
        {
            FunctionType = functionType;
            StatusKind = statusKind;
            DownloadSizeBytes = downloadSizeBytes;
            Progress = progress;
        }
    }

    public sealed class AssetData
    {
        private Action<AssetData> Completed;
        public string AssetsKey { get; }
        public AsyncOperationHandle AssetHandle { get; }
        public bool IsSceneChangeRelease { get; private set; }

        internal void RetainForSession()
        {
            IsSceneChangeRelease = false;
        }

        public AssetData(string asstsKey, AsyncOperationHandle asyncOp, Action<AssetData> callback, bool sceneChangeRelease)
        {
            AssetsKey = asstsKey;
            AssetHandle = asyncOp;
            IsSceneChangeRelease = sceneChangeRelease;

            AddCompletedCallback(callback);
        }

        public void AddCompletedCallback(Action<AssetData> callback)
        {
            Completed = callback;
        }

        public void InvokeCompleted()
        {
            var callback = Completed;
            Completed = null;
            callback?.Invoke(this);
        }

        public T GetAsset<T>() where T : UnityEngine.Object
        {
            return AssetHandle.Result as T;
        }

        public object GetAsset()
        {
            return AssetHandle.Result;
        }

        public bool IsValid()
        {
            return AssetHandle.IsValid() && AssetHandle.Result != null && string.IsNullOrEmpty(AssetsKey) == false;
        }
    }
}
