using System;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 마을 동물이 지정된 구간을 쉬어 가며 왕복하도록 표현합니다.
    /// </summary>
    public sealed class VillageAmbientMotion : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Vector3 offset;
        [SerializeField] private float travelDuration = 8f;
        [SerializeField] private float restDuration = 2f;
        [SerializeField] private float phase;
        private Vector3 origin;

        /// <summary>
        /// 제작 도구에서 이동 구간과 시작 시차를 연결합니다.
        /// </summary>
        public void Configure(SpriteRenderer renderer, Vector3 destinationOffset,
            float duration, float startPhase, float pauseDuration = 2f)
        {
            spriteRenderer = renderer;
            offset = destinationOffset;
            travelDuration = duration;
            phase = startPhase;
            restDuration = pauseDuration;
        }

        private void Awake()
        {
            if (spriteRenderer == null || travelDuration <= 0f)
            {
                throw new InvalidOperationException("마을 이동 표현의 설정이 필요합니다.");
            }

            origin = transform.localPosition;
        }

        private void Update()
        {
            float halfCycle = travelDuration + restDuration;
            float elapsed = Mathf.Repeat(Time.time + phase, halfCycle * 2f);
            bool returning = elapsed >= halfCycle;
            float progress = Mathf.Clamp01(Mathf.Repeat(elapsed, halfCycle) / travelDuration);

            if (returning == true)
            {
                progress = 1f - progress;
            }

            transform.localPosition = origin + offset * progress;
            spriteRenderer.flipX = returning;
            spriteRenderer.sortingOrder = 300 - Mathf.RoundToInt(transform.position.y * 10f);
        }
    }
}
