using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;

public class ProjectileMoveToTargetState : IState<IProjectileStateProvider>
{
    public void OnStateEnter(CombatState previousCombatState, IProjectileStateProvider entity)
    {
        if (entity.IsTargetAlive() == true)
        {
            entity.SetDirection(entity.GetTargetWorldPosition() - entity.GetWorldPosition());
        }
    }

    public void OnStatePhysicsUpdate(IProjectileStateProvider entity)
    {
        if (entity.IsTargetAlive() == false)
        {
            entity.Despawn();

            return;
        }

        var targetPosition = entity.GetTargetWorldPosition();
        var currentPosition = entity.GetWorldPosition();
        var newPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            ThreeSouls.Global.Constants.PROJECTILE_MOVE_SPEED * Time.fixedDeltaTime);
        entity.SetDirection(targetPosition - currentPosition);
        entity.SetPosition(newPosition);

        if (Vector2.Distance(newPosition, targetPosition) <= ThreeSouls.Global.Constants.PROJECTILE_ARRIVAL_DISTANCE)
        {
            entity.ApplyHit();
            entity.Despawn();
        }
    }

    public void OnStateUpdate(IProjectileStateProvider entity)
    {
    }

    public void OnStateExit(IProjectileStateProvider entity)
    {
    }
}
