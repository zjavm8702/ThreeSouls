using System;
using ThreeSouls.Data;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 마을 장비 거래를 검증하고 재고, 인벤토리와 골드 확정 후 변경을 통지합니다.
    /// </summary>
    public sealed partial class EquipmentShopManager : BaseManager
    {
        public delegate void OnShopChangedDelegate();
        private OnShopChangedDelegate onShopChangedEventHandler;
        public event OnShopChangedDelegate OnShopChangedEventHandler
        {
            add
            {
                onShopChangedEventHandler -= value;
                onShopChangedEventHandler += value;
            }
            remove
            {
                onShopChangedEventHandler -= value;
            }
        }

        private UserManager User;
        private InventoryManager Inventory;
        private EquipmentManager Equipment;
        private EquipmentShopStock Stock;
        private bool LobbySession;
        private bool RetainStockBetweenVisits;

        public static EquipmentShopManager Get() => GameManager.Instance.Get<EquipmentShopManager>();

        public void Configure(UserManager user, InventoryManager inventory, EquipmentManager equipment, bool retainStockBetweenVisits = false)
        {
            RetainStockBetweenVisits = retainStockBetweenVisits;
            EndLobbySession();
            User = user ?? throw new ArgumentNullException(nameof(user));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        }

        public void BeginLobbySession()
        {
            RequireConfigured();
            if (LobbySession == true)
            {
                throw new InvalidOperationException("상점 세션을 중복 시작할 수 없습니다.");
            }

            EquipmentShopCatalog.Validate();
            if (Stock != null)
            {
                LobbySession = true;
                return;
            }
            var stock = new EquipmentShopStock();
            var products = EquipmentShopCatalog.GetProducts();
            for (var i = 0; i < products.Length; i++)
            {
                for (var count = 0; count < products[i].StockCount; count++)
                {
                    if (Equipment.TryCreateEquipment(products[i].ItemKey, out var item, out _) == false)
                    {
                        throw new InvalidOperationException("상점 장비 생성 실패: " + products[i].ItemKey);
                    }

                    stock.Add(new EquipmentShopOffer(item, products[i].BuyGold));
                }
            }

            Stock = stock;
            LobbySession = true;
        }

        public void EndLobbySession()
        {
            LobbySession = false;
            if (RetainStockBetweenVisits == false)
            {
                Stock = null;
            }
        }

        public EquipmentShopOffer[] GetOffers()
        {
            RequireConfigured();
            return Stock == null ? Array.Empty<EquipmentShopOffer>() : Stock.GetOffers();
        }

        public EquipmentShopOffer FindOffer(string instanceId)
        {
            RequireConfigured();
            return Stock?.FindOffer(instanceId);
        }

        public bool CanBuy(string instanceId, out string error)
        {
            if (TryRequireLobby(out error) == false)
            {
                return false;
            }

            var offer = Stock.FindOffer(instanceId);
            if (offer == null)
            {
                error = "이미 구매했거나 진열되지 않은 장비입니다.";
            }
            else if (User.UData.Gold < offer.BuyGold)
            {
                error = $"골드가 {offer.BuyGold - User.UData.Gold:N0} 부족합니다.";
            }
            else if (Inventory.CanAddEquipment(offer.Item) == false)
            {
                error = "장비 인벤토리의 빈 공간이 필요합니다.";
            }

            return error == null;
        }

        public bool TryBuy(string instanceId, out string error)
        {
            if (CanBuy(instanceId, out error) == false)
            {
                return false;
            }

            var offer = Stock.FindOffer(instanceId);
            if (Inventory.TryAddShopEquipment(offer.Item) == false)
            {
                error = "장비를 인벤토리에 추가할 수 없습니다.";
                return false;
            }

            User.PayShopCost(offer.BuyGold);
            Stock.Remove(offer);
            NotifyTrade();
            return true;
        }

        public bool TryQuoteSale(string instanceId, out int gold, out string error)
        {
            gold = 0;
            if (TryRequireLobby(out error) == false)
            {
                return false;
            }

            var item = User.UData.Inventory.FindEquipment(instanceId);
            var product = item == null ? null : EquipmentShopCatalog.FindProduct(item.ItemKey);
            if (item == null)
            {
                error = "판매할 장비가 없습니다.";
            }
            else if (item.Growth.Locked == true)
            {
                error = "잠금 장비는 판매할 수 없습니다. 인벤토리에서 잠금을 해제하세요.";
            }
            else if (Equipment.GetOwner(instanceId) != null)
            {
                error = "장착 중인 장비는 판매할 수 없습니다. 먼저 장착을 해제하세요.";
            }
            else if (product == null)
            {
                error = "이 상점에서 취급하지 않는 장비입니다.";
            }
            else if (product.SellGold <= 0 || product.SellGold > product.BuyGold)
            {
                throw new InvalidOperationException("상점 판매 가격 테이블이 유효하지 않습니다: " + item.ItemKey);
            }
            else if ((long)User.UData.Gold + product.SellGold > int.MaxValue)
            {
                error = "골드 보유 상한 때문에 판매할 수 없습니다.";
            }
            else
            {
                gold = product.SellGold;
            }

            return error == null;
        }

        public bool TrySell(string instanceId, int expectedGold, out string error)
        {
            if (TryQuoteSale(instanceId, out var gold, out error) == false)
            {
                return false;
            }

            if (gold != expectedGold)
            {
                error = "판매 가격이 변경됐습니다. 가격을 다시 확인하세요.";
                return false;
            }

            if (Inventory.TryRemoveShopEquipment(instanceId) == false)
            {
                error = "장비의 잠금/장착 상태를 다시 확인하세요.";
                return false;
            }

            User.CreditShopGold(gold);
            NotifyTrade();
            return true;
        }

        private void NotifyTrade()
        {
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Trade);
            Inventory.NotifyShopInventoryChanged();
            User.NotifyGoldChanged();
            onShopChangedEventHandler?.Invoke();
        }

        private bool TryRequireLobby(out string error)
        {
            RequireConfigured();
            error = LobbySession == true ? null : "장비 거래는 마을에서 진행하세요.";
            return error == null;
        }

        private void RequireConfigured()
        {
            if (User == null || Inventory == null || Equipment == null)
            {
                throw new InvalidOperationException("장비 상점에 사용자/인벤토리/장비 Manager를 먼저 연결해야 합니다.");
            }
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
            RetainStockBetweenVisits = false;
            EndLobbySession();
            onShopChangedEventHandler = null;
            User = null;
            Inventory = null;
            Equipment = null;
        }
    }
}
