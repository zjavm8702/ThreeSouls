using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;

namespace ThreeSouls.Persistence
{
    public sealed class CloudRecord { public string Envelope,WriteLock; }
    public interface ICloudSaveBackend
    {
        Task<CloudRecord> LoadAsync();
        Task<string> SaveAsync(string envelope,string writeLock);
    }
    internal sealed class UnityCloudBackend : ICloudSaveBackend
    {
        private const string Key="three_souls_profile_v1";
        public async Task<CloudRecord> LoadAsync()
        {
            var data=await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>{Key});
            return data.TryGetValue(Key,out var item)?new CloudRecord {Envelope=item.Value.GetAs<string>(),WriteLock=item.WriteLock}:null;
        }
        public async Task<string> SaveAsync(string envelope,string writeLock)
        {
            var result=await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string,SaveItem>{{Key,new SaveItem(envelope,writeLock)}});
            return result[Key];
        }
    }
    public sealed class CloudSaveCoordinator : MonoBehaviour
    {
        public static CloudSaveCoordinator Current {get;private set;}
        public static event Action Changed;
        public string Status {get;private set;}="Google Play 연결 준비 중 · 로컬 저장은 사용 가능";
        public bool Busy {get;private set;}
        public bool Connected {get;private set;}
        public bool HasConflict {get;private set;}
        public GameSnapshot RemoteSnapshot {get;private set;}
        public string RemoteSummary=>RemoteSnapshot==null?"클라우드 기록 없음":$"진행 {RemoteSnapshot.ClearedProgress}/20 · 골드 {RemoteSnapshot.Gold:N0}";
        public bool CanConnect=>GameServicesConfig.Load()!=null && GameServicesConfig.Load().GoogleConfigured;
        private string OwnerId,WriteLock;
        private long LastUploadedRevision=-1;
        private string LastUploadedFingerprint;
        private bool Queued;
        private double NextUpload;
        private ICloudSaveBackend Backend;
        internal static void Create(GameObject host)
        { if(Current==null)Current=host.AddComponent<CloudSaveCoordinator>(); }
        private void Awake()
        {
            Current=this;Backend=new UnityCloudBackend();PersistentSession.Changed+=OnLocalSave;
        }
        public async Task ConnectGoogleAsync()
        {
            if(Busy || PersistentSession.Current?.CombatLocked==true || ThreeSouls.Ads.RewardedAds.Current?.Busy==true)return;
            if(!CanConnect){Status="Google Play 계정 연결은 준비 중입니다. 현재 기기 저장은 정상입니다.";Changed?.Invoke();return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            Busy=true;Status="Google Play에 연결하고 있습니다.";Changed?.Invoke();
            try
            {
                await UnityServices.InitializeAsync();
                GooglePlayGames.PlayGamesPlatform.Activate();
                var auth=new TaskCompletionSource<string>();
                // This is the explicit account-connect button: allow retry after auto sign-in failed.
                GooglePlayGames.PlayGamesPlatform.Instance.ManuallyAuthenticate(status=>{
                    if(status!=GooglePlayGames.BasicApi.SignInStatus.Success){auth.TrySetException(new InvalidOperationException("로그인이 취소되었습니다."));return;}
                    GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(true,code=>{
                        if(string.IsNullOrWhiteSpace(code))auth.TrySetException(new InvalidOperationException("로그인 인증을 완료하지 못했습니다."));else auth.TrySetResult(code);
                    });
                });
                if(await Task.WhenAny(auth.Task,Task.Delay(30000))!=auth.Task)throw new TimeoutException();
                // Tokens and OAuth client secrets are never logged or stored in the game profile.
                if(AuthenticationService.Instance.IsSignedIn)AuthenticationService.Instance.SignOut();
                await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(await auth.Task);
                OwnerId=AuthenticationService.Instance.PlayerId;Connected=true;
                await FetchCore();
            }
            catch(Exception){Status="계정에 연결하지 못했습니다. 로컬 저장으로 계속 플레이할 수 있습니다.";Connected=false;}
            finally {Busy=false;Changed?.Invoke();}
#else
            Status="Google Play 계정 연결은 Android 기기에서 사용할 수 있습니다.";Changed?.Invoke();await Task.CompletedTask;
#endif
        }
        private async void Start()
        {
            var local=PersistentSession.Current?.Snapshot;
            if(!CanConnect || local==null || string.IsNullOrEmpty(local.OwnerId))return;
            Busy=true;
            try
            {
                await UnityServices.InitializeAsync();
                if(!AuthenticationService.Instance.SessionTokenExists)return;
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if(AuthenticationService.Instance.PlayerInfo?.Identities==null || !AuthenticationService.Instance.PlayerInfo.Identities.Exists(x=>x.TypeId=="google-play-games") || AuthenticationService.Instance.PlayerId!=local.OwnerId)return;
                OwnerId=AuthenticationService.Instance.PlayerId;Connected=true;await FetchCore();
            }
            catch(Exception){Status="클라우드에 연결하지 못했습니다. 로컬 기록으로 계속합니다.";}
            finally{Busy=false;Changed?.Invoke();}
        }
        public async Task<bool> CheckCloudAsync()
        {
            if(Busy || !Connected)return false;Busy=true;Changed?.Invoke();
            try {await FetchCore();return true;}
            catch(Exception){RemoteSnapshot=null;HasConflict=true;Queued=false;Status="클라우드 기록을 확인하지 못했습니다. 로컬 기록은 유지됩니다.";return false;}
            finally {Busy=false;Changed?.Invoke();}
        }
        private async Task FetchCore()
        {
            var record=await Backend.LoadAsync();WriteLock=record?.WriteLock;
            RemoteSnapshot=record==null?null:GameSaveStore.Decode(record.Envelope);
            if(RemoteSnapshot!=null && RemoteSnapshot.OwnerId!=OwnerId)throw new InvalidOperationException("계정 기록 불일치");
            var local=PersistentSession.Current.Snapshot;
            var identical=RemoteSnapshot!=null && RemoteSnapshot.GameplayFingerprint()==local.GameplayFingerprint();
            HasConflict=RemoteSnapshot!=null && !identical && (local.ProfileId!=RemoteSnapshot.ProfileId || RemoteSnapshot.Revision!=local.CloudBaseRevision || local.OwnerId!=OwnerId)
                || !string.IsNullOrEmpty(local.OwnerId) && local.OwnerId!=OwnerId;
            if(HasConflict){Queued=false;Status="기기와 클라우드 기록이 다릅니다. 사용할 기록을 선택해 주세요.";}
            else
            {
                local.OwnerId=OwnerId;PersistentSession.Current.MarkDirty();Queued=true;
                if(identical){local.CloudBaseRevision=RemoteSnapshot.Revision;LastUploadedFingerprint=local.GameplayFingerprint();Queued=false;}
                Status="계정 연결 완료 · 클라우드 백업 사용 가능";
            }
        }
        public async Task<bool> UseLocalAndBackupAsync()
        {
            if(Busy || !Connected || PersistentSession.Current.CombatLocked)return false;
            Busy=true;Changed?.Invoke();
            try
            {
                var session=PersistentSession.Current;session.Snapshot.OwnerId=OwnerId;
                HasConflict=false;
                if(!session.SaveNow()) {Status=session.Status;return false;}
                var payload=GameSaveStore.Encode(session.Snapshot);var revision=session.Snapshot.Revision;var fingerprint=session.Snapshot.GameplayFingerprint();
                WriteLock=await Backend.SaveAsync(payload,WriteLock);LastUploadedRevision=revision;LastUploadedFingerprint=fingerprint;
                session.Snapshot.CloudBaseRevision=revision;var cursorSaved=session.SaveNow();Queued=session.Snapshot.GameplayFingerprint()!=fingerprint;
                RemoteSnapshot=GameSaveStore.Decode(payload);Status=cursorSaved?"클라우드 백업 완료":"클라우드 백업 완료 · 기기 저장 재시도 중";return true;
            }
            catch(CloudSaveConflictException){HasConflict=true;Queued=false;Status="다른 기기에서 기록이 변경됐습니다. 클라우드 기록을 다시 확인해 주세요.";return false;}
            catch(Exception){Status="클라우드 백업 실패 · 로컬 저장은 유지됩니다.";return false;}
            finally {Busy=false;NextUpload=Time.realtimeSinceStartupAsDouble+20;Changed?.Invoke();}
        }
        public bool UseCloudAndRestore()
        {
            if(Busy || !Connected || RemoteSnapshot==null || PersistentSession.Current.CombatLocked)return false;
            if(RemoteSnapshot.OwnerId!=OwnerId)return false;
            RemoteSnapshot.CloudBaseRevision=RemoteSnapshot.Revision;
            var result=PersistentSession.Current.ApplyRestoredSnapshot(RemoteSnapshot);
            if(result){HasConflict=false;LastUploadedRevision=RemoteSnapshot.Revision;LastUploadedFingerprint=PersistentSession.Current.Snapshot.GameplayFingerprint();Queued=false;Status="클라우드 기록 복구 완료";}
            Changed?.Invoke();return result;
        }
        private void OnLocalSave()
        {
            var s=PersistentSession.Current;
            if(Connected && s!=null && s.Snapshot.OwnerId!=OwnerId){HasConflict=true;Queued=false;Status="기기 기록이 변경됐습니다. 사용할 클라우드 기록을 확인해 주세요.";Changed?.Invoke();return;}
            if(Connected && !HasConflict && s!=null && !s.CombatLocked && s.Snapshot.OwnerId==OwnerId && s.Snapshot.GameplayFingerprint()!=LastUploadedFingerprint)Queued=true;
        }
        private async void Update()
        {
            if(!Queued || Busy || !Connected || HasConflict || PersistentSession.Current==null || PersistentSession.Current.CombatLocked || Time.realtimeSinceStartupAsDouble<NextUpload)return;
            await UseLocalAndBackupAsync();
        }
        private void OnDestroy(){PersistentSession.Changed-=OnLocalSave;if(Current==this)Current=null;}
#if UNITY_EDITOR
        public void ConfigureTestBackend(ICloudSaveBackend backend,string owner){Backend=backend;OwnerId=owner;Connected=true;Queued=false;NextUpload=double.MaxValue;}
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){Current=null;Changed=null;}
    }
}
