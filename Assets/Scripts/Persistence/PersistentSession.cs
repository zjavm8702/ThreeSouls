using System;
using System.Linq;
using UnityEngine;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using ThreeSouls.UI;

namespace ThreeSouls.Persistence
{
    public sealed class PersistentSession : MonoBehaviour
    {
        public static PersistentSession Current { get; private set; }
        public static event Action Changed;
        public GameSnapshot Snapshot { get; private set; }
        public GameSaveStore Store { get; private set; }
        public string Status { get; private set; }
        public bool WriteEnabled { get; private set; }
        public bool CombatLocked { get; private set; }
        public bool LoadedExisting { get; private set; }
        private UserManager User;
        private GameSnapshot FreshTemplate;
        private bool Dirty,Restoring;
        private double RetryAt;
        private string LastSavedState;
        private static string SaveDirectory
        {
            get {
#if UNITY_EDITOR
                var test=UnityEditor.EditorPrefs.GetString("ThreeSouls.Tests.SaveDirectory","");if(!string.IsNullOrWhiteSpace(test))return test;
#endif
                return System.IO.Path.Combine(Application.persistentDataPath,"ThreeSouls");
            }
        }
        public static void StartSession(UserManager user)
        {
            if(Current!=null)return;
            Current=user.gameObject.AddComponent<PersistentSession>();Current.Initialize(user);
        }
        private void Initialize(UserManager user)
        {
            User=user;Store=new GameSaveStore(SaveDirectory);
            FreshTemplate=GameSaveStore.Decode(GameSaveStore.Encode(GameSnapshot.Capture(User)));
            var result=Store.Load();
            WriteEnabled=result.Status!=SaveLoadStatus.Damaged && result.Status!=SaveLoadStatus.Unsupported;
            Status=result.Detail;LoadedExisting=result.Snapshot!=null;
            if(result.Status==SaveLoadStatus.Loaded)LastSavedState=result.Snapshot.StorageFingerprint();
            if(LoadedExisting){Restoring=true;Snapshot=result.Snapshot;Snapshot.Restore(User);Restoring=false;}
            else Snapshot=GameSnapshot.Capture(User);
            if(Snapshot.WasInDungeon){Status="중단된 던전을 정리하고 출전 전 상태로 마을에 복귀했습니다.";Snapshot.WasInDungeon=false;}
            User.OnCombatRewardChangedEventHandler+=OnCurrency;
            User.OnEnhancementMaterialsChangedEventHandler+=OnMaterial;
            User.OnGrowthMaterialsChangedEventHandler+=OnMaterial;
            User.UData.Inventory.OnInventoryChangedEventHandler+=MarkDirty;
            User.UData.Equipment.OnEquipmentChangedEventHandler+=OnEquipment;
            User.GetHeroGrowthManager().OnHeroGrowthChangedEventHandler+=OnEquipment;
            User.GetRuneManager().OnRunesChangedEventHandler+=MarkDirty;
            User.GetQuestManager().OnQuestChangedEventHandler+=OnQuest;
            EquipmentGrowthManager.Get().OnEquipmentGrowthChangedEventHandler+=MarkDirty;
            LobbySupplyManager.Get().OnPreparationChangedEventHandler+=MarkDirty;
            GameSettings.Changed+=OnSettings;
            Dirty=WriteEnabled;
            LocalTelemetry.StartSession(LoadedExisting,Snapshot);
            CloudSaveCoordinator.Create(gameObject);
            ThreeSouls.Ads.RewardedAds.Create(gameObject);
            Changed?.Invoke();
        }
        private void OnCurrency(int gold,int chests){MarkDirty();LocalTelemetry.Economy(User.UData);}
        private void OnMaterial(int value){MarkDirty();LocalTelemetry.Economy(User.UData);}
        private void OnEquipment(string key)=>MarkDirty();
        private void OnQuest(){MarkDirty();if(!Restoring)LocalTelemetry.Record("quest_state",new {completed=User.UData.Quests.CompletedCount,accepted=User.UData.Quests.Accepted,objectiveComplete=User.UData.Quests.ObjectiveComplete});}
        private void OnSettings(){if(Restoring)return;Snapshot.Settings=SavedSettings.Capture();Dirty=true;}
        public void MarkDirty(){if(!Restoring && !CombatLocked)Dirty=true;}
        private void LateUpdate()
        {
            if(Dirty && WriteEnabled && Time.realtimeSinceStartupAsDouble>=RetryAt)SaveNow();
        }
        public bool SaveNow(bool force=false)
        {
            if(!WriteEnabled)return false;
            if(!CombatLocked)Snapshot=GameSnapshot.Capture(User,Snapshot);
            var state=Snapshot.StorageFingerprint();
            if(!force && state==LastSavedState && System.IO.File.Exists(Store.PrimaryPath)){Dirty=false;Changed?.Invoke();return true;}
            Snapshot.Revision++;Snapshot.SavedUtc=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(Store.Save(Snapshot,out var error)) {LastSavedState=state;Dirty=false;Status="로컬 저장 완료";Changed?.Invoke();return true;}
            Status=error;Dirty=true;RetryAt=Time.realtimeSinceStartupAsDouble+5;Changed?.Invoke();return false;
        }
        public bool BeginDeparture(out string error,int regionNumber=0,int stageNumber=0)
        {
            error=null;if(!WriteEnabled){error=Status;return false;}
            Snapshot=GameSnapshot.Capture(User,Snapshot);Snapshot.WasInDungeon=true;Snapshot.DeploymentId=Guid.NewGuid().ToString("N");
            CombatLocked=true;
            if(!SaveNow()){CombatLocked=false;error=Status;return false;}
            LocalTelemetry.Record("dungeon_enter",new {id=Snapshot.DeploymentId,RegionNumber=regionNumber,StageNumber=stageNumber});return true;
        }
        public void CancelDeparture(){CombatLocked=false;Snapshot.WasInDungeon=false;MarkDirty();}
        public void CompleteCombat(CombatResultData result,bool victory)
        {
            var receipt="battle/"+Snapshot.DeploymentId;
            if(!Snapshot.RewardReceipts.Contains(receipt))Snapshot.RewardReceipts=Snapshot.RewardReceipts.Append(receipt).TakeLast(2048).ToArray();
            CombatLocked=false;Snapshot.WasInDungeon=false;SaveNow();
            LocalTelemetry.Record(victory?"dungeon_win":"dungeon_loss",new {result.RegionNumber,result.StageNumber,result.Gold,result.ItemCount,result.ElapsedSeconds});
        }
        public void VillageReady(){if(CombatLocked)CancelDeparture();MarkDirty();SaveNow();}
        public bool ApplyRestoredSnapshot(GameSnapshot restored)
        {
            if(CombatLocked || ThreeSouls.Ads.RewardedAds.Current?.Busy==true || SceneLoadManager.Get().IsLoading)return false;
            var candidate=GameSaveStore.Decode(GameSaveStore.Encode(restored));candidate.WasInDungeon=false;
            candidate.Revision=Math.Max(candidate.Revision,Snapshot.Revision)+1;candidate.SavedUtc=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(!Store.Save(candidate,out var error)){Status=error;Changed?.Invoke();return false;}
            Restoring=true;
            try {candidate.Restore(User);Snapshot=candidate;LastSavedState=candidate.StorageFingerprint();WriteEnabled=true;LoadedExisting=true;Dirty=false;}
            finally {Restoring=false;}
            LocalTelemetry.Rebase(User.UData);LocalTelemetry.Record("profile_restore",new {progress=Snapshot.ClearedProgress});
            SceneLoadManager.Get().LoadScene(ThreeSouls.Enumeration.SceneName.Scene_Village);
            return true;
        }
        internal bool CommitAdReward(string profile,string receipt,int gold,int potions,bool resultAd,int limit)
        {
            if(!WriteEnabled || CombatLocked || Snapshot.ProfileId!=profile || Snapshot.RewardReceipts.Contains(receipt))return false;
            var candidate=GameSaveStore.Decode(GameSaveStore.Encode(GameSnapshot.Capture(User,Snapshot)));
            var day=DateTime.UtcNow.ToString("yyyy-MM-dd");if(candidate.AdDay!=day){candidate.AdDay=day;candidate.ResultAds=candidate.SupplyAds=0;}
            if((resultAd?candidate.ResultAds:candidate.SupplyAds)>=limit || gold<0 || potions<0 || (long)candidate.Gold+gold>int.MaxValue || (long)candidate.SmallPotions+potions>LobbyTables.GetSupplyRules().MaxOwnedPerType)return false;
            candidate.Gold+=gold;candidate.SmallPotions+=potions;if(resultAd)candidate.ResultAds++;else candidate.SupplyAds++;
            candidate.RewardReceipts=candidate.RewardReceipts.Append(receipt).TakeLast(2048).ToArray();candidate.Revision++;candidate.SavedUtc=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(!Store.Save(candidate,out var error)){Status=error;Changed?.Invoke();return false;}
            Snapshot=candidate;LastSavedState=candidate.StorageFingerprint();Restoring=true;
            try{if(gold>0)User.AddCombatReward(gold,0);if(potions>0)User.UData.Consumables.Add(ConsumableType.SmallHealingPotion,potions);}
            finally{Restoring=false;}
            Dirty=false;Status="광고 보상 저장 완료";Changed?.Invoke();return true;
        }
        public bool RestoreLocalBackup()
        {
            try {return ApplyRestoredSnapshot(GameSaveStore.Decode(System.IO.File.ReadAllText(Store.BackupPath)));}
            catch(Exception e)when(e is System.IO.IOException || e is Newtonsoft.Json.JsonException || e is InvalidOperationException || e is ArgumentException || e is NotSupportedException)
            {Status="복구 가능한 로컬 백업이 없습니다.";Changed?.Invoke();return false;}
        }
        public bool StartNewGame()
        {
            if(CombatLocked)return false;
            try {Store.ArchiveForNewGame();}
            catch(Exception e)when(e is System.IO.IOException || e is UnauthorizedAccessException){Status="기존 기록을 보존하지 못해 새 게임을 시작하지 않았습니다.";Changed?.Invoke();return false;}
            var fresh=GameSaveStore.Decode(GameSaveStore.Encode(FreshTemplate));fresh.ProfileId=Guid.NewGuid().ToString("N");
            return ApplyRestoredSnapshot(fresh);
        }
        private void OnApplicationPause(bool pause){if(pause && Dirty)SaveNow();LocalTelemetry.Record(pause?"app_background":"app_foreground",new {inDungeon=CombatLocked});}
        private void OnApplicationQuit(){if(Dirty)SaveNow();LocalTelemetry.Record("session_end",new {inDungeon=CombatLocked,progress=User.UData.Campaign.ClearedProgress});}
        private void OnDestroy()
        {
            GameSettings.Changed-=OnSettings;
            if(User!=null){User.OnCombatRewardChangedEventHandler-=OnCurrency;User.OnEnhancementMaterialsChangedEventHandler-=OnMaterial;User.OnGrowthMaterialsChangedEventHandler-=OnMaterial;
                User.UData.Inventory.OnInventoryChangedEventHandler-=MarkDirty;User.UData.Equipment.OnEquipmentChangedEventHandler-=OnEquipment;
                User.GetHeroGrowthManager().OnHeroGrowthChangedEventHandler-=OnEquipment;User.GetRuneManager().OnRunesChangedEventHandler-=MarkDirty;User.GetQuestManager().OnQuestChangedEventHandler-=OnQuest;}
            if(Current==this)Current=null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic(){Current=null;Changed=null;}
    }
}
