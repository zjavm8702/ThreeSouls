using System;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 풀로 재사용하는 스킬 연출의 클립 교체, 명중 시점과 종료를 처리합니다.
    /// 피해와 타깃 선택은 전투 스킬 담당자에게 전달합니다.
    /// </summary>
    public sealed class CombatSkillEffect : MonoBehaviour
    {
        [SerializeField] private Animator EffectAnimator = null;

        private ParticleSystem[] Particles;
        private AnimatorOverrideController OverrideController;
        private Action HitCallback;
        private Action CompleteCallback;
        private float ElapsedTime;
        private float HitTime;
        private float EndTime;
        private bool HitDelivered;
        private Vector3 BaseScale;
        public bool IsPlaying { get; private set; }

        /// <summary>
        /// 연출에 필요한 컴포넌트를 인스턴스 생성 때 한 번 캐싱합니다.
        /// </summary>
        private void Awake()
        {
            Particles = GetComponentsInChildren<ParticleSystem>(true);
            BaseScale = transform.localScale;
        }

        /// <summary>
        /// 공통 Controller의 Attack 클립만 인스턴스별로 바꾸고 연출을 처음부터 재생합니다.
        /// </summary>
        public void Play(AnimationClip attackClip, float hitDelay, Quaternion rotation, Action hit, Action complete,
            float radiusMultiplier = 1)
        {
            transform.localScale = BaseScale * radiusMultiplier;
            if (OverrideController == null)
            {
                var controller = EffectAnimator.runtimeAnimatorController;
                while (controller is AnimatorOverrideController nested)
                {
                    controller = nested.runtimeAnimatorController;
                }

                if (controller == null || controller.animationClips.Length != 1)
                {
                    throw new InvalidOperationException("스킬 연출에는 단일 Attack 클립의 공통 Controller가 필요합니다.");
                }

                OverrideController = new AnimatorOverrideController(controller);
            }

            var original = OverrideController.runtimeAnimatorController.animationClips[0];
            OverrideController[original] = attackClip;
            EffectAnimator.runtimeAnimatorController = OverrideController;
            EffectAnimator.Rebind();
            EffectAnimator.Update(0);
            EffectAnimator.transform.localRotation = Quaternion.identity;
            transform.rotation = rotation;
            ElapsedTime = 0;
            HitTime = hitDelay;
            EndTime = Mathf.Max(attackClip.length, HitTime);
            HitDelivered = false;
            HitCallback = hit;
            CompleteCallback = complete;
            IsPlaying = true;

            for (int i = 0; i < Particles.Length; i++)
            {
                var main = Particles[i].main;
                EndTime = Mathf.Max(EndTime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
                Particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Particles[i].Play(true);
            }

            EndTime += 0.1f;
        }

        /// <summary>
        /// 게임 시간에 따라 피해 시점을 한 번 전달하고 입자가 끝나면 풀 반환을 요청합니다.
        /// </summary>
        private void Update()
        {
            if (IsPlaying == false)
            {
                return;
            }

            ElapsedTime += Time.deltaTime;
            if (HitDelivered == false && ElapsedTime >= HitTime)
            {
                HitDelivered = true;
                HitCallback?.Invoke();
            }

            if (IsPlaying == true && ElapsedTime >= EndTime)
            {
                var complete = CompleteCallback;
                Cancel();
                complete?.Invoke();
            }
        }

        /// <summary>
        /// 재사용과 취소 시 이전 콜백 및 잔여 입자를 제거합니다.
        /// </summary>
        public void Cancel()
        {
            transform.localScale = BaseScale;
            IsPlaying = false;
            HitCallback = null;
            CompleteCallback = null;
            for (int i = 0; i < Particles.Length; i++)
            {
                Particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>
        /// 인스턴스가 제거될 때 직접 만든 OverrideController도 해제합니다.
        /// </summary>
        private void OnDestroy()
        {
            if (OverrideController != null)
            {
                Destroy(OverrideController);
            }
        }
    }
}
