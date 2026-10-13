using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThreeSouls.Ads;
using ThreeSouls.Data;
using ThreeSouls.Manager;
using ThreeSouls.Persistence;
using ThreeSouls.UI;
using UnityEditor;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    public static class PersistenceLaunchChecks
    {
        private sealed class Cloud : ICloudSaveBackend
        {
            public CloudRecord Record;public int Writes;public bool Offline,Conflict;
            public Task<CloudRecord> LoadAsync(){if(Offline)throw new IOException();return Task.FromResult(Record);}
            public Task<string> SaveAsync(string value,string writeLock)
            {
                if(Offline)throw new IOException();
                if(Conflict || Record!=null && Record.WriteLock!=writeLock)
                {
                    var ctor=typeof(Unity.Services.CloudSave.CloudSaveConflictException).GetConstructors(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Single();
                    var args=ctor.GetParameters().Select(p=>p.ParameterType==typeof(string)?(object)"fixture conflict":p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null).ToArray();
                    throw (Exception)ctor.Invoke(args);
                }
                Writes++;Record=new CloudRecord{Envelope=value,WriteLock=Writes.ToString()};return Task.FromResult(Record.WriteLock);
            }
        }
        private sealed class Ad : IRewardedBackend
        {
            public event Action Loaded,LoadFailed,Displayed,DisplayFailed,Rewarded,Closed;
            public bool Ready=>true;public bool Capped(string p)=>false;public void Load()=>Loaded?.Invoke();public void Show(string p)=>Displayed?.Invoke();public void Dispose(){}
            public void Grant()=>Rewarded?.Invoke();public void Close()=>Closed?.Invoke();public void Fail()=>DisplayFailed?.Invoke();public void LoadFail()=>LoadFailed?.Invoke();
        }
        public static async void Run()
        {
            var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
            try
            {
                var s=PersistentSession.Current;var user=UserManager.Get();var original=GameSaveStore.Decode(GameSaveStore.Encode(GameSnapshot.Capture(user,s.Snapshot)));
                if(!Path.GetFullPath(s.Store.PrimaryPath).StartsWith(Path.GetFullPath("CodexBackups/PersistenceLaunch_20261012/"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Verification requires isolated backup-directory profile");
                Check(s.WriteEnabled,"Writable isolated test profile");
                var temp=Path.Combine(Path.GetDirectoryName(s.Store.PrimaryPath),"save.tmp.json");var committed=s.Store.Load().Snapshot;var pending=GameSaveStore.Decode(GameSaveStore.Encode(committed));pending.Revision+=10;pending.Gold+=1;File.WriteAllText(temp,GameSaveStore.Encode(pending));
                Check(s.Store.Load().Snapshot.Gold==committed.Gold,"Valid committed primary takes precedence over uncommitted temporary file");
                using(var file=new FileStream(temp,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(!s.ApplyRestoredSnapshot(pending) && user.UData.Gold==committed.Gold,"Failed restore write never mutates live gameplay");
                var a=GamePauseLease.Acquire();var b=GamePauseLease.Acquire();a.Dispose();Check(Time.timeScale==0,"Nested pause survives parent close first");b.Dispose();Check(Time.timeScale==1,"Last pause owner restores prior scale");
                var cloud=CloudSaveCoordinator.Current;var backend=new Cloud();cloud.ConfigureTestBackend(backend,"test-owner");
                await cloud.CheckCloudAsync();Check(!cloud.HasConflict,"Empty account cloud can bind local profile");
                Check(await cloud.UseLocalAndBackupAsync() && backend.Writes==1,"Cloud upload persists profile and returns optimistic lock");
                Check(GameSaveStore.Decode(backend.Record.Envelope).OwnerId=="test-owner","Cloud envelope carries account owner");
                Check(s.Snapshot.CloudBaseRevision==GameSaveStore.Decode(backend.Record.Envelope).Revision,"Cloud revision cursor records uploaded revision");
                await cloud.CheckCloudAsync();Check(!cloud.HasConflict,"Metadata-only local revisions do not create false conflict");
                var remote=GameSaveStore.Decode(backend.Record.Envelope);remote.Gold+=10;remote.Revision+=5;backend.Record.Envelope=GameSaveStore.Encode(remote);backend.Record.WriteLock="foreign-write";
                await cloud.CheckCloudAsync();Check(cloud.HasConflict && user.UData.Gold!=remote.Gold,"Divergent second-device record awaits explicit choice");
                backend.Offline=true;var before=user.UData.Gold;Check(!await cloud.UseLocalAndBackupAsync() && user.UData.Gold==before,"Offline cloud upload preserves local gameplay");backend.Offline=false;
                await cloud.CheckCloudAsync();backend.Conflict=true;Check(!await cloud.UseLocalAndBackupAsync() && cloud.HasConflict,"Optimistic write conflict blocks overwrite");backend.Conflict=false;
                remote.OwnerId="another-owner";backend.Record.Envelope=GameSaveStore.Encode(remote);await cloud.CheckCloudAsync();Check(user.UData.Gold==before,"Foreign account record never restores automatically");
                Check(cloud.RemoteSnapshot==null && cloud.HasConflict,"Invalid cloud refresh clears stale restore candidates");
                cloud.ConfigureTestBackend(new Cloud(),"test-owner");
                var fixture=GameSaveStore.Decode(GameSaveStore.Encode(original));fixture.ClearedProgress=1;fixture.Clears[0]=1;fixture.OwnerId="test-owner";fixture.DeploymentId=Guid.NewGuid().ToString("N");fixture.AdDay="";fixture.ResultAds=fixture.SupplyAds=0;
                fixture.Restore(user);s.Snapshot.DeploymentId=fixture.DeploymentId;s.Snapshot.RewardReceipts=new[]{"battle/"+fixture.DeploymentId};s.Snapshot.ResultAds=s.Snapshot.SupplyAds=0;s.Snapshot.AdDay="";s.SaveNow();
                var ads=RewardedAds.Current;Ad ad=null,supplyAd=null;void Backend(){ad=new Ad();supplyAd=new Ad();ads.ConfigureTestBackends(ad,supplyAd);}
                var result=(CombatResultData)Activator.CreateInstance(typeof(CombatResultData),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{1,1,100,30f,new Dictionary<string,int>(),0,0,0,""},null);
                Backend();before=user.UData.Gold;Check(ads.Show(RewardPlacement.DungeonGold,result) && Time.timeScale==0,"Result reward ad requires explicit show and pauses game");
                ad.Grant();ad.Grant();Check(user.UData.Gold==before+50,"Duplicate reward callbacks grant gold only once");
                Check(s.Store.Load().Snapshot.Gold==before+50 && s.Store.Load().Snapshot.ResultAds==1,"Reward and daily receipt committed together before presentation");
                ad.Close();Check(!ads.Busy && Time.timeScale==1,"Ad close restores pause ownership");Backend();Check(!ads.Show(RewardPlacement.DungeonGold,result),"Same dungeon result cannot claim a second ad reward");
                s.Snapshot.DeploymentId=Guid.NewGuid().ToString("N");s.SaveNow();Backend();before=user.UData.Gold;Check(ads.Show(RewardPlacement.DungeonGold,result),"Another result can request a new reward");ad.Fail();Check(user.UData.Gold==before && !ads.Busy && Time.timeScale==1,"Display failure never grants reward or leaves game paused");
                Backend();Check(ads.Show(RewardPlacement.DungeonGold,result),"Failed show does not consume daily allowance");ad.Close();Check(ads.Busy && Time.timeScale==1,"Close-before-reward retains the original reward context");ad.Grant();Check(user.UData.Gold==before+50 && !ads.Busy,"Delayed reward callback after close grants original result once");
                var old=ad;Backend();s.Snapshot.DeploymentId=Guid.NewGuid().ToString("N");s.SaveNow();Check(ads.Show(RewardPlacement.DungeonGold,result),"Third daily result reward requested");before=user.UData.Gold;old.Grant();Check(user.UData.Gold==before,"Retired impression callback cannot reward a later ad");ad.Grant();ad.Close();Backend();s.Snapshot.DeploymentId=Guid.NewGuid().ToString("N");s.SaveNow();Check(!ads.Show(RewardPlacement.DungeonGold,result),"Daily result reward limit persists");
                before=user.UData.Consumables.GetOwned(ConsumableType.SmallHealingPotion);Backend();Check(ads.Show(RewardPlacement.VillagePotion),"Village supply reward available after first clear");supplyAd.Grant();supplyAd.Close();Check(user.UData.Consumables.GetOwned(ConsumableType.SmallHealingPotion)==before+2,"Supply ad grants exactly two small potions");
                var profile=s.Snapshot.ProfileId;Backend();Check(ads.Show(RewardPlacement.VillagePotion),"Second supply ad requested");s.Snapshot.ProfileId=Guid.NewGuid().ToString("N");before=user.UData.Consumables.GetOwned(ConsumableType.SmallHealingPotion);supplyAd.Grant();Check(user.UData.Consumables.GetOwned(ConsumableType.SmallHealingPotion)==before && ads.Status.Contains("실패"),"Reward cannot cross into another profile");s.Snapshot.ProfileId=profile;supplyAd.Fail();
                Backend();before=user.UData.Consumables.GetOwned(ConsumableType.SmallHealingPotion);Check(ads.Show(RewardPlacement.VillagePotion),"Reward persistence retry fixture requested");
                using(var file=new FileStream(temp,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){supplyAd.Grant();supplyAd.Close();Check(ads.Busy && user.UData.Consumables.GetOwned(ConsumableType.SmallHealingPotion)==before,"Earned reward write failure preserves receipt context without granting in-memory-only items");}
                await Task.Delay(6000);Check(!ads.Busy && user.UData.Consumables.GetOwned(ConsumableType.SmallHealingPotion)==before+2 && s.Store.Load().Snapshot.SmallPotions==before+2,"Earned reward automatically retries durable commit after storage becomes available");
                LocalTelemetry.Record("verification_marker");var report=LocalMetricsReport.Summarize(LocalTelemetry.ExportPath);Check(report.Contains("ad_reward") && report.Contains("economy_change"),"Local metrics summarize economy and ad funnel");File.WriteAllText("CodexBackups/PersistenceLaunch_20261012/MetricsSummary.json",report);
                original.Restore(user);s.Snapshot.ProfileId=original.ProfileId;s.Snapshot.OwnerId=original.OwnerId;s.Snapshot.RewardReceipts=original.RewardReceipts;s.Snapshot.AdDay=original.AdDay;s.Snapshot.ResultAds=original.ResultAds;s.Snapshot.SupplyAds=original.SupplyAds;s.SaveNow();
                File.WriteAllLines("CodexBackups/PersistenceLaunch_20261012/IntegrationChecks.txt",checks);
                Debug.Log("Persistence integration checks passed: "+checks.Count);
            }
            catch(Exception e){File.WriteAllText("CodexBackups/PersistenceLaunch_20261012/IntegrationFailure.txt",e.ToString());Debug.LogError(e);}
        }
    }
}
