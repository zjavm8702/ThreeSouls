using ThreeSouls.Enumeration;
using ThreeSouls.Data;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;

public class CombatDeadState : ICombatState
{
    private float DeadTimer;
    private bool IsDespawnCompleted;

    public void OnStateEnter(CombatState previousCombatState, ICombatStateProvider entity)
    {
        DeadTimer = 0;
        IsDespawnCompleted = false;
        entity.SetAnimator(AnimatorStateParams.Dead, true);
    }

    public void OnStateExit(ICombatStateProvider entity)
    {
        entity.SetAnimator(AnimatorStateParams.Dead, false);
    }

    public void OnStatePhysicsUpdate(ICombatStateProvider entity)
    {
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
        if (IsDespawnCompleted == true)
        {
            return;
        }

        DeadTimer += Time.deltaTime;
        var normalizedTime = entity.GetPlayAnimationNormalizedTime(AnimatorStateParams.Dead);

        // 사망 클립이 끝났거나 애니메이터 전환에 실패했을 때의 제한 시간이 지나면 풀로 반환한다.
        if (normalizedTime < 0.95f && DeadTimer < Constants.DEAD_DESPAWN_FALLBACK_TIME)
        {
            return;
        }

        IsDespawnCompleted = true;
        EventUtility.TriggerEvent(this, new Event_DeathAnimationCompleted(entity));
    }
}
