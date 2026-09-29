using System;
using System.Collections.Generic;
using ThreeSouls.Interface;
using ThreeSouls.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 공통 UI 루트와 BaseUIItem/BasePopupUI의 생성, 재사용, 해제를 관리합니다.
    /// 개별 UI의 데이터 바인딩과 표시 규칙은 각 기능별 담당 클래스가 소유합니다.
    /// </summary>
    public sealed class UIManager : MonoBehaviour, IManager
    {
        private readonly Dictionary<BaseUIItem, Stack<BaseUIItem>> UIItemPoolContainer = new();
        private readonly Dictionary<BaseUIItem, BaseUIItem> ActiveUIItemContainer = new();
        private readonly Dictionary<BasePopupUI, Stack<BasePopupUI>> PopupPoolContainer = new();
        private readonly Dictionary<BasePopupUI, BasePopupUI> ActivePopupContainer = new();

        private RectTransform UIRoot = null;

        public static UIManager Get()
        {
            return GameManager.Instance.Get<UIManager>();
        }

        public void Initialize()
        {
            
        }

        public void SetUIRoot(RectTransform rootRect)
        {
            UIRoot = rootRect;
        }

        public void Release()
        {
            ClearActiveUI();
            UIItemPoolContainer.Clear();
            PopupPoolContainer.Clear();

            if (UIRoot != null)
            {
                Destroy(UIRoot.gameObject);
                UIRoot = null;
            }
        }

        public void OnSceneChanged()
        {
            ClearActiveUI();
        }

        public T CreateUIItem<T>(T prefab, Transform parent = null) where T : BaseUIItem
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (UIItemPoolContainer.TryGetValue(prefab, out var pool) == false)
            {
                pool = new Stack<BaseUIItem>();
                UIItemPoolContainer.Add(prefab, pool);
            }

            BaseUIItem item = null;

            while (pool.Count > 0 && item == null)
            {
                item = pool.Pop();
            }

            item ??= Instantiate(prefab, UIRoot, false);
            item.transform.SetParent(parent != null ? parent : UIRoot, false);
            item.gameObject.SetActive(true);
            ActiveUIItemContainer.Add(item, prefab);
            return (T)item;
        }

        public void ReleaseUIItem(BaseUIItem item)
        {
            if (item == null)
            {
                return;
            }

            item.Clear();

            if (ActiveUIItemContainer.Remove(item, out var prefab) == false)
            {
                return;
            }

            item.gameObject.SetActive(false);

            if (UIRoot != null)
            {
                item.transform.SetParent(UIRoot, false);
            }

            UIItemPoolContainer[prefab].Push(item);
        }

        public T CreatePopup<T>(T prefab) where T : BasePopupUI
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (PopupPoolContainer.TryGetValue(prefab, out var pool) == false)
            {
                pool = new Stack<BasePopupUI>();
                PopupPoolContainer.Add(prefab, pool);
            }

            BasePopupUI popup = null;

            while (pool.Count > 0 && popup == null)
            {
                popup = pool.Pop();
            }

            popup ??= Instantiate(prefab, UIRoot, false);
            popup.transform.SetParent(UIRoot, false);
            popup.gameObject.SetActive(true);
            popup.Initialize();
            ActivePopupContainer.Add(popup, prefab);
            return (T)popup;
        }

        public void ReleasePopup(BasePopupUI popup)
        {
            if (popup == null || ActivePopupContainer.Remove(popup, out var prefab) == false)
            {
                return;
            }

            popup.Hide(false);
            popup.gameObject.SetActive(false);

            if (UIRoot != null)
            {
                popup.transform.SetParent(UIRoot, false);
            }

            PopupPoolContainer[prefab].Push(popup);
        }

        private void ClearActiveUI()
        {
            var items = new List<BaseUIItem>(ActiveUIItemContainer.Keys);

            for (var i = 0; i < items.Count; i++)
            {
                ReleaseUIItem(items[i]);
            }

            var popups = new List<BasePopupUI>(ActivePopupContainer.Keys);

            for (var i = 0; i < popups.Count; i++)
            {
                ReleasePopup(popups[i]);
            }
        }
    }
}
