using System;
using System.Collections;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using UnityEngine;

/// <summary>
/// 유지된 사용자 세션의 로비를 조립하고 마을 월드 표시 담당자와 연결합니다.
/// </summary>
public sealed class Scene_Village : MonoBehaviour
{
    [SerializeField] private ThreeSouls.Village.VillageWorldPresentation WorldPresentation;
    private GameObject Lobby;
    private UIManager UI;

    private IEnumerator Start()
    {
        var assets = AddressableManager.Get();
        ThreeSouls.Utility.StartupLoadTrace.Mark("VillageScene");
        yield return LobbyResourceCatalog.PreLoad(assets, LobbyTables.Get<EquipmentTableDataTable>());
        ThreeSouls.Utility.StartupLoadTrace.Mark("LobbyImagesReady");
        yield return assets.PreLoadBatch<GameObject>(new[] { Prefabs.UI_Lobby.ToAddress(), Prefabs.UI_EquipmentShop.ToAddress() }, true);
        var user = UserManager.Get();
        var equipment = EquipmentManager.Get();
        var inventory = InventoryManager.Get();
        var growth = EquipmentGrowthManager.Get();
        var heroGrowth = user.GetHeroGrowthManager();
        var runes = user.GetRuneManager();
        var preparation = user.GetDungeonPreparationManager();
        var supplies = LobbySupplyManager.Get();
        var shop = EquipmentShopManager.Get();
        growth.BeginLobbySession();
        heroGrowth.BeginLobbySession();
        runes.BeginLobbySession();
        preparation.BeginLobbySession();
        supplies.Configure(user);
        supplies.BeginLobbySession();
        shop.Configure(user, inventory, equipment, true);
        shop.BeginLobbySession();
        UI = UIManager.Get();
        Lobby = Instantiate(assets.LoadAsset<GameObject>(Prefabs.UI_Lobby.ToAddress()), UI.GetUICanvas(), false);
        UI.OnClearUIEventHandler += ClearLobby;
        var tabs = Require<UI_LobbyTabs>();
        var items = Require<UI_LobbyInventory>();
        var characters = Require<UI_LobbyCharacters>();
        var forge = Require<UI_LobbyForge>();
        var supplyView = Require<UI_LobbySupplies>();
        var shopObject = Instantiate(assets.LoadAsset<GameObject>(Prefabs.UI_EquipmentShop.ToAddress()), tabs.transform, false);
        shopObject.SetActive(false);
        var shopView = shopObject.GetComponent<UI_EquipmentShop>();
        tabs.SetUp(user);
        items.SetUp(user.UData, inventory, equipment, TableManager.Get(), growth);
        characters.SetUp(equipment, TableManager.Get(), heroGrowth);
        forge.SetUp(growth, equipment, user, TableManager.Get(), items);
        supplyView.SetUp(supplies, user);
        shopView.SetUp(shop, user, equipment, TableManager.Get(), growth, tabs);
        var runeViews = Lobby.GetComponentsInChildren<UI_RuneStones>(true);
        for (var i = 0; i < runeViews.Length; i++)
        {
            runeViews[i].SetUp(runes, user);
        }
        Require<UI_DungeonSelection>().SetUp(preparation, runes, user,
            WorldPresentation != null ? WorldPresentation.GetShopInteraction() : null);
        Require<UI_DungeonPreparation>().ConnectTravel(DungeonTravelManager.Get());
        Lobby.transform.Find("Lobby/Village/OpenEquipmentShop").GetComponent<UnityEngine.UI.Button>().onClick.AddListener(shopView.Open);
        if (WorldPresentation != null)
        {
            WorldPresentation.Connect(tabs, shopView);
        }
        else
        {
            Require<UI_VillageHome>().Show();
        }
        ThreeSouls.Utility.StartupLoadTrace.Mark("LobbyReady");
        ThreeSouls.Persistence.PersistentSession.Current?.VillageReady();
    }

    private T Require<T>() where T : Component
    {
        return Lobby.GetComponentInChildren<T>(true)
            ?? throw new InvalidOperationException($"로비에 {typeof(T).Name}을 연결하세요.");
    }

    private void ClearLobby()
    {
        if (Lobby != null)
        {
            if (WorldPresentation != null)
            {
                WorldPresentation.Disconnect();
            }
            Lobby.SetActive(false);
            Destroy(Lobby);
            Lobby = null;
        }
    }

    private void OnDestroy()
    {
        if (UI != null)
        {
            UI.OnClearUIEventHandler -= ClearLobby;
        }
        ClearLobby();
    }
}
