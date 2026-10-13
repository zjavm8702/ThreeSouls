using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using ThreeSouls.Manager;

namespace ThreeSouls.UI
{
    public sealed class UI_Settings : BasePopupUI
    {
        [SerializeField] private UnityEngine.UI.Slider Master;
        [SerializeField] private UnityEngine.UI.Slider Music;
        [SerializeField] private UnityEngine.UI.Slider Effects;
        [SerializeField] private TMP_Text[] Percentages;
        [SerializeField] private UnityEngine.UI.Toggle Mute;
        [SerializeField] private UnityEngine.UI.Toggle Damage;
        [SerializeField] private UnityEngine.UI.Toggle Loot;
        [SerializeField] private UnityEngine.UI.Button Close;
        [SerializeField] private UnityEngine.UI.Button Defaults;
        [SerializeField] private UnityEngine.UI.Button Storage;
        [SerializeField] private UI_SaveAccount StoragePrefab;
        private UI_SaveAccount StoragePopup;
        private GamePauseLease PauseLease;
        private bool Connected;

        public override void Initialize()
        {
            base.Initialize();
            if (Connected) return;
            Master.onValueChanged.AddListener(GameSettings.SetMaster);
            Music.onValueChanged.AddListener(GameSettings.SetMusic);
            Effects.onValueChanged.AddListener(GameSettings.SetEffects);
            Mute.onValueChanged.AddListener(GameSettings.SetMuted);
            Damage.onValueChanged.AddListener(GameSettings.SetDamage);
            Loot.onValueChanged.AddListener(GameSettings.SetLoot);
            Close.onClick.AddListener(CloseSettings);
            Defaults.onClick.AddListener(GameSettings.RestoreDefaults);
            Storage?.onClick.AddListener(OpenStorage);
            GameSettings.Changed += Refresh;
            Connected = true;
        }
        public override void Show(bool playAnimation = true)
        {
            if (IsShowing) return;
            PauseLease=GamePauseLease.Acquire();
            var overlay = GetComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 100;
            Refresh();
            transform.SetAsLastSibling();
            base.Show(false);
            if (playAnimation) UIPanelMotion.Show(Contents);
        }
        private void Refresh()
        {
            Master.SetValueWithoutNotify(GameSettings.MasterVolume);
            Music.SetValueWithoutNotify(GameSettings.MusicVolume);
            Effects.SetValueWithoutNotify(GameSettings.EffectsVolume);
            Mute.SetIsOnWithoutNotify(GameSettings.Muted);
            Damage.SetIsOnWithoutNotify(GameSettings.ShowDamage);
            Loot.SetIsOnWithoutNotify(GameSettings.ShowLoot);
            Percentages[0].text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100)}%";
            Percentages[1].text = $"{Mathf.RoundToInt(GameSettings.MusicVolume * 100)}%";
            Percentages[2].text = $"{Mathf.RoundToInt(GameSettings.EffectsVolume * 100)}%";
        }
        private void Update()
        {
            if (IsShowing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseSettings();
        }
        public void CloseSettings() => UIManager.Get().ReleasePopup(this);
        private void OpenStorage()
        {
            if(StoragePrefab==null || StoragePopup!=null && StoragePopup.IsShowing)return;
            StoragePopup=UIManager.Get().CreatePopup(StoragePrefab);StoragePopup.Show();
        }
        public override void OnEscapeEvent() => CloseSettings();
        public override void Hide(bool playAnimation = true)
        {
            RestoreTime();
            base.Hide(false);
        }
        private void RestoreTime()
        {
            PauseLease?.Dispose();PauseLease=null;
        }
        protected override void OnDisable() { RestoreTime(); base.OnDisable(); }
        public override void Release()
        {
            RestoreTime();
            if (!Connected) return;
            GameSettings.Changed -= Refresh;
            Master.onValueChanged.RemoveListener(GameSettings.SetMaster);
            Music.onValueChanged.RemoveListener(GameSettings.SetMusic);
            Effects.onValueChanged.RemoveListener(GameSettings.SetEffects);
            Mute.onValueChanged.RemoveListener(GameSettings.SetMuted);
            Damage.onValueChanged.RemoveListener(GameSettings.SetDamage);
            Loot.onValueChanged.RemoveListener(GameSettings.SetLoot);
            Close.onClick.RemoveListener(CloseSettings);
            Defaults.onClick.RemoveListener(GameSettings.RestoreDefaults);
            Storage?.onClick.RemoveListener(OpenStorage);
            Connected = false;
        }
        protected override void OnDestroy() { Release(); base.OnDestroy(); }
    }
}
