using ThreeSouls.Enumeration;
using ThreeSouls.Data;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;

public class CombatMoveToDestinationState : ICombatState
{
    private int TargetIndex = 0;

    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        entity.SetAnimator(AnimatorStateParams.Walking, true);
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
        if (TargetIndex >= entity.CurrentDestinations.Length)
        {
            entity.ChangeState(CombatState.Wait);
            EventUtility.TriggerEvent(
                this,
                new Event_CombatMoveCompleted(
                    entity,
                    entity.EnemyTarget,
                    CombatState.Wait));
            return;
        }

        var targetPos = entity.CurrentDestinations[TargetIndex];
        var moveSpeed = entity.CharacterData.CharMoveSpeed;

        var newPos = Vector3.MoveTowards(entity.CurrentWorldPosition, targetPos, moveSpeed * Time.deltaTime);

        entity.SetPosition(newPos);
        entity.SetSpriteRendererFlip(entity.CurrentWorldPosition.x > targetPos.x);

        var distance = Vector2.Distance(targetPos, entity.CurrentWorldPosition);

        if (distance <= ThreeSouls.Global.Constants.PATH_ARRIVAL_DISTANCE)
        {
            TargetIndex++;
        }
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity) { }

    public void OnStateExit(ICombatStateProvider entity)
    {
        TargetIndex = 0;
        entity.SetAnimator(AnimatorStateParams.Walking, false);
    }
}
