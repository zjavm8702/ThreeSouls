using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Combat
{
    public readonly struct CharacterSpawnRequest
    {
        public CharacterType CharacterType { get; }
        public CharacterTableDataRow CharacterData { get; }
        public ThreeSoulsRuleDataRow RuleData { get; }
        public string SpriteLibraryName { get; }
        public Transform Parent { get; }
        public Vector3 Position { get; }
        public ICombatWorld CombatWorld { get; }

        public CharacterSpawnRequest(
            CharacterType characterType,
            CharacterTableDataRow characterData,
            ThreeSoulsRuleDataRow ruleData,
            string spriteLibraryName,
            Transform parent,
            Vector3 position,
            ICombatWorld combatWorld)
        {
            CharacterType = characterType;
            CharacterData = characterData;
            RuleData = ruleData;
            SpriteLibraryName = spriteLibraryName;
            Parent = parent;
            Position = position;
            CombatWorld = combatWorld;
        }
    }
}
