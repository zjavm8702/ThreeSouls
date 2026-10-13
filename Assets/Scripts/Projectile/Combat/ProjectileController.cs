using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;

/// <summary>
/// 투사체마다 보관한 이동 상태를 시작하고 풀 반환 시 중지합니다.
/// </summary>
public class ProjectileController : MonoBehaviour
{
    private readonly StateMachine<IProjectileStateProvider> StateMachine = new();
    private readonly ProjectileMoveToTargetState MoveState = new();
    private bool IsPlay;

    internal void BeginFlight(IProjectileStateProvider provider)
    {
        StateMachine.Release();
        StateMachine.Initialize(provider);
        StateMachine.ChangeState(CombatState.None, MoveState);
        IsPlay = true;
    }

    internal void Stop()
    {
        IsPlay = false;
        StateMachine.Release();
    }

    private void Update()
    {
        if (IsPlay == true)
        {
            StateMachine.Update();
        }
    }

    private void FixedUpdate()
    {
        if (IsPlay == true)
        {
            StateMachine.PhysicsUpdate();
        }
    }
}
