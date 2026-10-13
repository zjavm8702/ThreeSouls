using System;
using System.Collections.Generic;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 진열된 장비와 확정된 가격을 보관합니다. 구입 전후 동일 장비의 추첨값을 유지합니다.
    /// </summary>
    public sealed class EquipmentShopOffer
    {
        public EquipmentInstance Item { get; }
        public int BuyGold { get; }

        internal EquipmentShopOffer(EquipmentInstance item, int buyGold)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            BuyGold = buyGold;
        }
    }

    /// <summary>
    /// 마을 세션의 미구매 장비를 소유하고 판매 목록을 제한된 조회로 제공합니다.
    /// </summary>
    public sealed class EquipmentShopStock
    {
        private readonly List<EquipmentShopOffer> Offers = new();

        public EquipmentShopOffer[] GetOffers()
        {
            return Offers.ToArray();
        }

        public EquipmentShopOffer FindOffer(string instanceId)
        {
            for (var i = 0; i < Offers.Count; i++)
            {
                if (Offers[i].Item.InstanceId == instanceId)
                {
                    return Offers[i];
                }
            }

            return null;
        }

        internal void Add(EquipmentShopOffer offer)
        {
            if (FindOffer(offer.Item.InstanceId) != null || offer.BuyGold <= 0)
            {
                throw new InvalidOperationException("유효하고 고유한 상점 상품만 진열할 수 있습니다.");
            }

            Offers.Add(offer);
        }

        internal void Remove(EquipmentShopOffer offer)
        {
            if (Offers.Remove(offer) == false)
            {
                throw new InvalidOperationException("진열 중인 상품만 구매 확정할 수 있습니다.");
            }
        }
    }
}
