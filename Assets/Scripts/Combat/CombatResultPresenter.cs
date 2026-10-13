using System;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using ThreeSouls.Manager;
using ThreeSouls.UI;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 전투 결과 팝업의 생성, 닫기 요청과 씬 종료 시 반환을 연결합니다.
    /// </summary>
    internal sealed class CombatResultPresenter
    {
        private readonly UIManager UI;
        private readonly UI_Popup_Result Origin;
        private UI_Popup_Result Popup;
        internal delegate void OnReturnRequestedDelegate();
        private readonly OnReturnRequestedDelegate ReturnRequested;

        internal CombatResultPresenter(UIManager ui, UI_Popup_Result origin, OnReturnRequestedDelegate returnRequested = null)
        {
            UI = ui ?? throw new ArgumentNullException(nameof(ui));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            ReturnRequested = returnRequested;
            UI.OnClearUIEventHandler += ClosePopup;
        }

        internal void ShowResult(CombatResultData result, CombatOutcome outcome)
        {
            if (Popup != null)
            {
                throw new InvalidOperationException("현재 전투 결과창을 반환한 뒤 새 결과를 표시해야 합니다.");
            }

            Popup = UI.CreatePopup(Origin);
            Popup.OnConfirmRequestedEventHandler += Confirm;
            Popup.ShowResult(result, outcome);
        }

        internal void Release()
        {
            UI.OnClearUIEventHandler -= ClosePopup;
            ClosePopup();
        }

        private void ClosePopup()
        {
            if (Popup == null)
            {
                return;
            }

            Popup.OnConfirmRequestedEventHandler -= Confirm;
            UI.ReleasePopup(Popup);
            Popup = null;
        }

        private void Confirm()
        {
            ClosePopup();
            ReturnRequested?.Invoke();
        }
    }
}
