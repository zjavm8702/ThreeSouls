using System;
using ThreeSouls.Data;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 마을의 강화, 잠금과 분해 요청 및 재화 변경 순서를 확정합니다.
    /// </summary>
    public sealed class EquipmentGrowthManager : BaseManager
    {
        public delegate void OnEquipmentGrowthChangedDelegate();
        private OnEquipmentGrowthChangedDelegate onEquipmentGrowthChangedEventHandler;
        public event OnEquipmentGrowthChangedDelegate OnEquipmentGrowthChangedEventHandler
        {
            add
            {
                onEquipmentGrowthChangedEventHandler -= value;
                onEquipmentGrowthChangedEventHandler += value;
            }
            remove
            {
                onEquipmentGrowthChangedEventHandler -= value;
            }
        }

        private UserManager User;
        private Inventory Items;
        private EquipmentManager Equipment;
        private TableManager Tables;
        private bool LobbySession;

        public static EquipmentGrowthManager Get() => GameManager.Instance.Get<EquipmentGrowthManager>();

        internal void Configure(UserManager user, Inventory items, EquipmentManager equipment, TableManager tables)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
        }

        public void BeginLobbySession()
        {
            RequireConfigured();
            LobbySession = true;
        }

        public void EndLobbySession()
        {
            LobbySession = false;
        }

        public bool TryUpgrade(string instanceId, out string error)
        {
            if (TryGetLobbyItem(instanceId, out var item, out error) == false)
            {
                return false;
            }

            var rule = LobbyTables.GetRules();
            if ((item.Growth.Locked == true && rule.AllowLockedUpgrade == false) ||
                (Equipment.GetOwner(instanceId) != null && rule.AllowEquippedUpgrade == false))
            {
                error = "현재 테이블 설정에서는 잠금/장착 장비를 강화할 수 없습니다.";
                return false;
            }
            if (EquipmentUpgradeRules.TryGetCost(item.Growth.Level, out var gold, out var stones) == false)
            {
                error = $"최대 강화 단계 +{EquipmentUpgradeRules.GetMaxLevel()}입니다.";
                return false;
            }

            if ((long)item.Growth.InvestedStones + stones > int.MaxValue)
            {
                error = "강화 투자량 상한을 초과합니다.";
                return false;
            }
            if (User.UData.Gold < gold || User.UData.EnhancementStones < stones)
            {
                error = $"부족한 재료: 골드 {Math.Max(0, gold - User.UData.Gold):N0} / 강화석 {Math.Max(0, stones - User.UData.EnhancementStones)}";
                return false;
            }

            User.PayEnhancementCost(gold, stones);
            item.Growth.Upgrade(stones, rule.EquipmentMaxLevel);
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Upgrade);
            Equipment.NotifyGrowthChanged(instanceId);
            User.NotifyEnhancementCurrencyChanged(true);
            onEquipmentGrowthChangedEventHandler?.Invoke();
            error = null;
            return true;
        }

        public bool TrySetLocked(string instanceId, bool locked, out string error)
        {
            if (TryGetLobbyItem(instanceId, out var item, out error) == false)
            {
                return false;
            }

            if (item.Growth.Locked != locked)
            {
                item.Growth.SetLocked(locked);
                onEquipmentGrowthChangedEventHandler?.Invoke();
            }

            return true;
        }

        public bool TryDismantle(string instanceId, out int awardedStones, out string error)
        {
            awardedStones = 0;

            if (TryGetLobbyItem(instanceId, out var item, out error) == false)
            {
                return false;
            }

            if (item.Growth.Locked == true || Equipment.GetOwner(instanceId) != null)
            {
                error = "잠금을 해제하고 장착 중인 장비를 해제한 뒤 분해하세요.";
                return false;
            }

            var stones = GetDismantleStones(item);

            if ((long)User.UData.EnhancementStones + stones > int.MaxValue)
            {
                error = "강화석 보유 한도를 초과합니다.";
                return false;
            }

            User.CreditDismantleStones((int)stones);

            if (InventoryManager.Get().TryDiscard(instanceId) == false)
            {
                throw new InvalidOperationException("검증된 분해 장비를 제거하지 못했습니다.");
            }

            awardedStones = (int)stones;
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Dismantle);
            User.NotifyEnhancementCurrencyChanged(false);
            onEquipmentGrowthChangedEventHandler?.Invoke();
            return true;
        }

        public long GetDismantleStones(EquipmentInstance item)
        {
            RequireConfigured();
            var row = Tables.GetEquipmentTableData(item.ItemKey);
            return EquipmentUpgradeRules.GetDismantleStones(row.Rarity, item.Growth.InvestedStones);
        }

        public override void Initialize()
        {
        }

        public override void OnSceneChanged()
        {
            EndLobbySession();
        }

        public override void Release()
        {
            EndLobbySession();
            onEquipmentGrowthChangedEventHandler = null;
            User = null;
            Items = null;
            Equipment = null;
            Tables = null;
        }

        private bool TryGetLobbyItem(string id, out EquipmentInstance item, out string error)
        {
            RequireConfigured();
            item = Items.FindEquipment(id);
            error = LobbySession == false ? "장비 성장은 마을에서만 진행할 수 있습니다."
                : item == null ? "보유한 장비를 선택하세요." : null;
            return error == null;
        }

        private void RequireConfigured()
        {
            if (User == null || Items == null || Equipment == null || Tables == null)
            {
                throw new InvalidOperationException("장비 성장에 사용자 데이터와 담당 Manager를 먼저 연결해야 합니다.");
            }
        }
    }
}
