using UnityEngine;
using ThreeSouls.Enumeration;

namespace ThreeSouls.Interface
{
    /// <summary>
    /// 전투 상태가 전투 전체 흐름에 질의하거나 요청할 때 사용하는 계약입니다.
    /// 상태는 구체 매니저나 서비스 로케이터를 알지 않습니다.
    /// </summary>
    public interface ICombatWorld
    {
        bool CanEngageTarget(ICombatStateProvider entity, GameObject target);
        bool TryGetDestination(ICombatStateProvider entity, out Vector2 destination);
        MovePathResult GetDestinationPath(ICombatStateProvider entity, Vector2 destination, out Vector2[] path);
        bool CanMoveDirectly(ICombatStateProvider entity, Vector2 destination);
        MovePathResult GetMovePath(ICombatStateProvider entity, out Vector2[] path);
        bool MoveTowards(ICombatStateProvider entity, Vector2 destination, float moveSpeed);
        bool IsTargetExcluded(ICombatStateProvider entity, GameObject target);
        void ExcludeTarget(ICombatStateProvider entity, GameObject target);
        Vector2 GetTargetPosition(GameObject target);
        bool IsTargetAlive(GameObject target);
        bool IsTargetInAttackRange(ICombatStateProvider attacker, GameObject target);
        void SpawnDamageText(Vector3 spawnPos, float damage);
        void SpawnDamageText(Vector3 spawnPos, ThreeSouls.Combat.CombatDamageInfo damage);
        //void SpawnRewardGold(Vector3 spawnPos);
    }
}
