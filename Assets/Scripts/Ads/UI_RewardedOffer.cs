using TMPro;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.Ads
{
    public sealed class UI_RewardedOffer : MonoBehaviour
    {
        [SerializeField] private RewardPlacement Placement;
        [SerializeField] private Button Watch;
        [SerializeField] private TMP_Text Label,Notice;
        [SerializeField] private GameObject Card;
        private CombatResultData Result;
        private bool Victory;
        private bool Prepared;
        private void Awake()=>Watch.onClick.AddListener(Request);
        public void BindResult(CombatResultData data,bool victory){Result=data;Victory=victory;Prepared=false;Refresh();}
        private void OnEnable(){Prepared=false;}
        private void Update()=>Refresh();
        private void Refresh()
        {
            var ads=RewardedAds.Current;if(ads==null || ThreeSouls.Persistence.PersistentSession.Current==null)return;
            var visible=Placement==RewardPlacement.DungeonGold?Victory:UserManager.Get().UData.Campaign.ClearedProgress>=1;
            Card.SetActive(visible);if(!visible)return;
            if(ads.Available && !Prepared){ads.Prepare(Placement);Prepared=true;}
            var amount=Placement==RewardPlacement.DungeonGold?ads.BonusGold(Result):ads.PotionReward;
            Label.text=Placement==RewardPlacement.DungeonGold?$"광고 보고 골드 +{amount:N0}":$"광고 보고 소형 물약 {amount}개";
            Watch.interactable=ads.CanShow(Placement,Result,out var reason);
            Notice.text=ads.Busy?ads.Status:Watch.interactable?$"선택 보상 · 시청 완료 시 지급 · 하루 {ads.DailyLimit(Placement)}회":string.IsNullOrEmpty(reason)?ads.Status:reason;
        }
        private void Request(){RewardedAds.Current?.Show(Placement,Result);Prepared=false;}
    }
}
