using System;
using System.Collections;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

public class Scene_Start : MonoBehaviour
{
    [SerializeField] private RectTransform UICanvas = null;
   
    private UI_Popup_Patch PatchPopup = null;

    private void Start()
    {
        UIManager.Get().SetUIRoot(UICanvas);

        ThreeSoulsLog.Info("START_SCENE");
#if UNITY_EDITOR
        //var cacheCleared = Caching.ClearCache();
        //ThreeSoulsLog.Warning($"캐시 삭제 성공: {cacheCleared}", false, this);
#endif

        GameManager.Instance.Get<AddressableManager>().OnAddressableStatusEventHandler += OnAddressableStatus;
        GameManager.Instance.Get<AddressableManager>().AddressableInitializeAsync();
    }

    private void OnAddressableStatus(AddressableStatus addressableStatus)
    {
        switch (addressableStatus.FunctionType)
        {
            case AddressableFunctionType.InitializeAsync:
                if (addressableStatus.StatusKind == AddressableStatusKind.Completed)
                {
                    GameManager.Instance.Get<AddressableManager>().AddressableCheckForCatalogUpdates();
                    return;
                }
                break;
            case AddressableFunctionType.CheckForCatalogUpdates:
                if (addressableStatus.StatusKind == AddressableStatusKind.Completed)
                {
                    GameManager.Instance.Get<AddressableManager>().AddressableGetDownloadSizeAsync();
                    return;
                }
                break;
            case AddressableFunctionType.UpdateCatalogs:
                if (addressableStatus.StatusKind == AddressableStatusKind.Completed)
                {
                    GameManager.Instance.Get<AddressableManager>().AddressableGetDownloadSizeAsync();
                    return;
                }
                break;
            case AddressableFunctionType.GetDownloadSizeAsync:
                if (addressableStatus.StatusKind == AddressableStatusKind.DownloadSizeReady)
                {
                    UpdateBundleSize(addressableStatus.DownloadSizeBytes);
                    return;
                }
                break;
            case AddressableFunctionType.DownloadDependenciesAsync:
                if (addressableStatus.StatusKind == AddressableStatusKind.DownloadProgress)
                {
                    PatchPopup.OnBundleDownloadProgress(addressableStatus.Progress);
                    return;
                }
                else if (addressableStatus.StatusKind == AddressableStatusKind.Completed)
                {
                    StartCoroutine(AssetPreLoad());
                    return;
                }
                break;
        }

        ThreeSoulsLog.Error($"Addressable Error {addressableStatus.FunctionType}", false, this);
    }

    /// <summary>
    /// 패치 내역이 있는지 확인
    /// </summary>
    private void UpdateBundleSize(long bundleSize)
    {
        ThreeSoulsLog.Warning($"다운로드 필요 용량: {bundleSize} bytes", false, this);

        if (bundleSize > 0)
        {
            CreatePatchPopup(bundleSize);
        }
        else
        {
            //Todo :: 로그인
            StartCoroutine(AssetPreLoad());
            //LoadLobbyScene();
        }
    }

    private void CreatePatchPopup(long bundleSize)
    {
        var patchObject = Resources.Load(StringConstants.UI_POPUP_PATCH_ROOT);

        if (patchObject == null)
        {
            throw new ArgumentException();
        }

        var obj = Instantiate(patchObject) as GameObject;

        if (obj == null)
        {
            throw new ArgumentException();
        }

        PatchPopup = obj.GetComponent<UI_Popup_Patch>();
        PatchPopup.transform.SetSafeParent(UICanvas.transform);
        PatchPopup.ShowPopup(bundleSize);
        PatchPopup.ButtonAction = () =>
        {
            GameManager.Instance.Get<AddressableManager>().AddressableDownloadDependenciesAsync();
        };
    }

    private void DestroyPatchPopup()
    {
        if (PatchPopup != null)
        {
            Destroy(PatchPopup);
        }
    }

    private IEnumerator AssetPreLoad()
    {
        yield return TableManager.Get().AssetsPreLoad();

        DestroyPatchPopup();

        GameManager.Instance.Get<AddressableManager>().OnAddressableStatusEventHandler -= OnAddressableStatus;

        LoadLobbyScene();
    }

    private void LoadLobbyScene()
    {
        GameManager.Instance.Get<AddressableManager>().OnAddressableStatusEventHandler -= OnAddressableStatus;

        SceneLoadManager.Get().LoadScene(SceneName.Scene_Dungeon);
    }
}
