using System;
using System.Collections.Generic;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 영웅별 장착 슬롯을 소유하고 확정된 슬롯 변경만 통지합니다.
    /// </summary>
    public sealed partial class HeroEquipmentCollection
    {
        public delegate void OnEquipmentChangedDelegate(string characterKey);
        private OnEquipmentChangedDelegate onEquipmentChangedEventHandler;
        public event OnEquipmentChangedDelegate OnEquipmentChangedEventHandler
        {
            add
            {
                onEquipmentChangedEventHandler -= value;
                onEquipmentChangedEventHandler += value;
            }
            remove
            {
                onEquipmentChangedEventHandler -= value;
            }
        }

        private readonly List<HeroEquipmentData> Heroes = new();

        public string GetEquippedId(string characterKey, EquipSlot slot)
        {
            return FindHero(characterKey)?.Get(slot);
        }

        public string GetOwner(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) == true)
            {
                return null;
            }

            for (var i = 0; i < Heroes.Count; i++)
            {
                var hero = Heroes[i];

                if (hero.Get(EquipSlot.Weapon) == instanceId ||
                    hero.Get(EquipSlot.Armor) == instanceId ||
                    hero.Get(EquipSlot.Accessory) == instanceId)
                {
                    return hero.GetCharacterKey();
                }
            }

            return null;
        }

        internal void SetSlot(string characterKey, EquipSlot slot, string instanceId)
        {
            var hero = FindHero(characterKey);

            if (hero == null)
            {
                hero = new HeroEquipmentData(characterKey);
                Heroes.Add(hero);
            }

            if (hero.Get(slot) == instanceId)
            {
                return;
            }

            hero.Set(slot, instanceId);
            onEquipmentChangedEventHandler?.Invoke(characterKey);
        }

        private HeroEquipmentData FindHero(string characterKey)
        {
            for (var i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i].GetCharacterKey() == characterKey)
                {
                    return Heroes[i];
                }
            }

            return null;
        }
    }
}
