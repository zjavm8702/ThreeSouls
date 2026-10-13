using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using UnityEngine;

namespace ThreeSouls.Interface
{
    /// <summary>
    /// 캐릭터 전투 상태가 사용하는 공통 행동과 읽기 전용 정보를 제공합니다.
    /// </summary>
    public interface ICombatStateProvider
    {
        CharacterCombatData CharacterData { get; }
        GameObject EnemyTarget { get; }
        IReadOnlyList<GameObject> GetEnemySpawnTargets();
        void SetEnemyTarget(GameObject target);
        Vector2 GetWorldPosition();
        float GetMoveRadius();
        Vector2 GetMoveOffset();
        void MovePosition(Vector2 position);
        void StopMove();
        bool IsForcedMovementActive();
        bool TryUpdateForcedMovement(float deltaTime);
        void SetDirection(Vector2 direction);
        void SetAnimator(AnimatorStateParams parameter, bool value);
        bool IsAnimationPlay(AnimatorStateParams parameter);
        void PlayAnimation(AnimatorStateParams parameter, int layer = 0, float normalizedTime = 0);
        float GetPlayAnimationNormalizedTime(AnimatorStateParams parameter);
        void SetMovementEffect(bool play);
        void ChangeState(CombatState state);
    }

    /// <summary>
    /// 투사체 상태가 이동, 명중과 반환에 필요한 정보만 제공합니다.
    /// </summary>
    public interface IProjectileStateProvider
    {
        Vector2 GetWorldPosition();
        Vector2 GetTargetWorldPosition();
        bool IsTargetAlive();
        void SetPosition(Vector3 position);
        void SetDirection(Vector2 direction);
        void ApplyHit();
        void Despawn();
    }
}
