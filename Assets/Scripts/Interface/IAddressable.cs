using System;
using ThreeSouls.Data;

namespace ThreeSouls.Interface
{
    public enum AddressableFunctionType
    {
        InitializeAsync,
        CheckForCatalogUpdates,
        AddressableLoadCatalog,
        UpdateCatalogs,
        DownloadDependenciesAsync,
        GetDownloadSizeAsync,
    }

    public enum AddressableStatusKind
    {
        DownloadSizeReady,
        DownloadProgress,
        Completed,
        Failed
    }
}