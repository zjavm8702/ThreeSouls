using ThreeSouls.Enumeration;
using ThreeSouls.Data;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;

public class CombatMoveToTargetState : ICombatState
{
    private readonly ICombatWorld CombatWorld;
    private bool IsOnceEffect;
    private Vector2[] CurrentPath;
    private int CurrentPathIndex;

    public CombatMoveToTargetState(ICombatWorld combatWorld)
    {
        CombatWorld = combatWorld;
    }

    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        IsOnceEffect = false;
        CurrentPath = null;
        CurrentPathIndex = 0;

        CombatWorld.TryGetMovePath(entity, out CurrentPath);

        entity.SetAnimator(AnimatorStateParams.Walking, true);
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
        if (CombatWorld.IsTargetAlive(entity.EnemyTarget) == false)
        {
            var completedTarget = entity.EnemyTarget;
            entity.EnemyTarget = null;
            CompleteMove(entity, completedTarget, CombatState.DetectTarget);
            return;
        }

        var charData = entity.CharacterData;
        var targetPosition = (Vector2)entity.EnemyTarget.transform.position;

        // 공격 타입과 관계없이 실제 사거리에 들어온 시점에만 공격 타겟을 다시 결정한다.
        if (CombatWorld.IsTargetInAttackRange(entity, entity.EnemyTarget))
        {
            CompleteMove(entity, entity.EnemyTarget, charData.CharacterType == CharacterType.Hero
                ? CombatState.DetectTarget
                : CombatState.Attack);
            return;
        }

        if (CurrentPath == null || CurrentPathIndex >= CurrentPath.Length)
        {
            CompleteMove(entity, entity.EnemyTarget, charData.CharacterType == CharacterType.Hero
                ? CombatState.DetectTarget
                : CombatState.Attack);
            return;
        }

        var waypoint = CurrentPath[CurrentPathIndex];
        var remainingDistance = GetRemainingPathDistance(entity.CurrentWorldPosition);
        var moveSpeed = charData.CharMoveSpeed;

        if (charData.CharIsRunningPlay == true)
        {
            var runningDistance = charData.CharDistanceToTarget + Constants.RUNNING_DISTANCE;
            var curRunning = entity.IsAnimationPlay(AnimatorStateParams.Running);

            if (remainingDistance <= runningDistance)
            {
                moveSpeed *= charData.CharRunningSpeed;

                if (IsOnceEffect == false)
                {
                    entity.SetEffect(CombatState.MoveToTarget, true);
                    IsOnceEffect = true;
                }

                if (curRunning == false)
                {
                    entity.SetAnimator(AnimatorStateParams.Walking, false);
                    entity.SetAnimator(AnimatorStateParams.Running, true);
                }
            }
        }

        var newPos = Vector2.MoveTowards(entity.CurrentWorldPosition, waypoint, moveSpeed * Time.deltaTime);

        entity.SetPosition(newPos);
        entity.SetSpriteRendererFlip(entity.CurrentWorldPosition.x > waypoint.x);

        if (Vector2.Distance(newPos, waypoint) <= Constants.PATH_ARRIVAL_DISTANCE)
        {
            entity.SetPosition(waypoint);
            CurrentPathIndex++;
        }
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity) { }

    public void OnStateExit(ICombatStateProvider entity)
    {
        entity.SetAnimator(AnimatorStateParams.Running, false);
        entity.SetAnimator(AnimatorStateParams.Walking, false);
        entity.SetEffect(CombatState.MoveToTarget, false);
        CurrentPath = null;
        CurrentPathIndex = 0;
    }

    private void CompleteMove(
        ICombatStateProvider entity,
        GameObject target,
        CombatState nextState)
    {
        EventUtility.TriggerEvent(
            this,
            new Event_CombatMoveCompleted(entity, target, nextState));
        entity.ChangeState(nextState);
    }

    /// <summary>
    /// 현재 위치부터 남은 A* 웨이포인트까지의 실제 경로 길이를 계산한다.
    /// 직선거리가 아니라 경로 길이를 사용하므로 코너가 있어도 가속 시작 시점이 일정하다.
    /// </summary>
    private float GetRemainingPathDistance(Vector2 currentPosition)
    {
        if (CurrentPath == null || CurrentPathIndex >= CurrentPath.Length)
        {
            return 0;
        }

        var result = Vector2.Distance(currentPosition, CurrentPath[CurrentPathIndex]);

        for (var i = CurrentPathIndex; i < CurrentPath.Length - 1; i++)
        {
            result += Vector2.Distance(CurrentPath[i], CurrentPath[i + 1]);
        }

        return result;
    }
}
