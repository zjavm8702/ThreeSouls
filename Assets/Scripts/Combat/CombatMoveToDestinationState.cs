using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;

/// <summary>
/// 성소 또는 정렬 위치로 이동하고 이동 중 접근한 적을 탐지한다.
/// </summary>
public class CombatMoveToDestinationState : ICombatState
{
    private readonly ICombatWorld CombatWorld;
    private Vector2[] CurrentPath;
    private int CurrentPathIndex;
    private Vector2 PathDestination;
    private float NextPathTime;
    private float NextSearchTime;

    public CombatMoveToDestinationState(ICombatWorld combatWorld)
    {
        CombatWorld = combatWorld;
    }

    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        CurrentPath = null;
        CurrentPathIndex = 0;
        NextPathTime = 0;
        NextSearchTime = 0;
        entity.SetEnemyTarget(null);
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
        if (Time.time < NextSearchTime)
        {
            return;
        }

        NextSearchTime = Time.time + Mathf.Max(0.05f, entity.CharacterData.CharDetectDelay);

        var targets = entity.GetEnemySpawnTargets();

        for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            var target = targets[targetIndex];

            if (CombatWorld.CanEngageTarget(entity, target) == true && CombatWorld.IsTargetExcluded(entity, target) == false)
            {
                entity.ChangeState(CombatState.DetectTarget);

                return;
            }
        }
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity)
    {
        if (CombatWorld.TryGetDestination(entity, out var destination) == false)
        {
            entity.ChangeState(CombatState.DetectTarget);

            return;
        }

        var position = entity.GetWorldPosition();

        if (Vector2.Distance(position, destination) <= 0.03f)
        {
            entity.StopMove();
            entity.SetAnimator(AnimatorStateParams.Walking, false);
            entity.SetAnimator(AnimatorStateParams.Running, false);

            return;
        }

        if (CombatWorld.CanMoveDirectly(entity, destination) == true)
        {
            CurrentPath = null;
        }
        else
        {
            // 이동 대열의 목적지가 조금 움직여도 이미 확보한 우회 경로는 계속 사용한다.
            if (CurrentPath != null && Vector2.SqrMagnitude(destination - PathDestination) > 1f)
            {
                CurrentPath = null;
            }

            if (CurrentPath == null)
            {
                entity.SetAnimator(AnimatorStateParams.Walking, false);
                entity.SetAnimator(AnimatorStateParams.Running, false);
                entity.StopMove();

                if (Time.time < NextPathTime)
                {
                    return;
                }

                var result = CombatWorld.GetDestinationPath(entity, destination, out CurrentPath);

                if (result == MovePathResult.Pending)
                {
                    return;
                }

                NextPathTime = Time.time + Constants.MOVE_PATH_REQUEST_INTERVAL;

                if (result != MovePathResult.Found)
                {
                    return;
                }

                PathDestination = destination;
                CurrentPathIndex = 0;
            }

            while (CurrentPathIndex < CurrentPath.Length &&
                Vector2.Distance(position, CurrentPath[CurrentPathIndex]) <= Constants.PATH_ARRIVAL_DISTANCE &&
                (CurrentPathIndex + 1 == CurrentPath.Length ||
                CombatWorld.CanMoveDirectly(entity, CurrentPath[CurrentPathIndex + 1]) == true))
            {
                CurrentPathIndex++;
            }

            if (CurrentPathIndex >= CurrentPath.Length)
            {
                CurrentPath = null;
                NextPathTime = 0;

                return;
            }

            destination = CurrentPath[CurrentPathIndex];
        }

        entity.SetAnimator(AnimatorStateParams.Walking, entity.CharacterData.CharacterType != CharacterType.Hero);
        entity.SetAnimator(AnimatorStateParams.Running, entity.CharacterData.CharacterType == CharacterType.Hero);
        entity.SetMovementEffect(false);

        if (CombatWorld.MoveTowards(entity, destination, entity.CharacterData.CharMoveSpeed) == false)
        {
            CurrentPath = null;
        }
    }

    public void OnStateExit(ICombatStateProvider entity)
    {
        entity.StopMove();
        entity.SetAnimator(AnimatorStateParams.Walking, false);
        entity.SetAnimator(AnimatorStateParams.Running, false);
        CurrentPath = null;
    }
}
