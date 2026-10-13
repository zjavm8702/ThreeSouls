using System;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Interface;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 장비 생성, 장착 검증, 해제, 능력치 조회와 영웅 변경 알림을 담당합니다.
    /// </summary>
    public sealed class EquipmentManager : BaseManager, IHeroEquipmentSource
    {
        private Inventory Inventory;
        private HeroEquipmentCollection Equipment;
        private IHeroEquipmentSource.OnEquipmentChangedDelegate onEquipmentChangedEventHandler;

        public event IHeroEquipmentSource.OnEquipmentChangedDelegate OnEquipmentChangedEventHandler
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

        public static EquipmentManager Get() => GameManager.Instance.Get<EquipmentManager>();

        public override void Initialize()
        {
        }

        public override void OnSceneChanged()
        {
        }

        internal void Configure(Inventory inventory, HeroEquipmentCollection equipment)
        {
            if (inventory == null || equipment == null)
            {
                throw new ArgumentNullException(inventory == null ? nameof(inventory) : nameof(equipment));
            }

            if (Equipment != null)
            {
                Equipment.OnEquipmentChangedEventHandler -= OnEquipmentChanged;
            }

            Inventory = inventory;
            Equipment = equipment;
            Equipment.OnEquipmentChangedEventHandler += OnEquipmentChanged;
        }

        /// <summary>
        /// 테이블 정의가 유효한 장비의 고유 ID와 능력치를 생성합니다.
        /// </summary>
        public bool TryCreateEquipment(string itemKey, out EquipmentInstance item, out ItemRarity rarity)
        {
            var row = TableManager.Get().GetEquipmentTableData(itemKey);
            item = null;
            rarity = default;

            if (EquipmentRules.IsValidDefinition(row) == false)
            {
                return false;
            }

            var rule = LobbyTables.GetRules();
            var growth = new EquipmentGrowthData(rule.InitialEquipmentLevel, rule.InitialInvestedStones,
                rule.InitialLocked, rule.EquipmentMaxLevel);
            item = EquipmentRules.Roll(row, () => UnityEngine.Random.value, growth);
            rarity = row.Rarity;

            return true;
        }

        internal bool IsValidEquipment(EquipmentInstance item)
        {
            return item != null &&
                EquipmentRules.IsValidDefinition(TableManager.Get().GetEquipmentTableData(item.ItemKey)) == true;
        }

        public bool CanEquip(string heroKey, string instanceId)
        {
            RequireConfigured();

            return TryValidateEquip(heroKey, instanceId, out _, out _) == true &&
                Equipment.GetOwner(instanceId) != heroKey;
        }

        public bool TryEquip(string heroKey, string instanceId, out string error)
        {
            RequireConfigured();

            if (TryValidateEquip(heroKey, instanceId, out var row, out error) == false)
            {
                return false;
            }

            Equipment.SetSlot(heroKey, row.EquipSlot, instanceId);
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Equip);

            return true;
        }

        public bool TryUnequip(string heroKey, EquipSlot slot)
        {
            RequireConfigured();

            if (Enum.IsDefined(typeof(EquipSlot), slot) == false ||
                string.IsNullOrEmpty(Equipment.GetEquippedId(heroKey, slot)) == true)
            {
                return false;
            }

            Equipment.SetSlot(heroKey, slot, null);
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Equip);

            return true;
        }

        public string GetOwner(string instanceId)
        {
            RequireConfigured();

            return Equipment.GetOwner(instanceId);
        }

        public EquipmentInstance GetEquippedEquipment(string heroKey, EquipSlot slot)
        {
            RequireConfigured();

            return Inventory.FindEquipment(Equipment.GetEquippedId(heroKey, slot));
        }

        public EquipmentBonuses GetEquipmentBonuses(CharacterTableDataRow hero)
        {
            RequireConfigured();
            float attack = 0;
            float hp = 0;
            float speed = 0;

            if (hero == null || hero.CharacterType != CharacterType.Hero)
            {
                return new EquipmentBonuses(0, 0, 0);
            }

            var slots = (EquipSlot[])Enum.GetValues(typeof(EquipSlot));

            for (var i = 0; i < slots.Length; i++)
            {
                var item = GetEquippedEquipment(hero.StrKey, slots[i]);
                var row = item == null ? null : TableManager.Get().GetEquipmentTableData(item.ItemKey);

                if (EquipmentRules.CanEquip(row, hero) == false || row.EquipSlot != slots[i])
                {
                    continue;
                }

                var values = GetItemBonuses(item);
                attack += values.AttackDmg;
                hp += values.MaxHP;
                speed += values.AttackSpeedBonus;
            }

            return new EquipmentBonuses(attack, hp, speed);
        }

        public EquipmentBonuses GetItemBonuses(EquipmentInstance item)
        {
            RequireConfigured();
            var row = TableManager.Get().GetEquipmentTableData(item.ItemKey);

            return EquipmentUpgradeRules.Calculate(item, row.EquipSlot, item.Growth.Level);
        }

        internal void NotifyGrowthChanged(string instanceId)
        {
            var owner = GetOwner(instanceId);

            if (owner != null)
            {
                onEquipmentChangedEventHandler?.Invoke(owner);
            }
        }

        public override void Release()
        {
            if (Equipment != null)
            {
                Equipment.OnEquipmentChangedEventHandler -= OnEquipmentChanged;
            }

            onEquipmentChangedEventHandler = null;
            Inventory = null;
            Equipment = null;
        }

        private bool TryValidateEquip(
            string heroKey,
            string instanceId,
            out EquipmentTableDataRow row,
            out string error)
        {
            row = null;

            if (string.IsNullOrWhiteSpace(heroKey) == true)
            {
                error = "영웅을 선택해야 합니다.";

                return false;
            }

            var tables = TableManager.Get();
            var hero = tables.GetCharacterTableDataTable(heroKey);
            var item = Inventory.FindEquipment(instanceId);
            row = item == null ? null : tables.GetEquipmentTableData(item.ItemKey);

            if (EquipmentRules.CanEquip(row, hero) == false)
            {
                error = "이 영웅이 사용할 수 없는 장비입니다.";

                return false;
            }

            var owner = Equipment.GetOwner(instanceId);

            if (owner != null && owner != heroKey)
            {
                error = "다른 영웅이 장착 중입니다.";

                return false;
            }

            error = null;

            return true;
        }

        private void OnEquipmentChanged(string characterKey)
        {
            onEquipmentChangedEventHandler?.Invoke(characterKey);
        }

        private void RequireConfigured()
        {
            if (Inventory == null || Equipment == null)
            {
                throw new InvalidOperationException("EquipmentManager에 사용자 데이터를 먼저 연결해야 합니다.");
            }
        }
    }
}
