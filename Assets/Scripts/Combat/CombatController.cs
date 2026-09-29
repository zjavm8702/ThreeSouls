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

    private CombatState PreviousCombatState = CombatState.None;
    private CombatState CurrentCombatState = CombatState.None;
    private ICombatWorld CombatWorld = null;

    public bool IsPlay { get; set; } = false;

    public void Initialize(ICombatStateProvider provider, ICombatWorld combatWorld)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if (combatWorld == null)
        {
            throw new ArgumentNullException(nameof(combatWorld));
        }

        if (ReferenceEquals(CombatWorld, combatWorld) == false)
        {
            CombatWorld = combatWorld;
            SetStateContainer(combatWorld);
        }

        CharacterStateMachine.Release();
        CharacterStateMachine.Initialize(provider);
        PreviousCombatState = CombatState.None;
        CurrentCombatState = CombatState.None;
    }

    public void ChangeState(CombatState newState)
    {
        if (CharacterStateContainer.ContainsKey(newState) == false || IsPlay == false)
        {
            return;
        }

        if (CurrentCombatState == newState)
        {
            return;
        }

        PreviousCombatState = CurrentCombatState;
        CurrentCombatState = newState;

        CharacterStateMachine.ChangeState(PreviousCombatState, CharacterStateContainer[newState]);
    }

    private void SetStateContainer(ICombatWorld combatWorld)
    {
        CharacterStateContainer.Clear();
        CharacterStateContainer.Add(CombatState.Wait, new CombatWaitState());
        CharacterStateContainer.Add(CombatState.MoveToDestination, new CombatMoveToDestinationState());
        CharacterStateContainer.Add(CombatState.DetectTarget, new CombatDetectTargetState(combatWorld));
        CharacterStateContainer.Add(CombatState.MoveToTarget, new CombatMoveToTargetState(combatWorld));
        CharacterStateContainer.Add(CombatState.Attack, new CombatAttackState(combatWorld));
        CharacterStateContainer.Add(CombatState.Dead, new CombatDeadState());
    }

    private void Update()
    {
        if (IsPlay == true)
        {
            CharacterStateMachine.Update();
        }
    }

    private void FixedUpdate()
    {
        if (IsPlay == true)
        {
            CharacterStateMachine.PhysicsUpdate();
        }
    }
}
