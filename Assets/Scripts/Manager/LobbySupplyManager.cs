using System;
using ThreeSouls.Data;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 로비의 물약 구매 및 출정 준비 요청을 검증하고 확정 결과를 알립니다.
    /// </summary>
    public sealed class LobbySupplyManager : BaseManager
    {
        public delegate void OnPreparationChangedDelegate();
        private OnPreparationChangedDelegate onPreparationChangedEventHandler;
        public event OnPreparationChangedDelegate OnPreparationChangedEventHandler
        {
            add
            {
                onPreparationChangedEventHandler -= value;
                onPreparationChangedEventHandler += value;
            }
            remove
            {
                onPreparationChangedEventHandler -= value;
            }
        }

        private UserManager User;
        private ConsumableInventory Items;
        private SupplyLoadout Loadout;
        private bool LobbySession;

        public static LobbySupplyManager Get() => GameManager.Instance.Get<LobbySupplyManager>();

        internal void Configure(UserManager user)
        {
            if (User != null && ReferenceEquals(User, user) == true)
            {
                return;
            }
            User = user ?? throw new ArgumentNullException(nameof(user));
            Items = user.UData.Consumables;
            Loadout = user.UData.Supplies;
            Loadout.SetThreshold(LobbyTables.GetSupplyRules().DefaultAutoHeal);
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

        public bool TryBuy(ConsumableType type, int quantity, out string error)
        {
            if (TryRequireLobby(out error) == false)
            {
                return false;
            }

            if (ConsumableCatalog.IsPotion(type) == false || quantity < LobbyTables.GetSupplyRules().MinPurchase || quantity > LobbyTables.GetSupplyRules().MaxPurchase)
            {
                error = "구매할 물약과 수량을 확인하세요.";
                return false;
            }

            var total = (long)ConsumableCatalog.GetPrice(type) * quantity;
            if (total > int.MaxValue)
            {
                error = "구매 비용이 재화 상한을 초과합니다.";
                return false;
            }
            var price = (int)total;

            if (User.UData.Gold < price || Items.CanAdd(type, quantity) == false)
            {
                error = User.UData.Gold < price ? $"골드가 {price - User.UData.Gold:N0} 부족합니다."
                    : "물약 보유 한도를 초과합니다.";
                return false;
            }

            User.PayShopCost(price);
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Trade);
            Items.Add(type, quantity);
            User.NotifyGoldChanged();
            onPreparationChangedEventHandler?.Invoke();
            return true;
        }

        /// <summary>
        /// 테스트 조립자가 시작 물약을 지급합니다. 편성 수량은 바꾸지 않습니다.
        /// </summary>
        internal void GrantStartingPotions(ConsumableType type, int quantity)
        {
            RequireConfigured();

            if (quantity == 0)
            {
                return;
            }

            Items.Add(type, quantity);
            onPreparationChangedEventHandler?.Invoke();
        }

        public bool TrySetSlot(int index, ConsumableType type, int count, out string error)
        {
            if (TryRequireLobby(out error) == false)
            {
                return false;
            }

            if (index < 0 || index >= SupplyLoadout.SlotCount || count < 0 || count > SupplyLoadout.GetMaximumPerSlot() ||
                (type != ConsumableType.None && ConsumableCatalog.IsPotion(type) == false) ||
                (count > 0 && ConsumableCatalog.IsPotion(type) == false))
            {
                error = $"공급품은 {SupplyLoadout.SlotCount}슬롯에 각각 최대 {SupplyLoadout.GetMaximumPerSlot()}개까지 편성할 수 있습니다.";
                return false;
            }

            type = count == 0 ? ConsumableType.None : type;
            var previous = Loadout.GetSlot(index);
            var assigned = Loadout.GetAssigned(type) - (previous.Type == type ? previous.Count : 0);

            if (LobbyTables.GetSupplyRules().AllowDuplicateType == false && assigned > 0 && count > 0)
            {
                error = "같은 종류의 공급품을 중복 편성할 수 없습니다.";
                return false;
            }
            if (count > 0 && assigned + count > Items.GetOwned(type))
            {
                error = "보유 수량이 부족합니다. 상점에서 물약을 구매하세요.";
                return false;
            }

            if (previous.Type != type || previous.Count != count)
            {
                Loadout.SetSlot(index, type, count);
                onPreparationChangedEventHandler?.Invoke();
            }

            return true;
        }

        public bool TrySetAutoHeal(AutoHealThreshold threshold, out string error)
        {
            if (TryRequireLobby(out error) == false)
            {
                return false;
            }

            if (Enum.IsDefined(typeof(AutoHealThreshold), threshold) == false)
            {
                error = "자동 회복 기준을 확인하세요.";
                return false;
            }

            if (Loadout.Threshold != threshold)
            {
                Loadout.SetThreshold(threshold);
                onPreparationChangedEventHandler?.Invoke();
            }

            return true;
        }

        public bool TryClearSlots(out string error)
        {
            if (TryRequireLobby(out error) == false)
            {
                return false;
            }

            if (Loadout.GetTotalAssigned() > 0)
            {
                Loadout.Clear();
                onPreparationChangedEventHandler?.Invoke();
            }

            return true;
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
            onPreparationChangedEventHandler = null;
            User = null;
            Items = null;
            Loadout = null;
        }

        private bool TryRequireLobby(out string error)
        {
            RequireConfigured();
            error = LobbySession == true ? null : "물약 구매와 공급품 준비는 마을에서 진행하세요.";
            return error == null;
        }

        private void RequireConfigured()
        {
            if (User == null)
            {
                throw new InvalidOperationException("공급품 Manager에 사용자 데이터를 먼저 연결해야 합니다.");
            }
        }
    }
}
