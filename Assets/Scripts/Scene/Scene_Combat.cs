using System.Collections;
using ThreeSouls.Data;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using ThreeSouls.UI;
using UnityEngine;

public class Scene_Combat : MonoBehaviour
{
    private UI_HUD_Top TopHUD = null;
    private UI_HUD_Bottom BottomHUD = null;
   
    private IEnumerator Start()
    {
        UserManager.Get();
        var assets = AddressableManager.Get();
        yield return assets.AddressableLoadAssetAsync<GameObject>(Prefabs.UI_HUD_Top.ToAddress(), false);
        yield return assets.AddressableLoadAssetAsync<GameObject>(Prefabs.UI_HUD_Botttom.ToAddress(), false);
        yield return assets.AddressableLoadAssetAsync<GameObject>(Prefabs.UI_HUD_Supplies.ToAddress(), false);
        TopHUD = UIManager.Get().CreateHUD(assets.LoadAsset<GameObject>(Prefabs.UI_HUD_Top.ToAddress()).GetComponent<UI_HUD_Top>());
        GameCombatManager.Get().ConfigureRewardTarget(TopHUD.GetGoldRewardTarget());

        var travel = DungeonTravelManager.Get();
        GameCombatManager.Get().ConfigureDeparture(travel.GetDeparture(), travel.GetSupplies(), travel.ReturnToVillage);
        yield return GameCombatManager.Get().AssetsPreLoad();

        GameCombatManager.Get().SetUp();
        BottomHUD = UIManager.Get().CreateHUD(assets.LoadAsset<GameObject>(Prefabs.UI_HUD_Botttom.ToAddress()).GetComponent<UI_HUD_Bottom>());
        UIManager.Get().CreateHUD(assets.LoadAsset<GameObject>(Prefabs.UI_HUD_Supplies.ToAddress()).GetComponent<UI_HUD_Supplies>());
    }

    private void OnDestroy()
    {
        if (BottomHUD != null && GameManager.TryGetInstance(out _) == true)
        {
            UIManager.Get().ReleaseHUD(BottomHUD);
            BottomHUD = null;
        }

        if (TopHUD != null && GameManager.TryGetInstance(out _) == true)
        {
            UIManager.Get().ReleaseHUD(TopHUD);
            TopHUD = null;
        }
    }
}
