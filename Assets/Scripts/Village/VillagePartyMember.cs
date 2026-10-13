using System;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 마을 영웅 하나의 위치와 이동·정지·방향별 표현을 소유합니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class VillagePartyMember : MonoBehaviour
    {
        private enum VerticalDirection
        {
            None = 0,
            Down = 1,
            Up = 2
        }

        private SpriteRenderer Renderer;
        private Animator Animator;
        private VerticalDirection Direction = VerticalDirection.Down;
        private static readonly int WalkingHash = Animator.StringToHash("Walking");
        private static readonly int DirectionHash = Animator.StringToHash("Direction");

        public void Initialize()
        {
            Renderer = transform.Find("Body").GetComponent<SpriteRenderer>();
            Animator = GetComponent<Animator>();

            if (Animator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException("마을 영웅의 방향별 Animator Controller가 필요합니다.");
            }

            Renderer.flipX = false;
            Animator.SetFloat(DirectionHash, (int)Direction);
            Stop();
        }

        public Vector2 GetWorldPosition()
        {
            return transform.position;
        }

        public void MoveTo(Vector2 position)
        {
            var delta = position - GetWorldPosition();

            if (delta.sqrMagnitude < 0.0000000001f)
            {
                Stop();

                return;
            }

            var threshold = Direction == VerticalDirection.None ? 0.1f : -0.1f;
            var direction = delta.normalized;

            if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x) + threshold)
            {
                Direction = direction.y > 0 ? VerticalDirection.Up : VerticalDirection.Down;
                Renderer.flipX = false;
            }
            else
            {
                Direction = VerticalDirection.None;
                Renderer.flipX = direction.x < 0;
            }

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            Renderer.sortingOrder = 300 - Mathf.RoundToInt(position.y * 10);
            Animator.SetFloat(DirectionHash, (int)Direction);
            Animator.SetBool(WalkingHash, true);
        }

        public void Stop()
        {
            Animator.SetBool(WalkingHash, false);
            Renderer.sortingOrder = 300 - Mathf.RoundToInt(transform.position.y * 10);
        }

        public void StopOnDisable()
        {
            if (Animator == null || Renderer == null)
            {
                return;
            }
            Stop();
        }
    }
}
