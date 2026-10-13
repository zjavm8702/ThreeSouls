using System;
using System.Collections;
using ThreeSouls.Character;
using ThreeSouls.Data;
using Unity.Cinemachine;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 카메라 인트로와 리더 추적 및 연출 코루틴의 수명을 관리합니다.
    /// </summary>
    internal sealed class CombatCameraHandler
    {
        public delegate void OnPresentationCompletDelegate();

        private OnPresentationCompletDelegate onPresentationCompletEventHandler = null;
        public event OnPresentationCompletDelegate OnPresentationCompletEventHandler
        {
            add
            {
                onPresentationCompletEventHandler -= value;
                onPresentationCompletEventHandler += value;
            }
            remove
            {
                onPresentationCompletEventHandler -= value;
            }
        }

        private readonly MonoBehaviour CoroutineOwner;
        private CinemachineCamera FollowCamera;
        private CinemachineConfiner2D CameraConfiner;
        private Collider2D CameraBounds;
        private CameraPresentationData CameraPresentationSettings;
        private Transform[] HolyPlaceTargets;
        private BaseEntity Leader;
        private Coroutine CameraSequenceCoroutine;

        internal CombatCameraHandler(MonoBehaviour owner)
        {
            CoroutineOwner = owner;
        }

        internal void SetLeader(BaseEntity leader)
        {
            Leader = leader;

            if (FollowCamera != null && Leader != null)
            {
                SetCameraTarget(Leader.transform);
            }
        }

        internal void BeginPresentation()
        {
            StopPresentation();
            CameraSequenceCoroutine = CoroutineOwner.StartCoroutine(PlayCameraPresentation());
        }

        internal void StopPresentation()
        {
            if (CameraSequenceCoroutine != null)
            {
                CoroutineOwner.StopCoroutine(CameraSequenceCoroutine);
                CameraSequenceCoroutine = null;
            }
        }

        internal void Release()
        {
            StopPresentation();
            Leader = null;
            FollowCamera = null;
            CameraConfiner = null;
            CameraBounds = null;
            HolyPlaceTargets = null;
            CameraPresentationSettings = null;
        }

        internal void Initialize(CinemachineCamera camera, Collider2D bounds, Transform[] holyPlaces, CameraPresentationData settings)
        {
            FollowCamera = camera;
            CameraBounds = bounds;
            HolyPlaceTargets = holyPlaces;
            CameraPresentationSettings = settings;

            if (CameraPresentationSettings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (FollowCamera == null || CameraBounds == null)
            {
                throw new InvalidOperationException("전투 카메라 또는 맵의 카메라 경계가 없습니다.");
            }

            FollowCamera.Lens.OrthographicSize = CameraPresentationSettings.IntroCameraSize;
            CameraConfiner = FollowCamera.GetComponent<CinemachineConfiner2D>();

            if (CameraConfiner == null)
            {
                throw new InvalidOperationException("전투 카메라에 CinemachineConfiner2D가 없습니다.");
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
        }

        private IEnumerator PlayCameraPresentation()
        {
            yield return new WaitForSecondsRealtime(CameraPresentationSettings.IntroDelay);

            for (var i = 0; i < HolyPlaceTargets.Length; i++)
            {
                SetCameraTarget(HolyPlaceTargets[i]);
                yield return new WaitForSecondsRealtime(CameraPresentationSettings.MonsterFocusTime);
            }

            if (Leader != null)
            {
                SetCameraTarget(Leader.transform);
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
            CameraConfiner.InvalidateLensCache();
            CameraSequenceCoroutine = null;
            onPresentationCompletEventHandler?.Invoke();
        }

        private void SetCameraTarget(Transform trackingTarget)
        {
            FollowCamera.Target = new CameraTarget
            {
                TrackingTarget = trackingTarget
            };
        }
    }
}
