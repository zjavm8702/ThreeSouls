using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    public class BasePopupUI : BaseUI, IPopupElement
    {
        private RectTransform ContentsRectTransform;
        public RectTransform GetContentsRectTransform => ContentsRectTransform ?? (ContentsRectTransform = Contents.GetComponent<RectTransform>());

        [SerializeField] protected GameObject Root;
        [SerializeField] protected GameObject Contents;
        [SerializeField] protected GameObject Dim;

        public UIState CurrentUIState { get; protected set; } = UIState.HIDE;
        public override bool IsShowing => CurrentUIState == UIState.SHOW_START || CurrentUIState == UIState.SHOW;

        public override void Initialize()
        {
            BasicObjectState(false);
            CurrentUIState = UIState.HIDE;
        }

        public virtual void Show(bool playAnimation = true)
        {
            BasicObjectState(true);
            CurrentUIState = UIState.SHOW;
        }

        public virtual void Hide(bool playAnimation = true) 
        {
            CurrentUIState = UIState.HIDE_START;
            Exit();
        }

        protected virtual void Exit()
        {
            BasicObjectState(false);
            CurrentUIState = UIState.HIDE;
        }

        protected virtual void BasicObjectState(bool isActive)
        {
            Root?.SetActive(isActive);
            Contents?.SetActive(isActive);
            Dim?.SetActive(isActive);
        }

        public virtual void OnEscapeEvent()
        {
            Hide();
        }
    }
}
