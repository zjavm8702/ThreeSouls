using System;
using ThreeSouls.Enumeration;
using UnityEngine;

namespace ThreeSouls.Character
{
    /// <summary>
    /// 캐릭터의 애니메이션, 방향, 스프라이트와 선택적 이동 먼지 표현을 담당합니다.
    /// </summary>
    internal sealed class EntityAnimation
    {
        private enum UpDownDirection
        {
            None = 0,
            Down = 1,
            Up = 2
        }

        private readonly Animator Animator;
        private readonly SpriteRenderer Renderer;
        private readonly ParticleSystemRenderer MovementDust;
        private readonly bool HasRunningParameter;
        private readonly EntityHitFeedback HitFeedback;
        private UpDownDirection VerticalDirection = UpDownDirection.None;
        private static readonly int DirectionHash = Animator.StringToHash("Direction");
        private static readonly int[] ParameterHashes =
        {
            Animator.StringToHash("Idle"),
            Animator.StringToHash("Walking"),
            Animator.StringToHash("Running"),
            Animator.StringToHash("Attack"),
            Animator.StringToHash("Death"),
            Animator.StringToHash("Hit")
        };

        internal EntityAnimation(Animator animator, SpriteRenderer renderer, ParticleSystemRenderer dust, Material hitMaterial)
        {
            Animator = animator;
            Renderer = renderer;
            MovementDust = dust;
            HitFeedback = new EntityHitFeedback(renderer, hitMaterial);
            var parameters = animator.parameters;

            // 캐릭터 Controller의 공통 bool 파라미터는 초기화 시 한 번 확인합니다.
            for (var i = 0; i < ParameterHashes.Length; i++)
            {
                if ((AnimatorStateParams)i == AnimatorStateParams.Idle)
                {
                    continue;
                }

                var found = false;

                for (var j = 0; j < parameters.Length; j++)
                {
                    if (parameters[j].nameHash == ParameterHashes[i] && parameters[j].type == AnimatorControllerParameterType.Bool)
                    {
                        found = true;
                        break;
                    }
                }

                if ((AnimatorStateParams)i == AnimatorStateParams.Running)
                {
                    HasRunningParameter = found;
                    continue;
                }

                if (found == false)
                {
                    throw new InvalidOperationException($"필수 캐릭터 Animator bool 파라미터 누락 : {(AnimatorStateParams)i}");
                }
            }
        }

        internal void ValidateRunningSupport(bool canRun)
        {
            if (canRun == true && HasRunningParameter == false)
            {
                throw new InvalidOperationException("달리기를 사용하는 캐릭터의 Animator에는 Running bool이 필요합니다.");
            }
        }

        internal void Reset()
        {
            ClearHitFeedback();

            VerticalDirection = UpDownDirection.None;
            Renderer.flipX = false;
            Animator.SetFloat(DirectionHash, (int)VerticalDirection);

            SetAnimator(AnimatorStateParams.Dead, false);
            SetAnimator(AnimatorStateParams.Attack, false);
            SetAnimator(AnimatorStateParams.Walking, false);
            SetAnimator(AnimatorStateParams.Running, false);
            SetAnimator(AnimatorStateParams.Hit, false);
            SetMovementEffect(false);
        }

        /// <summary>
        /// 피격 시 붉게 표시한 뒤 원래 색으로 복원하며 연속 피격은 새로 시작합니다.
        /// </summary>
        internal void PlayHitFeedback(ThreeSouls.Combat.CombatDamageInfo hit)
        {
            HitFeedback.Play(hit);
        }

        /// <summary>
        /// 풀 반환과 재설정 시 진행 중인 피격 표현을 종료합니다.
        /// </summary>
        internal void ClearHitFeedback()
        {
            HitFeedback.Clear();
        }

        /// <summary>
        /// 새 피격을 시작하며 종료 이벤트 직후 같은 상태에서 재피격되면 클립도 다시 시작합니다.
        /// </summary>
        internal void BeginHitAnimation()
        {
            if (IsAnimationPlay(AnimatorStateParams.Hit) == true)
            {
                return;
            }

            var hitHash = ParameterHashes[(int)AnimatorStateParams.Hit];

            // 종료 이벤트와 다음 Animator 평가 사이에는 bool만 해제되고 Hit 상태가 남아 있습니다.
            if (Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hitHash)
            {
                Animator.Play(hitHash, 0, 0);
            }

            SetAnimator(AnimatorStateParams.Hit, true);
        }

        internal void SetAnimator(AnimatorStateParams parameter, bool value)
        {
            if (parameter == AnimatorStateParams.Running && HasRunningParameter == false)
            {
                return;
            }

            Animator.SetBool(ParameterHashes[(int)parameter], value);
        }

        internal bool IsAnimationPlay(AnimatorStateParams parameter)
        {
            if (parameter == AnimatorStateParams.Running && HasRunningParameter == false)
            {
                return false;
            }

            return Animator.GetBool(ParameterHashes[(int)parameter]);
        }

        internal void PlayAnimation(AnimatorStateParams parameter, int layer, float normalizedTime)
        {
            Animator.Play(ParameterHashes[(int)parameter], layer, normalizedTime);
        }

        internal float GetNormalizedTime(AnimatorStateParams parameter)
        {
            var state = Animator.GetCurrentAnimatorStateInfo(0);

            return state.shortNameHash == ParameterHashes[(int)parameter] ? state.normalizedTime : 0;
        }

        internal void SetDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction.Normalize();
            var threshold = VerticalDirection == UpDownDirection.None ? 0.1f : -0.1f;

            if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x) + threshold)
            {
                VerticalDirection = direction.y > 0 ? UpDownDirection.Up : UpDownDirection.Down;
                Renderer.flipX = false;
            }
            else
            {
                VerticalDirection = UpDownDirection.None;
                Renderer.flipX = direction.x < 0;
            }

            Animator.SetFloat(DirectionHash, (int)VerticalDirection);
        }

        internal Vector2 GetFacingDirection()
        {
            if (VerticalDirection == UpDownDirection.Down)
            {
                return Vector2.down;
            }

            if (VerticalDirection == UpDownDirection.Up)
            {
                return Vector2.up;
            }

            return Renderer.flipX == true ? Vector2.left : Vector2.right;
        }

        internal Bounds GetSpriteBounds()
        {
            return Renderer.bounds;
        }

        internal void SetMovementEffect(bool play)
        {
            if (MovementDust == null)
            {
                return;
            }

            MovementDust.gameObject.SetActive(play);
            MovementDust.flip = play == true ? Vector2.right : Vector2.zero;
        }
    }
}
