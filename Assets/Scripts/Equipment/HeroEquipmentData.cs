using System;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 한 영웅의 장착 슬롯을 소유하고 컬렉션 내부에서만 변경을 허용합니다.
    /// </summary>
    internal sealed class HeroEquipmentData
    {
        private readonly string CharacterKey;
        private string WeaponInstanceId;
        private string ArmorInstanceId;
        private string AccessoryInstanceId;

        internal HeroEquipmentData(string characterKey)
        {
            if (string.IsNullOrWhiteSpace(characterKey) == true)
            {
                throw new ArgumentException("장착 데이터에는 영웅 키가 필요합니다.", nameof(characterKey));
            }

            CharacterKey = characterKey;
        }

        internal string GetCharacterKey()
        {
            return CharacterKey;
        }

        internal string Get(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Weapon => WeaponInstanceId,
                EquipSlot.Armor => ArmorInstanceId,
                EquipSlot.Accessory => AccessoryInstanceId,
                _ => throw new ArgumentOutOfRangeException(nameof(slot))
            };
        }

        internal void Set(EquipSlot slot, string instanceId)
        {
            switch (slot)
            {
                case EquipSlot.Weapon:
                    WeaponInstanceId = instanceId;
                    break;
                case EquipSlot.Armor:
                    ArmorInstanceId = instanceId;
                    break;
                case EquipSlot.Accessory:
                    AccessoryInstanceId = instanceId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }
}
