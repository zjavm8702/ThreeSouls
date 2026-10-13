using ThreeSouls.Interface;
using ThreeSouls.Data.Generated;
using UnityEngine;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 실제 장비 드랍 위치와 이름표에 필요한 키 및 등급을 전달합니다.
    /// </summary>
    public readonly struct Event_EquipmentDropped : IEventValue
    {
        public string ItemKey { get; }
        public ItemRarity Rarity { get; }
        public Vector3 WorldPosition { get; }

        /// <summary>
        /// 드랍된 장비의 키, 등급과 실제 월드 위치를 저장합니다.
        /// </summary>
        public Event_EquipmentDropped(string itemKey, ItemRarity rarity, Vector3 position)
        {
            ItemKey = itemKey;
            Rarity = rarity;
            WorldPosition = position;
        }
    }

    public readonly struct Event_EquipmentCollected : IEventValue
    {
        public EquipmentInstance Item { get; }

        public Event_EquipmentCollected(EquipmentInstance item)
        {
            Item = item;
        }
    }
}
