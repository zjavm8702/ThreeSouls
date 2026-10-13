using System;
using System.Collections.Concurrent;
using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using ThreeSouls.Persistence;
using ThreeSouls.UI;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace ThreeSouls.Ads
{
    public enum RewardPlacement { DungeonGold, VillagePotion }
    public interface IRewardedBackend : IDisposable
    {
        event Action Loaded,LoadFailed,Displayed,DisplayFailed,Rewarded,Closed;
        bool Ready {get;}
        bool Capped(string placement);
        void Load();void Show(string placement);
    }
    internal sealed class LevelPlayRewardedBackend : IRewardedBackend
    {
        private readonly LevelPlayRewardedAd Ad;
        private readonly ConcurrentQueue<Action> Queue;
        public event Action Loaded,LoadFailed,Displayed,DisplayFailed,Rewarded,Closed;
        public bool Ready=>Ad.IsAdReady();
        public bool Capped(string placement)
        {
#if UNITY_EDITOR
            return false;
#else
            return LevelPlayRewardedAd.IsPlacementCapped(placement);
#endif
        }
        public LevelPlayRewardedBackend(string id,ConcurrentQueue<Action> queue)
        {
            Queue=queue;Ad=new LevelPlayRewardedAd(id);
            Ad.OnAdLoaded+=OnLoaded;Ad.OnAdLoadFailed+=OnLoadFailed;Ad.OnAdDisplayed+=OnDisplayed;
            Ad.OnAdDisplayFailed+=OnDisplayFailed;Ad.OnAdRewarded+=OnRewarded;Ad.OnAdClosed+=OnClosed;Ad.OnAdImpressionDataReady+=OnImpression;
        }
        private void OnLoaded(LevelPlayAdInfo i)=>Queue.Enqueue(()=>Loaded?.Invoke());
        private void OnLoadFailed(LevelPlayAdError e)=>Queue.Enqueue(()=>LoadFailed?.Invoke());
        private void OnDisplayed(LevelPlayAdInfo i)=>Queue.Enqueue(()=>Displayed?.Invoke());
        private void OnDisplayFailed(LevelPlayAdInfo i,LevelPlayAdError e)=>Queue.Enqueue(()=>DisplayFailed?.Invoke());
        private void OnRewarded(LevelPlayAdInfo i,LevelPlayReward r)=>Queue.Enqueue(()=>Rewarded?.Invoke());
        private void OnClosed(LevelPlayAdInfo i)=>Queue.Enqueue(()=>Closed?.Invoke());
        private void OnImpression(LevelPlayImpressionData i)
        {
            if(i==null)return;
#if UNITY_EDITOR
            Queue.Enqueue(()=>LocalTelemetry.Record("ad_mock_impression"));
#else
            Queue.Enqueue(()=>LocalTelemetry.Record("ad_revenue",new {network=i.AdNetwork,revenue=i.Revenue}));
#endif
        }
        public void Load()=>Ad.LoadAd();public void Show(string placement)=>Ad.ShowAd(placement);
        public void Dispose(){Ad.OnAdLoaded-=OnLoaded;Ad.OnAdLoadFailed-=OnLoadFailed;Ad.OnAdDisplayed-=OnDisplayed;Ad.OnAdDisplayFailed-=OnDisplayFailed;Ad.OnAdRewarded-=OnRewarded;Ad.OnAdClosed-=OnClosed;Ad.OnAdImpressionDataReady-=OnImpression;Ad.Dispose();}
    }
    public sealed class RewardedAds : MonoBehaviour
    {
        private sealed class Unit
        {
            public IRewardedBackend Backend;public bool Loading,Requested;public double Retry;
            public string Profile,Receipt;public int Gold,Potions,Limit;public bool Closed,Granted,Earned;public double ClosedAt,RewardRetry;
        }
        public static RewardedAds Current {get;private set;}
        public static event Action Changed;
        public string Status {get;private set;}="광고 준비 중";
        public bool Busy=>Active!=null;
        public bool Available=>Initialized;
        private bool Initialized;
        private bool TestBackend;
        private GameServicesConfig Config;
        private Unit Result,Supply,Active;
        private GamePauseLease Pause;
        private readonly ConcurrentQueue<Action> Queue=new();
        internal static void Create(GameObject host){if(Current==null)Current=host.AddComponent<RewardedAds>();}
        private void Awake(){Current=this;Config=GameServicesConfig.Load();}
        private void Start()
        {
            var configured=Config!=null && Config.AdsConfigured;
#if UNITY_EDITOR
            if(UnityEditor.EditorPrefs.GetBool("ThreeSouls.Tests.MockAds",false))
            {Config=Instantiate(Config);Config.AndroidAppKey="editor";Config.ResultAdUnitId="editor-result";Config.SupplyAdUnitId="editor-supply";Config.AdsEnabled=true;configured=true;}
#endif
            if(!configured)return;
#if UNITY_ANDROID
            LevelPlay.OnInitSuccess+=OnInit;LevelPlay.OnInitFailed+=OnInitFailed;
            // No personalized-ad consent is inferred. Publisher may later integrate a CMP.
            LevelPlayPrivacySettings.SetGDPRConsent(false);LevelPlayPrivacySettings.SetCCPA(true);
            LevelPlayPrivacySettings.SetCOPPA(Config.ChildDirected);
            LevelPlay.Init(Config.AndroidAppKey);
#if UNITY_EDITOR
            SetupUnits(); // Editor mock may not dispatch initialization success.
#endif
#endif
        }
        private void OnInit(LevelPlayConfiguration c)=>Queue.Enqueue(SetupUnits);
        private void OnInitFailed(LevelPlayInitError e)=>Queue.Enqueue(()=>{Status="광고 연결 실패 · 잠시 후 다시 시도하세요";Changed?.Invoke();});
        private void SetupUnits()
        {
            if(Initialized)return;Result=Wire(new LevelPlayRewardedBackend(Config.ResultAdUnitId,Queue));Supply=Wire(new LevelPlayRewardedBackend(Config.SupplyAdUnitId,Queue));Initialized=true;Status="광고 준비 중";Changed?.Invoke();
        }
        private Unit Wire(IRewardedBackend backend)
        {
            var u=new Unit {Backend=backend};
            backend.Loaded+=()=>{u.Loading=false;Status="광고 시청 가능";Changed?.Invoke();};
            backend.LoadFailed+=()=>{u.Loading=false;u.Retry=Time.realtimeSinceStartupAsDouble+30;Status="광고를 불러오지 못했습니다";LocalTelemetry.Record("ad_load_failed");Changed?.Invoke();};
            backend.Displayed+=()=>LocalTelemetry.Record("ad_displayed");
            backend.DisplayFailed+=()=>{if(Active==u){Status="광고를 표시하지 못했습니다. 보상은 지급되지 않았습니다.";Finish(u);}LocalTelemetry.Record("ad_display_failed");};
            backend.Rewarded+=()=>Reward(u);
            backend.Closed+=()=>{if(Active!=u)return;u.Closed=true;u.ClosedAt=Time.realtimeSinceStartupAsDouble;Pause?.Dispose();Pause=null;if(u.Granted)Finish(u);else {Status=u.Earned?"보상 저장 재시도 중 · 저장 공간을 확인하세요":"시청 결과 확인 중";Changed?.Invoke();}};
            return u;
        }
        public int BonusGold(CombatResultData data)=>data==null?0:(int)Math.Floor(data.Gold*(double)(Config?.ResultGoldRatio ?? .5f));
        public int PotionReward=>Config?.SupplyPotions ?? 2;
        public int DailyLimit(RewardPlacement p)=>p==RewardPlacement.DungeonGold?Config?.ResultAdsPerDay ?? 3:Config?.SupplyAdsPerDay ?? 3;
        public void Prepare(RewardPlacement placement)
        {
            var u=Get(placement);if(u==null)return;u.Requested=true;
            if(!u.Loading && !u.Backend.Ready && Time.realtimeSinceStartupAsDouble>=u.Retry){u.Loading=true;u.Backend.Load();}
        }
        public bool CanShow(RewardPlacement placement,CombatResultData data,out string reason)
        {
            reason=Status;var s=PersistentSession.Current;var u=Get(placement);
            if(s==null || !s.WriteEnabled || s.CombatLocked || CloudSaveCoordinator.Current?.Busy==true || Busy || u==null)return false;
            if(!u.Backend.Ready){reason="광고 준비 중 · 잠시 후 다시 시도하세요";return false;}
            var result=placement==RewardPlacement.DungeonGold;var limit=result?Config.ResultAdsPerDay:Config.SupplyAdsPerDay;
            if(s.Snapshot.AdDay==DateTime.UtcNow.ToString("yyyy-MM-dd") && (result?s.Snapshot.ResultAds:s.Snapshot.SupplyAds)>=limit){reason="오늘의 광고 보상을 모두 받았습니다";return false;}
            if(u.Backend.Capped(result?Config.ResultPlacement:Config.SupplyPlacement)){reason="잠시 후 다시 시도하세요";return false;}
            if(result){var receipt="ad/gold/"+s.Snapshot.DeploymentId;if(data==null || BonusGold(data)<=0 || string.IsNullOrEmpty(s.Snapshot.DeploymentId)){reason="추가 골드 대상이 없습니다";return false;}if(s.Snapshot.RewardReceipts.Contains(receipt)){reason="추가 골드를 받았습니다";return false;}if((long)UserManager.Get().UData.Gold+BonusGold(data)>int.MaxValue){reason="보유 골드 한도를 확인하세요";return false;}}
            else if(UserManager.Get().UData.Campaign.ClearedProgress<1 || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Scene_Village" || !UserManager.Get().UData.Consumables.CanAdd(ConsumableType.SmallHealingPotion,PotionReward)){reason="첫 던전 클리어 후 · 물약 보유 공간이 필요합니다";return false;}
            reason="";return true;
        }
        public bool Show(RewardPlacement placement,CombatResultData data=null)
        {
            if(!CanShow(placement,data,out var reason)){Status=reason;Changed?.Invoke();return false;}
            var u=Get(placement);var s=PersistentSession.Current;
            if(!s.SaveNow())return false;
            u.Profile=s.Snapshot.ProfileId;u.Receipt=placement==RewardPlacement.DungeonGold?"ad/gold/"+s.Snapshot.DeploymentId:"ad/potion/"+Guid.NewGuid().ToString("N");
            u.Gold=placement==RewardPlacement.DungeonGold?BonusGold(data):0;u.Potions=placement==RewardPlacement.VillagePotion?PotionReward:0;
            u.Limit=placement==RewardPlacement.DungeonGold?Config.ResultAdsPerDay:Config.SupplyAdsPerDay;u.Granted=u.Closed=u.Earned=false;Active=u;Pause=GamePauseLease.Acquire();
            Status="광고 시청 중";LocalTelemetry.Record("ad_requested",new {placement=placement.ToString()});Changed?.Invoke();
            try{u.Backend.Show(placement==RewardPlacement.DungeonGold?Config.ResultPlacement:Config.SupplyPlacement);return true;}
            catch(Exception){Status="광고를 표시하지 못했습니다";Finish(u);return false;}
        }
        private void Reward(Unit u)
        {
            if(Active!=u || u.Granted)return;
            u.Earned=true;u.RewardRetry=Time.realtimeSinceStartupAsDouble+5;
            u.Granted=PersistentSession.Current.CommitAdReward(u.Profile,u.Receipt,u.Gold,u.Potions,u==Result,u.Limit);
            Status=u.Granted?$"보상 지급 완료 · {(u.Gold>0?"골드 +"+u.Gold:"소형 물약 +"+u.Potions)}":"보상 저장 실패 · 저장 공간 확보 후 재시도 중";
            LocalTelemetry.Record("ad_reward",new {granted=u.Granted,gold=u.Gold,potions=u.Potions});Changed?.Invoke();if(u.Closed && u.Granted)Finish(u);
        }
        private void Finish(Unit u)
        {
            if(Active!=u)return;Pause?.Dispose();Pause=null;Active=null;u.Requested=false;
            u.Backend.Dispose();
            // Retire the per-impression listeners so a late callback cannot reward a later request.
            if(u==Result)Result=TestBackend?null:Wire(new LevelPlayRewardedBackend(Config.ResultAdUnitId,Queue));
            else Supply=TestBackend?null:Wire(new LevelPlayRewardedBackend(Config.SupplyAdUnitId,Queue));
            Changed?.Invoke();
        }
        private Unit Get(RewardPlacement p)=>p==RewardPlacement.DungeonGold?Result:Supply;
        private void Update()
        {
            while(Queue.TryDequeue(out var action))action();
            if(Active!=null && Active.Earned && !Active.Granted && Time.realtimeSinceStartupAsDouble>=Active.RewardRetry)Reward(Active);
            if(Active!=null && Active.Closed && !Active.Earned && Time.realtimeSinceStartupAsDouble-Active.ClosedAt>15){Status="시청 완료가 확인되지 않아 보상이 지급되지 않았습니다";Finish(Active);}
            if(Active==null){if(Result?.Requested==true)Prepare(RewardPlacement.DungeonGold);if(Supply?.Requested==true)Prepare(RewardPlacement.VillagePotion);}
        }
        private void OnDestroy(){Pause?.Dispose();Result?.Backend.Dispose();Supply?.Backend.Dispose();LevelPlay.OnInitSuccess-=OnInit;LevelPlay.OnInitFailed-=OnInitFailed;if(Current==this)Current=null;}
#if UNITY_EDITOR
        public void ConfigureTestBackends(IRewardedBackend result,IRewardedBackend supply){TestBackend=true;Result?.Backend.Dispose();Supply?.Backend.Dispose();Result=Wire(result);Supply=Wire(supply);Initialized=true;}
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset(){Current=null;Changed=null;}
    }
}
