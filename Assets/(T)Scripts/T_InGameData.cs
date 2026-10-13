using System;
using ThreeSouls.Data;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Global;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 원본 에셋을 자동으로 찾아 패치와 번들 로드 없이 테스트 스테이지를 실행합니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class T_InGameData : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField, Min(1)] private int RegionNumber = 1;
    [SerializeField, Min(1)] private int StageNumber = 1;
    [SerializeField] private bool PlayCameraIntro = false;
    [SerializeField] private CinemachineCamera FollowCamera = null;
    [Tooltip("실행 중 숨길 기존 맵, 체력바 샘플, 카메라 테스트 오브젝트")]
    [SerializeField] private GameObject[] PreviewObjects = Array.Empty<GameObject>();

    private GameObject ManagerObject;
    private GameManager Managers;
    private AddressableManager AssetManager;
    private GameCombatManager CombatManager;
    private UIManager CombatUIManager;
    private CameraPresentationData CameraSettings;
    private UI_HUD_Top TopHUDPrefab;
    private UI_HUD_Bottom BottomHUDPrefab;
    private bool IsInitialized;

    private void Awake()
    {
        if (GameManager.TryGetInstance(out _) == true || FindAnyObjectByType<GameManager>() != null)
        {
            throw new InvalidOperationException("테스트 씬은 기존 GameManager 없이 직접 실행해야 합니다.");
        }

        if (FollowCamera == null)
        {
            var cameraObject = GameObject.FindGameObjectWithTag(StringConstants.TAG_FOLLOW_CAMERA);
            FollowCamera = cameraObject != null ? cameraObject.GetComponent<CinemachineCamera>() : null;
        }

        if (FollowCamera == null || FollowCamera.GetComponent<CinemachineConfiner2D>() == null ||
            FollowCamera.CompareTag(StringConstants.TAG_FOLLOW_CAMERA) == false)
        {
            throw new InvalidOperationException("테스트 씬에 FollowCamera 태그의 CinemachineCamera와 Confiner가 필요합니다.");
        }

        for (var i = 0; i < PreviewObjects.Length; i++)
        {
            if (PreviewObjects[i] != null)
            {
                PreviewObjects[i].SetActive(false);
            }
        }

        var characterParent = new GameObject("CharacterParent");
        characterParent.tag = StringConstants.TAG_CHARACTER_PARENT;
        characterParent.transform.SetParent(transform, false);

        Managers = GameManager.Instance;
        ManagerObject = Managers.transform.parent.gameObject;
        AssetManager = Managers.Get<AddressableManager>();
        var loader = new T_InGameAssetLoader(AssetManager);
        loader.Prepare(RegionNumber, StageNumber);
        CameraSettings = loader.Load<CameraPresentationData>(ScriptableObjectDatas.CameraPresentationData.ToAddress());
        TopHUDPrefab = LoadHUD<UI_HUD_Top>(loader, Prefabs.UI_HUD_Top.ToAddress());
        BottomHUDPrefab = LoadHUD<UI_HUD_Bottom>(loader, Prefabs.UI_HUD_Botttom.ToAddress());
        Managers.Get<TableManager>();
        Managers.Get<TimeManager>();
        CombatUIManager = Managers.Get<UIManager>();
        Managers.Get<UserManager>();
        CombatManager = Managers.Get<GameCombatManager>();
        IsInitialized = true;
    }

    private static T LoadHUD<T>(T_InGameAssetLoader loader, string address) where T : Component
    {
        var prefab = loader.Load<GameObject>(address);
        return prefab.GetComponent<T>()
            ?? throw new InvalidOperationException($"테스트 HUD 컴포넌트 누락 : {address}, {typeof(T).Name}");
    }

    /// <summary>
    /// 준비된 테이블로 캐릭터와 HUD를 생성하고 전투를 시작합니다.
    /// </summary>
    private void Start()
    {
        if (IsInitialized == false)
        {
            return;
        }

        var preload = TableManager.Get().AssetsPreLoad();

        while (preload.MoveNext())
        {
        }

        var topHUD = CombatUIManager.CreateHUD(TopHUDPrefab);
        CombatManager.ConfigureRewardTarget(topHUD.GetGoldRewardTarget());
        CombatManager.ConfigureStage(RegionNumber, StageNumber);
        CombatManager.SetUp();
        CombatUIManager.CreateHUD(BottomHUDPrefab);

        if (PlayCameraIntro == false)
        {
            CombatManager.StopAllCoroutines();
            FollowCamera.Lens.OrthographicSize = CameraSettings.CombatCameraSize;
            FollowCamera.GetComponent<CinemachineConfiner2D>().InvalidateLensCache();
            CombatManager.StartCombat();
        }
    }

    private void OnApplicationQuit()
    {
        Release();
    }

    private void OnDestroy()
    {
        Release();
    }

    /// <summary>
    /// 매니저와 풀을 먼저 정리하고 테스트 핸들과 그룹을 해제합니다.
    /// </summary>
    private void Release()
    {
        if (ManagerObject == null)
        {
            return;
        }

        Managers.Release();
        AssetManager.ReleaseAssetAll();
        Destroy(ManagerObject);
        ManagerObject = null;
        IsInitialized = false;
    }
}
