using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;

public class CombatDetectTargetState : ICombatState
{
    private readonly ICombatWorld CombatWorld;
    private float NextSearchTime;

    public CombatDetectTargetState(ICombatWorld combatWorld)
    {
        CombatWorld = combatWorld;
    }

    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        NextSearchTime = 0;
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
        if (Time.time < NextSearchTime)
        {
            return;
        }

        NextSearchTime = Time.time + Mathf.Max(0.05f, entity.CharacterData.CharDetectDelay);

        var previousTarget = entity.EnemyTarget;

        if (entity.CharacterData.CharacterType == CharacterType.Hero)
        {
            entity.SetEnemyTarget(FindNearestTarget(entity));
        }
        else if (CanSelectTarget(entity, previousTarget) == false)
        {
            entity.SetEnemyTarget(FindMonsterTarget(entity));
        }

        if (entity.EnemyTarget == null)
        {
            if (CombatWorld.TryGetDestination(entity, out _) == true)
            {
                entity.ChangeState(CombatState.MoveToDestination);
            }

            return;
        }

        if (entity.EnemyTarget != previousTarget)
        {
            PublishTargetAcquired(entity);
        }

        entity.ChangeState(CombatWorld.IsTargetInAttackRange(entity, entity.EnemyTarget) == true ? CombatState.Attack : CombatState.MoveToTarget);
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity)
    {
    }

    public void OnStateExit(ICombatStateProvider entity)
    {
    }

    private void PublishTargetAcquired(ICombatStateProvider entity)
    {
        EventUtility.TriggerEvent(this, new Event_CombatTargetAcquired(entity, entity.EnemyTarget));
    }

    /// <summary>
    /// 영웅은 탐색할 때 현재 위치에서 가장 가까운 생존 적을 선택합니다.
    /// 역할, 체력 또는 무작위 우선순위를 사용하지 않습니다.
    /// </summary>
    private GameObject FindNearestTarget(ICombatStateProvider entity)
    {
        var targets = entity.GetEnemySpawnTargets();
        var position = entity.GetWorldPosition();
        var nearestDistance = Mathf.Infinity;
        GameObject targetObject = null;

        for (var i = 0; i < targets.Count; i++)
        {
            var candidate = targets[i];

            if (CanSelectTarget(entity, candidate) == false)
            {
                continue;
            }

            var distance = Vector2.SqrMagnitude(position - CombatWorld.GetTargetPosition(candidate));

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                targetObject = candidate;
            }
        }

        return targetObject;
    }

    /// <summary>
    /// 몬스터는 기존의 탱커 우선 선택을 유지하고 같은 역할 후보는 균등하게 선택합니다.
    /// </summary>
    private GameObject FindMonsterTarget(ICombatStateProvider entity)
    {
        var targets = entity.GetEnemySpawnTargets();
        var bestPriority = int.MaxValue;
        var candidateCount = 0;
        GameObject targetObject = null;

        for (var i = 0; i < targets.Count; i++)
        {
            var candidate = targets[i];

            if (CanSelectTarget(entity, candidate) == false)
            {
                continue;
            }

            var target = candidate.GetComponent<BaseEntity>();
            var priority = target.CharacterData.CharacterRole == CharacterRole.Tank ? 0 : 1;

            if (priority < bestPriority)
            {
                bestPriority = priority;
                candidateCount = 1;
                targetObject = candidate;
                continue;
            }

            if (priority > bestPriority)
            {
                continue;
            }

            candidateCount++;

            if (Random.Range(0, candidateCount) == 0)
            {
                targetObject = candidate;
            }
        }

        return targetObject;
    }

    private bool CanSelectTarget(ICombatStateProvider entity, GameObject target)
    {
        return CombatWorld.CanEngageTarget(entity, target) == true &&
            CombatWorld.IsTargetExcluded(entity, target) == false;
    }
}
