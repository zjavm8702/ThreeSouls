using System;
using ThreeSouls.Enumeration;

namespace ThreeSouls.Interface
{
    //In(반공변성) : 자신 또는 부모 객체에 해당하는 타입을 자식 변수에 해당하는 타입으로 형변환.
    public interface IState<in T> where T : class
    {
        void OnStateEnter(CombatState previousCombatState, T entity);
        void OnStateUpdate(T entity);
        void OnStatePhysicsUpdate(T entity);
        void OnStateExit(T entity);
    }

    public interface ICombatState : IState<ICombatStateProvider> { }

    public interface IStateMachine<T> where T : class
    {
        void Initialize(T entity);
        void ChangeState(CombatState prevCombatState, IState<T> newState);
        void Update();
        void PhysicsUpdate();
        void Release();
    }
}
