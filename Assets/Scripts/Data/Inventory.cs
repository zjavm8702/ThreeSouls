using System;
using System.Collections.Generic;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 보유 아이템과 용량을 보호하며 변경 불가능한 장비를 조회합니다.
    /// </summary>
    public sealed partial class Inventory
    {
        public delegate void OnInventoryChangedDelegate();
        private OnInventoryChangedDelegate onInventoryChangedEventHandler;
        public event OnInventoryChangedDelegate OnInventoryChangedEventHandler
        {
            add
            {
                onInventoryChangedEventHandler -= value;
                onInventoryChangedEventHandler += value;
            }
            remove
            {
                onInventoryChangedEventHandler -= value;
            }
        }

        public int Capacity { get; private set; }
        private readonly List<EquipmentInstance> Items = new();

        public Inventory() : this(80)
        {
        }

        public Inventory(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
        }

        internal void ConfigureCapacity(int capacity)
        {
            if (capacity < 1 || capacity < Items.Count)
            {
                throw new InvalidOperationException("보유 장비보다 작은 인벤토리 용량을 적용할 수 없습니다.");
            }
            Capacity = capacity;
        }

        public int GetCount()
        {
            return Items.Count;
        }

        public EquipmentInstance[] GetItems()
        {
            var snapshot = new EquipmentInstance[Items.Count];

            for (var i = 0; i < Items.Count; i++)
            {
                snapshot[i] = Items[i];
            }

            return snapshot;
        }

        public EquipmentInstance FindEquipment(string instanceId)
        {
            var index = FindIndex(instanceId);

            return index < 0 ? null : Items[index];
        }

        internal bool TryAdd(EquipmentInstance item, bool notify = true)
        {
            if (item == null || Items.Count >= Capacity || FindIndex(item.InstanceId) >= 0)
            {
                return false;
            }

            Items.Add(item);
            if (notify == true)
            {
                NotifyChanged();
            }

            return true;
        }

        internal bool TryRemove(string instanceId, bool notify = true)
        {
            var index = FindIndex(instanceId);

            if (index < 0)
            {
                return false;
            }

            Items.RemoveAt(index);
            if (notify == true)
            {
                NotifyChanged();
            }

            return true;
        }

        /// <summary>
        /// 검증된 보상 묶음을 한 번에 반영하고 확정된 인벤토리 변경을 통지합니다.
        /// </summary>
        internal void AddBatch(EquipmentInstance[] items)
        {
            if (items == null || Items.Count + items.Length > Capacity)
            {
                throw new ArgumentException("Inventory reward batch exceeds reserved capacity.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < Items.Count; i++)
            {
                ids.Add(Items[i].InstanceId);
            }

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null || ids.Add(items[i].InstanceId) == false)
                {
                    throw new ArgumentException("Invalid/duplicate inventory reward.");
                }
            }

            for (var i = 0; i < items.Length; i++)
            {
                Items.Add(items[i]);
            }

            if (items.Length > 0)
            {
                onInventoryChangedEventHandler?.Invoke();
            }
        }

        private int FindIndex(string instanceId)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].InstanceId == instanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        internal void NotifyChanged()
        {
            onInventoryChangedEventHandler?.Invoke();
        }
    }
}
