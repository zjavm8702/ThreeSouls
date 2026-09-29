using UnityEngine;

namespace ThreeSouls.Interface
{
    /// <summary>
    /// 전투 상태가 전투 전체 흐름에 질의하거나 요청할 때 사용하는 계약입니다.
    /// 상태는 구체 매니저나 서비스 로케이터를 알지 않습니다.
    /// </summary>
    public interface ICombatWorld
    {
        bool TryGetMovePath(ICombatStateProvider entity, out Vector2[] path);
        bool TryGetPlannedTarget(ICombatStateProvider entity, out GameObject target);
        bool IsTargetAlive(GameObject target);
        bool IsTargetInAttackRange(ICombatStateProvider attacker, GameObject target);
    }
}
