using System;
using System.Collections.Generic;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 상점 테이블의 상품, 가격과 접근 규칙을 조회하고 지원하는 정책을 검증합니다.
    /// </summary>
    public static class EquipmentShopCatalog
    {
        public const string VillageShopKey = "VillageEquipment";

        public static VillageShopTableDataRow GetVillageShop()
        {
            var rows = LobbyTables.Get<VillageShopTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].ShopKey == VillageShopKey)
                {
                    return rows[i];
                }
            }

            throw new InvalidOperationException("VillageEquipment 상점 정의가 필요합니다.");
        }

        public static EquipmentShopTableDataRow[] GetProducts()
        {
            var products = new List<EquipmentShopTableDataRow>();
            var rows = LobbyTables.Get<EquipmentShopTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].ShopKey == VillageShopKey)
                {
                    products.Add(rows[i]);
                }
            }

            products.Sort((left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));
            return products.ToArray();
        }

        public static EquipmentShopTableDataRow FindProduct(string itemKey)
        {
            var rows = LobbyTables.Get<EquipmentShopTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].ShopKey == VillageShopKey && rows[i].ItemKey == itemKey)
                {
                    return rows[i];
                }
            }

            return null;
        }

        public static void Validate()
        {
            var shop = GetVillageShop();
            if (string.IsNullOrWhiteSpace(shop.DisplayName) == true ||
                float.IsNaN(shop.InteractRadius) == true || float.IsInfinity(shop.InteractRadius) == true ||
                shop.InteractRadius <= 0 || shop.InteractRadius > 1)
            {
                throw new InvalidOperationException("상점 이름과 입구 도착 거리(0 초과~1)를 확인하세요.");
            }

            var products = GetProducts();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            if (products.Length == 0)
            {
                throw new InvalidOperationException("상점 상품이 필요합니다.");
            }

            for (var i = 0; i < products.Length; i++)
            {
                var row = products[i];
                if (keys.Add(row.ItemKey) == false || orders.Add(row.DisplayOrder) == false ||
                    EquipmentRules.IsValidDefinition(LobbyTables.GetEquipment(row.ItemKey)) == false ||
                    row.BuyGold <= 0 || row.SellGold <= 0 || row.SellGold > row.BuyGold ||
                    row.StockCount < 0 || row.StockCount > 100 || row.DisplayOrder < 0)
                {
                    throw new InvalidOperationException("상점 상품/가격/재고/진열 순서를 확인하세요: " + row.ItemKey);
                }
            }
        }
    }
}
