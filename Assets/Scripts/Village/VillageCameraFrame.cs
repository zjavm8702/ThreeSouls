using System;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 화면 비율이 달라져도 마을 전체가 보이도록 직교 카메라 크기를 맞춥니다.
    /// </summary>
    public sealed class VillageCameraFrame : MonoBehaviour
    {
        [SerializeField] private Camera villageCamera;
        [SerializeField] private Vector2 frameSize = new Vector2(68f, 46f);
        private float previousAspect;

        /// <summary>
        /// 제작 도구에서 카메라와 마을 표시 크기를 연결합니다.
        /// </summary>
        public void Configure(Camera target, Vector2 size)
        {
            villageCamera = target;
            frameSize = size;
        }

        private void Awake()
        {
            if (villageCamera == null || villageCamera.orthographic == false)
            {
                throw new InvalidOperationException("마을에는 직교 카메라가 필요합니다.");
            }

            ApplyFrame();
        }

        private void LateUpdate()
        {
            if (Mathf.Approximately(previousAspect, villageCamera.aspect) == false)
            {
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            previousAspect = villageCamera.aspect;
            villageCamera.orthographicSize = Mathf.Max(frameSize.y * 0.5f,
                frameSize.x / (previousAspect * 2f));
        }
    }
}
