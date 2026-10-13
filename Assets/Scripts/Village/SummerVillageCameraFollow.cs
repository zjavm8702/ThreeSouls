using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 넓어진 Summer 테스트 마을에서 부대를 따라가며 픽셀 격자와 배경 경계를 유지합니다.
    /// </summary>
    public sealed class SummerVillageCameraFollow : MonoBehaviour
    {
        [SerializeField] private Camera WorldCamera;
        [SerializeField] private Transform FollowTarget;
        [SerializeField] private Rect WorldBounds;
        [SerializeField] private Vector2 ViewOffset = new Vector2(0, 2.5f);
        [SerializeField] private float FollowSharpness = 8;
        private PixelPerfectCamera Pixels;
        private Vector2 SmoothPosition;

        public void Configure(Camera camera, Transform target, Rect bounds)
        {
            WorldCamera = camera;
            FollowTarget = target;
            WorldBounds = bounds;
            ViewOffset = new Vector2(0, 2.5f);
        }

        private void Awake()
        {
            if (WorldCamera == null || FollowTarget == null || WorldCamera.orthographic == false ||
                WorldBounds.width <= 0 || WorldBounds.height <= 0 || FollowSharpness <= 0)
            {
                throw new InvalidOperationException("Summer 마을 카메라의 부대/배경 경계를 연결해야 합니다.");
            }
            Pixels = WorldCamera.GetComponent<PixelPerfectCamera>();
            if (Pixels == null)
            {
                throw new InvalidOperationException("Summer 마을에는 URP 픽셀 카메라가 필요합니다.");
            }
            SmoothPosition = GetClampedPosition((Vector2)FollowTarget.position + ViewOffset);
        }

        private void Start()
        {
            ApplyPosition();
        }

        private void LateUpdate()
        {
            var target = GetClampedPosition((Vector2)FollowTarget.position + ViewOffset);
            var weight = 1 - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            SmoothPosition = Vector2.Lerp(SmoothPosition, target, weight);
            ApplyPosition();
        }

        private Vector2 GetClampedPosition(Vector2 position)
        {
            var halfHeight = WorldCamera.orthographicSize;
            var halfWidth = halfHeight * WorldCamera.aspect;
            var x = WorldBounds.width <= halfWidth * 2 ? WorldBounds.center.x
                : Mathf.Clamp(position.x, WorldBounds.xMin + halfWidth, WorldBounds.xMax - halfWidth);
            var y = WorldBounds.height <= halfHeight * 2 ? WorldBounds.center.y
                : Mathf.Clamp(position.y, WorldBounds.yMin + halfHeight, WorldBounds.yMax - halfHeight);
            return new Vector2(x, y);
        }

        private void ApplyPosition()
        {
            var position = new Vector3(SmoothPosition.x, SmoothPosition.y, transform.position.z);
            transform.position = Pixels.RoundToPixel(position);
        }
    }
}
