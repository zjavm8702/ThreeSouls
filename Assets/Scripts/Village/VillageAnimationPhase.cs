using System;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 환경 애니메이션의 시작 시각을 분산해 일제히 움직이는 모습을 방지합니다.
    /// </summary>
    public sealed class VillageAnimationPhase : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Range(0f, 1f)] private float phase;
        [SerializeField] private float speed = 1f;

        /// <summary>
        /// 제작 도구에서 재생할 Animator와 시차를 연결합니다.
        /// </summary>
        public void Configure(Animator target, float startPhase, float playbackSpeed)
        {
            animator = target;
            phase = startPhase;
            speed = playbackSpeed;
        }

        private void OnEnable()
        {
            if (animator == null)
            {
                throw new InvalidOperationException("마을 환경 Animator를 연결해야 합니다.");
            }

            animator.speed = speed;
            animator.Play("Ambient", 0, phase);
        }
    }
}
