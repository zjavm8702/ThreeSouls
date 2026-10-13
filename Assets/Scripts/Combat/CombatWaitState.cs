using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;

public class CombatWaitState : ICombatState
{
    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        entity.StopMove();
        entity.SetAnimator(AnimatorStateParams.Walking, false);
        entity.SetAnimator(AnimatorStateParams.Running, false);
        entity.PlayAnimation(AnimatorStateParams.Idle);
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity)
    {
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
    }

    public void OnStateExit(ICombatStateProvider entity)
    {
    }
}
