using System.Collections.Generic;
using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

public class ProjectileController : MonoBehaviour
{
    public bool IsPlay { get; set; }

    private readonly StateMachine<IProjectileStateProvider> ProjectileStateMachine = new();
    private readonly Dictionary<CombatState, IState<IProjectileStateProvider>> ProjectileStateContainer = new();

    private CombatState PreviousCombatState = CombatState.None;
    private CombatState CurrentCombatState = CombatState.None;

    public void Initialize(IProjectileStateProvider provider)
    {
        SetStateContainer();
        ProjectileStateMachine.Initialize(provider);
    }

    public void ChangeState(CombatState newState)
    {
        if (ProjectileStateContainer.ContainsKey(newState) == false || IsPlay == false)
        {
            return;
        }

        if (CurrentCombatState == newState)
        {
            return;
        }

        PreviousCombatState = CurrentCombatState;
        CurrentCombatState = newState;

        ProjectileStateMachine.ChangeState(PreviousCombatState, ProjectileStateContainer[newState]);
    }

    private void Update()
    {
        if (IsPlay == true)
        {
            ProjectileStateMachine.Update();
        }
    }

    private void FixedUpdate()
    {
        if (IsPlay == true)
        {
            ProjectileStateMachine.PhysicsUpdate();
        }
    }

    private void SetStateContainer()
    {
        ProjectileStateContainer.Add(CombatState.MoveToTarget, new ProjectileMoveToTargetState());
    }
}
