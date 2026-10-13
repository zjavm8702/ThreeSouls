using System;
using TMPro;
using ThreeSouls.Manager;
using ThreeSouls.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ThreeSouls.UI
{
    public sealed class UI_SaveAccount : BasePopupUI
    {
        [SerializeField] private TMP_Text LocalStatus;
        [SerializeField] private TMP_Text CloudStatus;
        [SerializeField] private UnityEngine.UI.Button Save,Backup,Restore,Connect,Upload,Download,NewGame,Close;
        [SerializeField] private GameObject ConfirmPanel;
        [SerializeField] private TMP_Text ConfirmText;
        [SerializeField] private UnityEngine.UI.Button Confirm,Cancel;
        private Action Pending;
        private bool Connected;
        private GamePauseLease Pause;
        public override void Initialize()
        {
            base.Initialize();ConfirmPanel.SetActive(false);
            if(Connected)return;
            Save.onClick.AddListener(SaveLocal);
            Backup.onClick.AddListener(SaveLocal);
            Restore.onClick.AddListener(AskRestore);
            Connect.onClick.AddListener(ConnectGoogle);
            Upload.onClick.AddListener(AskUpload);
            Download.onClick.AddListener(AskDownload);
            NewGame.onClick.AddListener(AskNewGame);
            Close.onClick.AddListener(ClosePopup);
            Confirm.onClick.AddListener(ConfirmAction);Cancel.onClick.AddListener(CancelAction);
            PersistentSession.Changed+=Refresh;CloudSaveCoordinator.Changed+=Refresh;Connected=true;
        }
        public override void Show(bool playAnimation=true)
        {
            if(IsShowing)return;Pause=GamePauseLease.Acquire();
            var overlay=GetComponent<Canvas>();overlay.overrideSorting=true;overlay.sortingOrder=120;
            transform.SetAsLastSibling();base.Show(false);Refresh();if(playAnimation)UIPanelMotion.Show(Contents);
        }
        private void Refresh()
        {
            var session=PersistentSession.Current;var cloud=CloudSaveCoordinator.Current;if(session==null)return;
            var adBusy=ThreeSouls.Ads.RewardedAds.Current?.Busy==true;
            LocalStatus.text=$"{session.Status}\n진행 {session.Snapshot.ClearedProgress}/20 · 골드 {session.Snapshot.Gold:N0}";
            CloudStatus.text=cloud?.Status ?? "현재 기기에 자동 저장됩니다.";
            Save.interactable=session.WriteEnabled;Backup.interactable=session.WriteEnabled;
            Restore.interactable=!session.CombatLocked && !adBusy && System.IO.File.Exists(session.Store.BackupPath);
            Connect.interactable=cloud!=null && cloud.CanConnect && !cloud.Busy && !session.CombatLocked;
            Upload.interactable=cloud!=null && cloud.Connected && !cloud.Busy && !session.CombatLocked && session.WriteEnabled;
            Download.interactable=cloud!=null && cloud.Connected && cloud.RemoteSnapshot!=null && !cloud.Busy && !session.CombatLocked;
            NewGame.interactable=!session.CombatLocked && !adBusy && !(cloud?.Busy ?? false);
        }
        private void SaveLocal(){PersistentSession.Current.SaveNow(true);Refresh();}
        private async void ConnectGoogle(){await CloudSaveCoordinator.Current.ConnectGoogleAsync();Refresh();}
        private void Ask(string text,Action action){Pending=action;ConfirmText.text=text;ConfirmPanel.SetActive(true);}
        private void AskRestore()=>Ask("직전 정상 백업으로 복구합니다.\n현재 기록은 백업으로 남고 마을로 돌아갑니다.",()=>PersistentSession.Current.RestoreLocalBackup());
        private async void AskDownload()
        { var cloud=CloudSaveCoordinator.Current;if(!await cloud.CheckCloudAsync() || !IsShowing || cloud.RemoteSnapshot==null)return;Ask($"클라우드 기록을 사용하시겠습니까?\n{cloud.RemoteSummary}\n현재 기기 기록은 백업으로 남습니다.",()=>cloud.UseCloudAndRestore()); }
        private void AskUpload()
        { Ask("현재 기기 기록을 클라우드에 백업합니다.\n클라우드의 기존 진행 기록을 바꿀 수 있습니다.",UploadCloud); }
        private async void UploadCloud(){await CloudSaveCoordinator.Current.UseLocalAndBackupAsync();Refresh();}
        private void AskNewGame()=>Ask("영웅·장비·의뢰·진행을 초기화하고 새 게임을 시작합니다.\n이전 기록은 백업으로 보관합니다.",()=>PersistentSession.Current.StartNewGame());
        private void ConfirmAction(){var action=Pending;Pending=null;ConfirmPanel.SetActive(false);action?.Invoke();Refresh();}
        private void CancelAction(){Pending=null;ConfirmPanel.SetActive(false);}
        public void ClosePopup(){CancelAction();UIManager.Get().ReleasePopup(this);}
        public override void OnEscapeEvent(){if(ConfirmPanel.activeSelf)CancelAction();else ClosePopup();}
        private void Update(){if(IsShowing && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)OnEscapeEvent();}
        public override void Hide(bool playAnimation=true){Pause?.Dispose();Pause=null;base.Hide(false);}
        protected override void OnDisable(){Pause?.Dispose();Pause=null;base.OnDisable();}
        protected override void OnDestroy(){PersistentSession.Changed-=Refresh;CloudSaveCoordinator.Changed-=Refresh;base.OnDestroy();}
    }
}
