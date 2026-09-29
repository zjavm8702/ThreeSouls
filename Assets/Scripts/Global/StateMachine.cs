using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Global
{
    public class StateMachine<T> : IStateMachine<T> where T : class
    {
        private T Entity = null;
        private IState<T> CurrentState = null;
        private IState<T> PreviousState = null;


        public void Initialize(T entity)
        {
            Entity = entity;
        }

        public void ChangeState(CombatState prevCombatState, IState<T> newState)
        {
            if (IsValid() == false)
            {
                return;
            }

            if (newState == null)
            {
                ThreeSoulsLog.Error($"{typeof(T)} StateMachine newState null");
                return;
            }

            if (newState.Equals(CurrentState) == true)
            {
                return;
            }

            PreviousState = CurrentState;

            CurrentState?.OnStateExit(Entity);
            CurrentState = newState;
            CurrentState.OnStateEnter(prevCombatState, Entity);
        }

        public void Update()
        {
            if (IsValid() == true)
            {
                CurrentState?.OnStateUpdate(Entity);
            }
        }

        public void PhysicsUpdate()
        {
            if (IsValid() == true)
            {
                CurrentState?.OnStatePhysicsUpdate(Entity);
            }
        }

        public void Release()
        {
            Entity = null;
            PreviousState = null;
            CurrentState = null;
        }

        private bool IsValid()
        {
            if (Entity != null)
            {
                return true;
            }

            ThreeSoulsLog.Error($"{typeof(T)} StateMachine entity null");

            return false;
        }
    }
}