using System.Collections.Generic;
using ThreeSouls.Character;
using UnityEngine;

namespace ThreeSouls.Interface
{
    /// <summary>
    /// 스킬 담당자가 필요한 전투 단계와 생존 캐릭터를 조회합니다.
    /// </summary>
    public interface ICombatSkillWorld
    {
        bool IsCombatActive();
        IReadOnlyList<BaseEntity> GetAliveHeroes();
        BaseEntity GetCharacterInstance(GameObject instance);
        bool IsTargetAlive(GameObject target);
        bool CanMoveDirectly(ICombatStateProvider entity, Vector2 destination);
    }
}
