using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;

public class ProjectileMoveToTargetState : IState<IProjectileStateProvider>
{
    public void OnStateEnter(CombatState previousCombatState, IProjectileStateProvider entity)
    {
        
    }

    public void OnStatePhysicsUpdate(IProjectileStateProvider entity)
    {
        if (entity.IsTargetAlive == false)
        {
            entity.Despawn();
            return;
        }

        var targetPosition = entity.TargetWorldPosition;
        var direction = targetPosition - entity.CurrentWorldPosition;
        var newPosition = Vector2.MoveTowards(
            entity.CurrentWorldPosition,
            targetPosition,
            ThreeSouls.Global.Constants.PROJECTILE_MOVE_SPEED * Time.fixedDeltaTime);

        entity.SetSpriteRendererFlip(entity.CurrentWorldPosition.x < targetPosition.x);
        //var rotZ = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        //entity.SetRotation(Quaternion.Euler(0, 0, rotZ));
        entity.SetPosition(newPosition);

        

        if (Vector2.Distance(newPosition, targetPosition) <=
            ThreeSouls.Global.Constants.PROJECTILE_ARRIVAL_DISTANCE)
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
