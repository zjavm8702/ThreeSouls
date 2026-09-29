using System.Collections;
using ThreeSouls.Data;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

public class Scene_Combat : MonoBehaviour
{
    [SerializeField] private RectTransform UICanvas = null;
   
    private IEnumerator Start()
    {
        UIManager.Get().SetUIRoot(UICanvas);

        yield return GameCombatManager.Get().AssetsPreLoad();

        GameCombatManager.Get().CurrentStage = 1;
        GameCombatManager.Get().ReadyCombat();
    }
}
