using System;
using System.Collections.Generic;
using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;

public class CombatController : MonoBehaviour
{
    private readonly StateMachine<ICombatStateProvider> CharacterStateMachine = new();
    private readonly Dictionary<CombatState, ICombatState> CharacterStateContainer = new();
    private CombatState CurrentCombatState = CombatState.None;
    private ICombatWorld CombatWorld = null;
    private ICombatStateProvider Entity;
    private bool IsPlay;

    public void Initialize(ICombatStateProvider provider, ICombatWorld combatWorld)
    {
        Entity = provider;
        if (ReferenceEquals(CombatWorld, combatWorld) == false)
        {
            CombatWorld = combatWorld;
            SetStateContainer(combatWorld);
        }

        CharacterStateMachine.Release();
        CharacterStateMachine.Initialize(provider);
        CurrentCombatState = CombatState.None;
    }

    public void ChangeState(CombatState newState)
    {
        if (IsPlay == false)
        {
            return;
        }

        if (CurrentCombatState == newState)
        {
            return;
        }

        var previousState = CurrentCombatState;
        var nextState = CharacterStateContainer[newState];
        CurrentCombatState = newState;
        CharacterStateMachine.ChangeState(previousState, nextState);
    }

    internal void Play()
    {
        IsPlay = true;
    }

    internal void Stop()
    {
        IsPlay = false;
        CharacterStateMachine.Release();
        CurrentCombatState = CombatState.None;
    }

    private void SetStateContainer(ICombatWorld combatWorld)
    {
        CharacterStateContainer.Clear();
        CharacterStateContainer.Add(CombatState.Wait, new CombatWaitState());
        CharacterStateContainer.Add(CombatState.DetectTarget, new CombatDetectTargetState(combatWorld));
        CharacterStateContainer.Add(CombatState.MoveToDestination, new CombatMoveToDestinationState(combatWorld));
        CharacterStateContainer.Add(CombatState.MoveToTarget, new CombatMoveToTargetState(combatWorld));
        CharacterStateContainer.Add(CombatState.Attack, new CombatAttackState(combatWorld));
        CharacterStateContainer.Add(CombatState.Dead, new CombatDeadState());
    }

    private void Update()
    {
        if (IsPlay == true && Entity.IsForcedMovementActive() == false)
        {
            CharacterStateMachine.Update();
        }
    }

    private void FixedUpdate()
    {
        if (IsPlay == true)
        {
            if (Entity.TryUpdateForcedMovement(Time.fixedDeltaTime) == false)
            {
                CharacterStateMachine.PhysicsUpdate();
            }
        }
    }
}
