using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;

public class CombatMoveToTargetState : ICombatState
{
    private readonly ICombatWorld CombatWorld;
    private Vector2[] CurrentPath;
    private int CurrentPathIndex;
    private Vector2 PathTargetPosition;
    private Vector2 PreviousPosition;
    private float NextPathCheckTime;
    private float NextPathRequestTime;
    private float StuckTimer;
    private bool IsDirectMove;

    public CombatMoveToTargetState(ICombatWorld combatWorld)
    {
        CombatWorld = combatWorld;
    }

    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        CurrentPath = null;
        CurrentPathIndex = 0;
        NextPathCheckTime = 0;
        NextPathRequestTime = 0;
        StuckTimer = 0;
        PreviousPosition = entity.GetWorldPosition();
        IsDirectMove = true;
        entity.SetAnimator(AnimatorStateParams.Walking, entity.CharacterData.CharacterType != CharacterType.Hero);
        entity.SetAnimator(AnimatorStateParams.Running, entity.CharacterData.CharacterType == CharacterType.Hero);
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity)
    {
        if (CombatWorld.CanEngageTarget(entity, entity.EnemyTarget) == false)
        {
            entity.SetEnemyTarget(null);
            entity.ChangeState(CombatState.DetectTarget);

            return;
        }

        if (CombatWorld.IsTargetInAttackRange(entity, entity.EnemyTarget) == true)
        {
            entity.ChangeState(CombatState.Attack);

            return;
        }

        var position = entity.GetWorldPosition();
        var targetPosition = CombatWorld.GetTargetPosition(entity.EnemyTarget);
        var direction = targetPosition - position;
        var stopDistance = Mathf.Min(entity.CharacterData.CharDistanceToTarget, direction.magnitude);
        var destination = targetPosition - direction.normalized * stopDistance;

        if (Time.time >= NextPathCheckTime)
        {
            NextPathCheckTime = Time.time + Constants.MOVE_PATH_CHECK_INTERVAL;
            IsDirectMove = CombatWorld.CanMoveDirectly(entity, destination) == true && CombatWorld.CanMoveDirectly(entity, targetPosition) == true;

            if (IsDirectMove == true)
            {
                CurrentPath = null;
                CurrentPathIndex = 0;
            }
            else if (Vector2.SqrMagnitude(targetPosition - PathTargetPosition) > 0.25f)
            {
                CurrentPath = null;
            }
        }

        if (IsDirectMove == false)
        {
            if (CurrentPath == null || CurrentPathIndex >= CurrentPath.Length)
            {
                if (Time.time < NextPathRequestTime)
                {
                    entity.StopMove();

                    return;
                }

                var result = CombatWorld.GetMovePath(entity, out CurrentPath);

                if (result == MovePathResult.Pending)
                {
                    entity.StopMove();

                    return;
                }

                NextPathRequestTime = Time.time + Constants.MOVE_PATH_REQUEST_INTERVAL;

                if (result == MovePathResult.NotFound)
                {
                    CombatWorld.ExcludeTarget(entity, entity.EnemyTarget);
                    entity.SetEnemyTarget(null);
                    entity.ChangeState(CombatState.DetectTarget);

                    return;
                }

                CurrentPathIndex = 0;
                PathTargetPosition = targetPosition;
                StuckTimer = 0;
            }

            // 예약 위치가 아니라 실제 Rigidbody2D 위치로 도착을 판단한다.
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
                NextPathRequestTime = 0;

                return;
            }

            destination = CurrentPath[CurrentPathIndex];
        }

        var isRunning = entity.CharacterData.CharacterType == CharacterType.Hero;
        var speed = entity.CharacterData.CharMoveSpeed;
        entity.SetAnimator(AnimatorStateParams.Walking, !isRunning);
        entity.SetAnimator(AnimatorStateParams.Running, isRunning);
        entity.SetMovementEffect(false);

        if (Vector2.SqrMagnitude(position - PreviousPosition) < 0.000001f)
        {
            StuckTimer += Time.fixedDeltaTime;
        }
        else
        {
            StuckTimer = 0;
        }

        PreviousPosition = position;

        if (CombatWorld.MoveTowards(entity, destination, speed) == false || StuckTimer >= Constants.MOVE_STUCK_TIME)
        {
            IsDirectMove = false;
            CurrentPath = null;
            NextPathCheckTime = Time.time + Constants.MOVE_PATH_CHECK_INTERVAL;
            StuckTimer = 0;
        }
    }

    public void OnStateExit(ICombatStateProvider entity)
    {
        entity.StopMove();
        entity.SetAnimator(AnimatorStateParams.Running, false);
        entity.SetAnimator(AnimatorStateParams.Walking, false);
        entity.SetMovementEffect(false);
        CurrentPath = null;
        CurrentPathIndex = 0;
    }
}
