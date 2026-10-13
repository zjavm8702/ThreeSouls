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

    public readonly struct Event_CombatHolyPlaceStarted : IEventValue
    {
        public readonly string RoundKey;
        public readonly int RoundNumber;
        public readonly Vector2 Position;
        public Event_CombatHolyPlaceStarted(string roundKey, int roundNumber, Vector2 position)
        { RoundKey = roundKey; RoundNumber = roundNumber; Position = position; }
    }

    public readonly struct Event_CombatStageCompleted : IEventValue
    {
        public readonly int RegionNumber;
        public readonly int StageNumber;
        public readonly bool IsVictory;
        public Event_CombatStageCompleted(int regionNumber, int stageNumber, bool victory)
        { RegionNumber = regionNumber; StageNumber = stageNumber; IsVictory = victory; }
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
