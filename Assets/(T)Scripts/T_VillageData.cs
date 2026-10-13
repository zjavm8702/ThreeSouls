using System;
using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using UnityEngine;

/// <summary>
/// 테스트 로비의 테이블과 가데이터를 준비하고 화면에 분야별 Manager를 연결합니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class T_VillageData : MonoBehaviour
{
    [SerializeField] private UI_LobbyTabs Tabs;
    [SerializeField] private UI_LobbyInventory Inventory;
    [SerializeField] private UI_LobbyCharacters Characters;
    [SerializeField] private UI_LobbyForge Forge;
    [SerializeField] private UI_LobbySupplies Supplies;
    [SerializeField] private UI_EquipmentShop Shop;
    private GameManager Managers;

    private void Awake()
    {
        if (GameManager.TryGetInstance(out _) == true || FindAnyObjectByType<GameManager>() != null)
        {
            throw new InvalidOperationException("테스트 로비는 기존 GameManager 없이 직접 실행해야 합니다.");
        }

        if (Tabs == null || Inventory == null || Characters == null || Forge == null || Supplies == null || Shop == null)
        {
            throw new InvalidOperationException("테스트 로비의 탭과 UI를 연결해야 합니다.");
        }

        Managers = GameManager.Instance;
        var loader = new T_InGameAssetLoader(Managers.Get<AddressableManager>());
        loader.Load<CharacterTableDataTable>(ScriptableObjectDatas.CharacterTableData.ToAddress());
        loader.Load<StringTableDataTable>(ScriptableObjectDatas.StringTableData.ToAddress());
        var definitions = loader.Load<EquipmentTableDataTable>(ScriptableObjectDatas.EquipmentTableData.ToAddress());
        var start = LobbyTables.Get<LobbyTestStartTableDataTable>().Items[0];
        LobbyTableValidation.Validate();
        var sprites = LobbyResourceCatalog.GetSpriteAddresses(definitions);
        for (var i = 0; i < sprites.Length; i++)
        {
            loader.Load<Sprite>(sprites[i]);
        }
        var heroes = LobbyTables.Get<LobbyHeroTableDataTable>().Items;
        for (var i = 0; i < heroes.Length; i++)
        {
            loader.Load<RuntimeAnimatorController>(heroes[i].VillageAnimatorPath);
        }
        var tables = Managers.Get<TableManager>();
        var user = Managers.Get<UserManager>();
        user.ApplyTableRules();
        var equipment = Managers.Get<EquipmentManager>();
        var inventory = Managers.Get<InventoryManager>();
        SeedEquipment(definitions, equipment, inventory);
        user.AddCombatReward(start.StartingGold, 0);
        user.AddEnhancementStones(start.StartingEnhancementStones);
        user.AddGrowthStones(ThreeSouls.Growth.HeroGrowthRules.GetRule().TestStartingGrowthStones);
        var heroGrowth = user.GetHeroGrowthManager();
        heroGrowth.BeginLobbySession();
        var runes = user.GetRuneManager();
        runes.BeginLobbySession();
        runes.GrantTestRunes();
        Characters.GetComponentInChildren<UI_RuneStones>(true).SetUp(runes, user);
        var preparation = user.GetDungeonPreparationManager();
        preparation.BeginLobbySession();
        Tabs.GetComponentInChildren<UI_DungeonSelection>(true).SetUp(preparation, runes, user,
            FindAnyObjectByType<ThreeSouls.Village.VillageShopInteraction>());
        var growth = Managers.Get<EquipmentGrowthManager>();
        growth.BeginLobbySession();
        var supplies = Managers.Get<LobbySupplyManager>();
        supplies.Configure(user);
        supplies.BeginLobbySession();
        supplies.GrantStartingPotions(ConsumableType.SmallHealingPotion, start.StartingSmallPotions);
        supplies.GrantStartingPotions(ConsumableType.PartyHealingPotion, start.StartingPartyPotions);
        Tabs.SetUp(user);
        Inventory.SetUp(user.UData, inventory, equipment, tables, growth);
        Characters.SetUp(equipment, tables, heroGrowth);
        Forge.SetUp(growth, equipment, user, tables, Inventory);
        Supplies.SetUp(supplies, user);
        var shop = Managers.Get<EquipmentShopManager>();
        shop.Configure(user, inventory, equipment);
        shop.BeginLobbySession();
        Shop.SetUp(shop, user, equipment, tables, growth);
    }

    /// <summary>
    /// 기존 테이블의 모든 장비를 복수 생성하고 세 영웅의 시작 무기를 장착합니다.
    /// </summary>
    private void SeedEquipment(EquipmentTableDataTable definitions,
        EquipmentManager equipment, InventoryManager inventory)
    {
        var rows = LobbyTables.Get<LobbyStartingEquipmentTableDataTable>().Items;
        var maximumCopies = rows.Where(x => x.PresetKey == "TestVillage").Max(x => x.Count);
        for (var copy = 0; copy < maximumCopies; copy++)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.PresetKey != "TestVillage" || copy >= row.Count)
                {
                    continue;
                }
                if (equipment.TryCreateEquipment(row.ItemKey, out var item, out _) == false ||
                    inventory.TryAddEquipment(item) == false)
                {
                    throw new InvalidOperationException($"테스트 장비 생성 실패: {row.ItemKey}");
                }
                if (copy == 0 && string.IsNullOrEmpty(row.EquipToHeroKey) == false &&
                    equipment.TryEquip(row.EquipToHeroKey, item.InstanceId, out var error) == false)
                {
                    throw new InvalidOperationException(error);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (Managers != null)
        {
            Managers.Release();
            Destroy(Managers.gameObject);
        }
    }
}
