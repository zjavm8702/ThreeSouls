using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Data
{
    public struct Event_SceneState : IEventValue
    {
        public readonly SceneState SceneState;

        public Event_SceneState(SceneState sceneState)
        {
            SceneState = sceneState;
        }
    }

    public readonly struct Event_CharacterHealthDepleted : IEventValue
    {
        public readonly ICombatStateProvider Entity;

        public Event_CharacterHealthDepleted(ICombatStateProvider entity)
        {
            Entity = entity;
        }
    }

    public readonly struct Event_CombatTargetAcquired : IEventValue
    {
        public readonly ICombatStateProvider Entity;
        public readonly GameObject Target;

        public Event_CombatTargetAcquired(ICombatStateProvider entity, GameObject target)
        {
            Entity = entity;
            Target = target;
        }
    }

    public readonly struct Event_CombatMoveCompleted : IEventValue
    {
        public readonly ICombatStateProvider Entity;
        public readonly GameObject Target;
        public readonly CombatState NextState;

        public Event_CombatMoveCompleted(
            ICombatStateProvider entity,
            GameObject target,
            CombatState nextState)
        {
            Entity = entity;
            Target = target;
            NextState = nextState;
        }
    }

    public readonly struct Event_CombatAttackCycleCompleted : IEventValue
    {
        public readonly ICombatStateProvider Entity;
        public readonly GameObject Target;

        public Event_CombatAttackCycleCompleted(ICombatStateProvider entity, GameObject target)
        {
            Entity = entity;
            Target = target;
        }
    }

    public readonly struct Event_DeathAnimationCompleted : IEventValue
    {
        public readonly ICombatStateProvider Entity;

        public Event_DeathAnimationCompleted(ICombatStateProvider entity)
        {
            Entity = entity;
        }
    }

    public struct Event_CombatState : IEventValue 
    {
        public readonly bool IsReadyCombat;

        public Event_CombatState(bool isReadyCombat)
        {
            IsReadyCombat = isReadyCombat;
        }
    }
}
