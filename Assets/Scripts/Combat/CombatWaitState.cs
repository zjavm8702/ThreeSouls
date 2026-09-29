using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;

public class CombatWaitState : ICombatState
{
    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        entity.SetAnimator(AnimatorStateParams.Idle, true);
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity) { }

    public void OnStateUpdate(ICombatStateProvider entity) { }

    public void OnStateExit(ICombatStateProvider entity)
    {
        entity.SetAnimator(AnimatorStateParams.Idle, false);
    }
}
