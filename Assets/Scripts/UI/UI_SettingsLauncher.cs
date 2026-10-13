using ThreeSouls.Manager;
using UnityEngine;

namespace ThreeSouls.UI
{
    public sealed class UI_SettingsLauncher : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button OpenButton;
        [SerializeField] private UI_Settings Prefab;
        private UI_Settings Popup;
        private void Awake() => OpenButton.onClick.AddListener(OpenSettings);
        public void OpenSettings()
        {
            if (Popup != null && Popup.IsShowing) return;
            Popup = UIManager.Get().CreatePopup(Prefab);
            Popup.Show();
        }
        private void OnDisable()
        {
            if (Popup != null && Popup.IsShowing) UIManager.Get().ReleasePopup(Popup);
            Popup = null;
        }
        private void OnDestroy() => OpenButton.onClick.RemoveListener(OpenSettings);
    }
}
