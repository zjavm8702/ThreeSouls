using ThreeSouls.Enumeration;
using ThreeSouls.Data;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;

public class CombatAttackState : ICombatState
{
    private readonly ICombatWorld CombatWorld;
    private float DelayTimer;

    public CombatAttackState(ICombatWorld combatWorld)
    {
        CombatWorld = combatWorld;
    }

    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        DelayTimer = 0;
        entity.SetAnimator(AnimatorStateParams.Attack, true);
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity)
    {
        if (CanAttackTarget(entity) == false)
        {
            return;
        }

        DelayTimer += Time.fixedDeltaTime;

        var playAttack = entity.IsAnimationPlay(AnimatorStateParams.Attack);
        var normalizedTime = entity.GetPlayAnimationNormalizedTime(AnimatorStateParams.Attack);

        if (playAttack == true && normalizedTime < 0.9f)
        {
            return;
        }

        if (entity.CharacterData.CharAttackInterval > DelayTimer)
        {
            return;
        }

        DelayTimer = 0;
        entity.PlayAnimation(AnimatorStateParams.Attack);
        EventUtility.TriggerEvent(
            this,
            new Event_CombatAttackCycleCompleted(entity, entity.EnemyTarget));
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
        CanAttackTarget(entity);
    }

    public void OnStateExit(ICombatStateProvider entity)
    {
        DelayTimer = 0;
        entity.SetAnimator(AnimatorStateParams.Attack, false);
    }

    /// <summary>
    /// 공격 타겟의 생존 여부와 실제 거리를 검사한다.
    /// 타겟이 사거리 밖이면 공격 애니메이션을 끝내고 A* 이동 상태로 되돌린다.
    /// </summary>
    private bool CanAttackTarget(ICombatStateProvider entity)
    {
        if (CombatWorld.IsTargetAlive(entity.EnemyTarget) == false)
        {
            entity.EnemyTarget = null;
            entity.ChangeState(CombatState.DetectTarget);
            return false;
        }

        var targetPosition = (Vector2)entity.EnemyTarget.transform.position;
        entity.SetSpriteRendererFlip(entity.CurrentWorldPosition.x > targetPosition.x);

        if (CombatWorld.IsTargetInAttackRange(entity, entity.EnemyTarget))
        {
            return true;
        }

        entity.ChangeState(CombatState.MoveToTarget);
        return false;
    }
}
