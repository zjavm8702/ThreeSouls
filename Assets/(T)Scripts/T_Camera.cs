using System.Collections;
using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Utility;
using Unity.Cinemachine;
using UnityEngine;

public class T_Camera : MonoBehaviour
{
    [SerializeField] private CinemachineCamera FollowCamera = null;
    [SerializeField] private Collider2D CameraBounds = null;
    [SerializeField] private GameObject[] FollowTargets = null;
    [SerializeField] private CameraPresentationData CameraPresentationSettings = null;

    private CinemachineConfiner2D CameraConfiner = null;
    private CinemachinePositionComposer CameraPositionComposer = null;
    private Coroutine CameraSequenceCoroutine = null;

    void Start()
    {
        SetUpCamera();

        if (CameraSequenceCoroutine != null)
        {
            StopCoroutine(CameraSequenceCoroutine);
        }

        CameraSequenceCoroutine = StartCoroutine(PlayCameraPresentation());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private bool SetUpCamera()
    {
        if (CameraPresentationSettings == null)
        {
            ThreeSoulsLog.Error("CameraPresentationData null");
            return false;
        }

        if (FollowCamera == null || CameraBounds == null)
        {
            ThreeSoulsLog.Error("Camera presentation setup failed");
            return false;
        }

        FollowCamera.Lens.OrthographicSize = CameraPresentationSettings.IntroCameraSize;

        CameraPositionComposer = FollowCamera.GetComponent<CinemachinePositionComposer>();

        if (CameraPositionComposer != null)
        {
            var composition = CameraPositionComposer.Composition;
            composition.DeadZone.Enabled = false;
            composition.DeadZone.Size = CameraPresentationSettings.DeadZoneSize;
            CameraPositionComposer.Composition = composition;
        }

        CameraConfiner = FollowCamera.GetComponent<CinemachineConfiner2D>();

        if (CameraConfiner == null)
        {
            ThreeSoulsLog.Error("Not Found Component CinemachineConfiner2D");
            return false;
        }

        CameraConfiner.BoundingShape2D = CameraBounds;
        CameraConfiner.Damping = CameraPresentationSettings.ConfinerDamping;
        CameraConfiner.SlowingDistance = CameraPresentationSettings.ConfinerSlowingDistance;
        CameraConfiner.OversizeWindow = new CinemachineConfiner2D.OversizeWindowSettings
        {
            Enabled = false,
            MaxWindowSize = CameraPresentationSettings.IntroCameraSize
        };
        CameraConfiner.InvalidateBoundingShapeCache();

        return true;
    }

    private IEnumerator PlayCameraPresentation()
    {
        yield return new WaitForSecondsRealtime(CameraPresentationSettings.IntroDelay);

        for (var i = 0; i < FollowTargets.Length; i++)
        {
            var followObject = FollowTargets[i];

            if (followObject == null || followObject.gameObject.activeInHierarchy == false)
            {
                continue;
            }

            SetCameraTarget(followObject.transform);
            yield return new WaitForSecondsRealtime(CameraPresentationSettings.MonsterFocusTime);
        }

        var elapsedTime = 0f;
        var startSize = FollowCamera.Lens.OrthographicSize;

        while (elapsedTime < CameraPresentationSettings.HeroFocusTime)
        {
            elapsedTime += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsedTime / CameraPresentationSettings.HeroFocusTime);
            FollowCamera.Lens.OrthographicSize = Mathf.Lerp(startSize, CameraPresentationSettings.CombatCameraSize, Mathf.SmoothStep(0f, 1f, progress));

            yield return null;
        }

        FollowCamera.Lens.OrthographicSize = CameraPresentationSettings.CombatCameraSize;
        //CameraConfiner?.InvalidateLensCache();
        //SetCameraDeadZone(true);
        CameraSequenceCoroutine = null;
    }

    private void SetCameraTarget(Transform trackingTarget)
    {
        FollowCamera.Target = new CameraTarget
        {
            TrackingTarget = trackingTarget
        };
    }

    private void SetCameraDeadZone(bool enabled)
    {
        if (CameraPositionComposer == null)
        {
            return;
        }

        var composition = CameraPositionComposer.Composition;
        composition.DeadZone.Enabled = enabled;
        CameraPositionComposer.Composition = composition;
    }
}
