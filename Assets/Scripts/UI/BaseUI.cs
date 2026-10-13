using UnityEngine;
using UnityEngine.EventSystems;
using ThreeSouls.Interface;

namespace ThreeSouls.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class BaseUI : UIBehaviour
    {
        public IUIAnimation EnterAnimation { get; set; }
        public IUIAnimation ExitAnimation { get; set; }
        public virtual bool IsShowing => false;

        public virtual void Initialize() { }

        public virtual void Release() { }

        private RectTransform rectTransform;
        public virtual RectTransform RectTransform
        {
            get
            {
                if (IsDestroyed() == true)
                {
                    return null;
                }

                return rectTransform ?? (rectTransform = GetComponent<RectTransform>());
            }
        }

        private CanvasGroup canvasGroup;
        public virtual CanvasGroup CanvasGroup
        {
            get
            {
                if (IsDestroyed() == true)
                {
                    return null;
                }

                return canvasGroup ?? (canvasGroup = GetComponent<CanvasGroup>());
            }
        }

        public virtual Transform Transform
        {
            get { return IsDestroyed() == true ? null : transform; }
        }

        public virtual float Alpha
        {
            get { return IsDestroyed() == false && gameObject != null ? CanvasGroup.alpha : 0f; }
            set
            {
                if (IsDestroyed() == false && gameObject != null)
                {
                    CanvasGroup.alpha = value;
                }
            }
        }

        public virtual bool Interactable
        {
            get { return IsDestroyed() == false && gameObject != null ? CanvasGroup.interactable : false; }
            set
            {
                if (IsDestroyed() == false && gameObject != null)
                {
                    CanvasGroup.interactable = value;
                }
            }
        }

        public virtual bool Visibility
        {
            get { return IsDestroyed() == false && gameObject != null ? gameObject.activeSelf : false; }
            set
            {
                if (IsDestroyed() || gameObject != null)
                {
                    return;
                }

                if (gameObject.activeSelf == value)
                {
                    return;
                }

                gameObject.SetActive(value);
            }
        }
    }
}
