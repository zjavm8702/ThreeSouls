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

    private enum TargetPriority
    {
        Distance,
        HealthPoint,
        TankPriorityRandom
    }

    private readonly Collider2D[] HitResults = new Collider2D[10];
    private readonly GameObject[] RangeTargetResults = new GameObject[10];
    private ContactFilter2D TargetContactFilter = new();

    private CombatState PreviousCombatState;

    public CombatDetectTargetState(ICombatWorld combatWorld)
    {
        CombatWorld = combatWorld;
    }

    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        PreviousCombatState = previousCombatState;
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
        // 매니저가 이번 교전의 타겟을 미리 정했다면 기존 역할군 탐색보다 먼저 사용한다.
        // 계획이 없는 몬스터와 교전 위치에 도착한 영웅은 아래의 원래 탐색 규칙을 그대로 따른다.
        if (CombatWorld.TryGetPlannedTarget(entity, out var plannedTarget))
        {
            entity.EnemyTarget = plannedTarget;
            PublishTargetAcquired(entity);
            entity.ChangeState(CombatState.MoveToTarget);
            return;
        }

        var charCombatData = entity.CharacterData;

        //가장 가까운 적을 먼저 찾음
        if (PreviousCombatState != CombatState.MoveToTarget && charCombatData.CharacterType != CharacterType.Monster)
        {
            FindTargetByNearest(entity);

            if (entity.EnemyTarget != null)
            {
                PublishTargetAcquired(entity);
            }

            entity.ChangeState(CombatState.MoveToTarget);
        }
        else
        {
            //가장 가까운 적 이동 후 각 클래스별로 조건찾음
            switch (charCombatData.CharacterRole)
            {
                case CharacterRole.Tank:
                    FindTargetByCircleRange(entity);
                    break;
                case CharacterRole.RangedDealer:
                    FindTargetByNearest(entity);
                    break;
                case CharacterRole.MeleeDealer:
                    FindTargetByHealthPoint(entity);
                    break;
                case CharacterRole.MonsterMeleeDealer:
                    FindTargetByTankPriority(entity);
                    break;
                default:
                    break;
            }

            switch (charCombatData.CharacterType)
            {
                case CharacterType.Hero:
                    if (entity.EnemyTarget != null)
                    {
                        PublishTargetAcquired(entity);
                        entity.ChangeState(CombatState.Attack);
                    }
                    break;
                case CharacterType.Monster:
                    if (entity.EnemyTarget != null)
                    {
                        PublishTargetAcquired(entity);
                        entity.ChangeState(CombatState.MoveToTarget);
                    }
                    break;
            }
        }
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity) { }
    public void OnStateExit(ICombatStateProvider entity) 
    {
    
    }

    private void PublishTargetAcquired(ICombatStateProvider entity)
    {
        EventUtility.TriggerEvent(
            this,
            new Event_CombatTargetAcquired(entity, entity.EnemyTarget));
    }

    private void FindTargetByTankPriority(ICombatStateProvider entity)
    {
        FindTargetByCircleRange(entity, TargetPriority.TankPriorityRandom);
    }

    private void FindTargetByCircleRange(ICombatStateProvider entity)
    {
        FindTargetByCircleRange(entity, TargetPriority.Distance);
    }

    private void FindTargetByNearest(ICombatStateProvider entity)
    {
        var enemySpawnTargets = entity.EnemySpawnTargets;

        entity.EnemyTarget = FindTarget(entity, enemySpawnTargets, enemySpawnTargets.Length, TargetPriority.Distance);
    }

    private void FindTargetByHealthPoint(ICombatStateProvider entity)
    {
        FindTargetByCircleRange(entity, TargetPriority.HealthPoint);
    }

    private void FindTargetByCircleRange(ICombatStateProvider entity, TargetPriority targetPriority)
    {
        TargetContactFilter.SetLayerMask(entity.EnemyLayerMask);

        var pos = entity.CurrentWorldPosition + Vector2.up * 0.5f;  
        var radius = entity.CharacterData.CharacterType == CharacterType.Hero ? Constants.CIRCLE_RADIUS_HERO : Constants.CIRCLE_RADIUS_MONSTER;
        var hitCount = Physics2D.OverlapCircle(pos, radius, TargetContactFilter, HitResults);

        for (var i = 0; i < hitCount; i++)
        {
            RangeTargetResults[i] = HitResults[i].gameObject;
        }

        entity.EnemyTarget = FindTarget(entity, RangeTargetResults, hitCount, targetPriority);
    }

    /// <summary>
    /// 전달받은 타겟 목록에서 지정된 우선순위에 맞는 타겟(낮은 값)을 찾음
    /// </summary>
    /// <param name="entity">타겟을 탐색하는 캐릭터</param>
    /// <param name="targetObjects">탐색할 타겟 목록</param>
    /// <param name="targetCount">타겟 목록에서 실제로 사용할 개수</param>
    /// <param name="targetPriority">타겟 선택 기준</param>
    /// <returns>선택된 타겟</returns>
    private GameObject FindTarget(ICombatStateProvider entity, GameObject[] targetObjects, int targetCount, TargetPriority targetPriority)
    {
        var bestValue = Mathf.Infinity;
        var samePriorityCount = 0;

        GameObject targetObject = null;

        for (var i = 0; i < targetCount; i++)
        {
            var currentTarget = targetObjects[i];

            if (currentTarget == null)
            {
                continue;
            }

            var compareValue = GetTargetPriorityValue(entity, currentTarget, targetPriority);

            if (float.IsPositiveInfinity(compareValue) == true)
            {
                continue;
            }

            // 기존 타겟보다 우선순위 값이 낮으면 새로운 최우선 타겟
            if (compareValue < bestValue)
            {
                bestValue = compareValue;
                samePriorityCount = 1;
                targetObject = currentTarget;
                continue;
            }

            // 기존 타겟보다 우선순위 값이 높으면 선택 대상에서 제외
            if (compareValue > bestValue)
            {
                continue;
            }

            // 거리와 체력 기준은 같은 값이 나와도 기존 타겟을 유지
            if (targetPriority != TargetPriority.TankPriorityRandom)
            {
                continue;
            }

            // 동일한 우선순위의 후보가 추가
            samePriorityCount++;

            // 지금까지 발견한 동일 우선순위 후보가
            // 각각 같은 확률로 선택되도록 타겟을 교체
            if (Random.Range(0, samePriorityCount) == 0)
            {
                targetObject = currentTarget;
            }
        }

        return targetObject;
    }

    private float GetTargetPriorityValue(ICombatStateProvider entity, GameObject targetObject, TargetPriority targetPriority)
    {
        switch (targetPriority)
        {
            case TargetPriority.Distance:
                return Vector2.SqrMagnitude(entity.CurrentWorldPosition - (Vector2)targetObject.transform.position);
            case TargetPriority.HealthPoint:
                
                var targetEntity = targetObject.GetComponent<BaseEntity>();

                if (targetEntity == null)
                {
                    return Mathf.Infinity;
                }

                return targetEntity.CharacterData.CharHealthPoint;

            case TargetPriority.TankPriorityRandom:
                var roleTargetEntity = targetObject.GetComponent<BaseEntity>();

                if (roleTargetEntity == null || roleTargetEntity.CharacterData == null)
                {
                    return Mathf.Infinity;
                }

                if (roleTargetEntity.CharacterData.CharacterRole == CharacterRole.Tank)
                {
                    return 0;
                }

                return 1;

            default:
                return Mathf.Infinity;
        }
    }
}
