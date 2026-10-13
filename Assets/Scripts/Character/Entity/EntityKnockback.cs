using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Character
{
    /// <summary>
    /// 짧은 넉백의 거리와 수명을 소유하고 기존 이동 검사로 벽 관통을 막습니다.
    /// </summary>
    internal sealed class EntityKnockback
    {
        private const float Distance = 0.12f;
        private const float Duration = 0.08f;
        private readonly ICombatStateProvider Entity;
        private readonly ICombatWorld World;
        private Vector2 Direction;
        private float ElapsedTime;

        internal bool IsActive { get; private set; }

        internal EntityKnockback(ICombatStateProvider entity, ICombatWorld world)
        {
            Entity = entity;
            World = world;
        }

        internal void Begin(Vector2 direction)
        {
            Direction = direction.normalized;
            ElapsedTime = 0;
            IsActive = Direction.sqrMagnitude > 0;

            if (IsActive == true)
            {
                Entity.StopMove();
            }
        }

        /// <summary>
        /// 넉백이 끝나는 프레임까지 이동을 소유하여 AI가 같은 프레임에 덮어쓰지 않게 합니다.
        /// </summary>
        internal bool TryUpdate(float deltaTime)
        {
            if (IsActive == false)
            {
                return false;
            }

            var previousProgress = ElapsedTime / Duration;
            ElapsedTime = Mathf.Min(Duration, ElapsedTime + deltaTime);

            if (Mathf.Approximately(ElapsedTime, Duration) == true)
            {
                ElapsedTime = Duration;
            }

            var progress = ElapsedTime / Duration;
            var previousDistance = Distance * (1 - (1 - previousProgress) * (1 - previousProgress));
            var distance = Distance * (1 - (1 - progress) * (1 - progress));
            var destination = Entity.GetWorldPosition() + Direction * (distance - previousDistance);

            if (World.CanMoveDirectly(Entity, destination) == false)
            {
                Clear();

                return true;
            }

            Entity.MovePosition(destination);
            IsActive = ElapsedTime < Duration;

            return true;
        }

        internal void Clear()
        {
            if (IsActive == true)
            {
                Entity.StopMove();
            }

            IsActive = false;
            Direction = Vector2.zero;
            ElapsedTime = 0;
        }
    }
}
