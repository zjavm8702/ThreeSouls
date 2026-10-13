using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using ThreeSouls.UI;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 공통 UI 루트와 BaseUIItem/BasePopupUI의 생성, 재사용, 해제를 관리합니다.
    /// 개별 UI의 데이터 바인딩과 표시 규칙은 각 기능별 담당 클래스가 소유합니다.
    /// </summary>
    public sealed class UIManager : BaseManager, IManager, IEventListener<Event_SceneState>
    {
        public delegate void OnClearUIDelegate();

        private OnClearUIDelegate onClearUIEventHandler;
        public event OnClearUIDelegate OnClearUIEventHandler
        {
            add
            {
                onClearUIEventHandler -= value;
                onClearUIEventHandler += value;
            }
            remove
            {
                onClearUIEventHandler -= value;
            }
        }

        public int CallbackOrder => 0;
        private readonly Dictionary<BaseUIItem, Stack<BaseUIItem>> UIItemPoolContainer = new();
        private readonly Dictionary<BaseUIItem, BaseUIItem> ActiveUIItemContainer = new();
        private readonly Dictionary<BasePopupUI, Stack<BasePopupUI>> PopupPoolContainer = new();
        private readonly Dictionary<BasePopupUI, BasePopupUI> ActivePopupContainer = new();
        private readonly Dictionary<Type, BaseUI> ActiveHUDContainer = new();

        private GameObject CanvasGroup;
        private RectTransform UICanvas;
        private RectTransform ItemCanvas;
        private RectTransform PopupCanvas;

        public static UIManager Get()
        {
            return GameManager.Instance.Get<UIManager>();
        }

        public override void Initialize()
        {
            var groups = GameObject.FindGameObjectsWithTag("CanvasGroup");

            if (groups.Length != 1 || groups[0].transform.parent != null)
            {
                throw new InvalidOperationException("최상위 CanvasGroup 태그 오브젝트가 하나 있어야 합니다.");
            }

            var group = groups[0];
            UICanvas = GetRequiredCanvas(group.transform, "UICanvas");
            ItemCanvas = GetRequiredCanvas(group.transform, "ItemCanvas");
            PopupCanvas = GetRequiredCanvas(group.transform, "PopupCanvas");
            CanvasGroup = group;
            DontDestroyOnLoad(CanvasGroup);
            EventUtility.AddListener(this);
        }

        public RectTransform GetItemCanvas()
        {
            return ItemCanvas;
        }

        public RectTransform GetUICanvas()
        {
            return UICanvas;
        }

        public RectTransform GetPopupCanvas()
        {
            return PopupCanvas;
        }

        public override void Release()
        {
            ClearActiveUI();
            DestroyInactiveUI();
            UIItemPoolContainer.Clear();
            PopupPoolContainer.Clear();
            EventUtility.RemoveListener(this);
            onClearUIEventHandler = null;

            if (CanvasGroup != null)
            {
                Destroy(CanvasGroup);
            }

            CanvasGroup = null;
            UICanvas = null;
            ItemCanvas = null;
            PopupCanvas = null;
        }

        public override void OnSceneChanged()
        {
            ClearActiveUI();
            DestroyInactiveUI();
            UIItemPoolContainer.Clear();
            PopupPoolContainer.Clear();
        }

        public void OnEvent<TSender>(TSender sender, Event_SceneState eventData) where TSender : class
        {
            if (eventData.SceneState == SceneState.Load_SceneChangeEnter)
            {
                OnSceneChanged();
            }
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

            item ??= Instantiate(prefab, ItemCanvas, false);
            item.transform.SetParent(parent != null ? parent : ItemCanvas, false);
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

            if (ItemCanvas != null)
            {
                item.transform.SetParent(ItemCanvas, false);
            }

            UIItemPoolContainer[prefab].Push(item);
        }

        public T CreateHUD<T>(T prefab) where T : BaseUI
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (UICanvas == null)
            {
                throw new InvalidOperationException("UI root must be set before creating a HUD.");
            }

            var hudType = typeof(T);

            if (ActiveHUDContainer.TryGetValue(hudType, out var activeHUD) == true)
            {
                return (T)activeHUD;
            }

            var hud = Instantiate(prefab, UICanvas, false);

            hud.transform.SetAsLastSibling();
            
            hud.gameObject.SetActive(true);
            
            hud.Initialize();
            
            ActiveHUDContainer.Add(hudType, hud);
            
            return hud;
        }

        public void ReleaseHUD(BaseUI hud)
        {
            if (hud == null || ActiveHUDContainer.Remove(hud.GetType()) == false)
            {
                return;
            }

            hud.Release();
            Destroy(hud.gameObject);
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

            popup ??= Instantiate(prefab, PopupCanvas, false);
            popup.transform.SetParent(PopupCanvas, false);
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

            if (PopupCanvas != null)
            {
                popup.transform.SetParent(PopupCanvas, false);
            }

            PopupPoolContainer[prefab].Push(popup);
        }

        private void ClearActiveUI()
        {
            onClearUIEventHandler?.Invoke();
            var huds = new List<BaseUI>(ActiveHUDContainer.Values);

            for (var i = 0; i < huds.Count; i++)
            {
                ReleaseHUD(huds[i]);
            }

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

        private static RectTransform GetRequiredCanvas(Transform group, string canvasName)
        {
            var child = group.Find(canvasName);

            if (child == null || child.TryGetComponent<Canvas>(out _) == false || child is not RectTransform rect)
            {
                throw new InvalidOperationException($"CanvasGroup의 자식 Canvas 누락 : {canvasName}");
            }

            return rect;
        }

        private void DestroyInactiveUI()
        {
            using var itemPools = UIItemPoolContainer.Values.GetEnumerator();

            while (itemPools.MoveNext() == true)
            {
                var pool = itemPools.Current;

                while (pool.Count > 0)
                {
                    var item = pool.Pop();

                    if (item != null)
                    {
                        Destroy(item.gameObject);
                    }
                }
            }

            using var popupPools = PopupPoolContainer.Values.GetEnumerator();

            while (popupPools.MoveNext() == true)
            {
                var pool = popupPools.Current;

                while (pool.Count > 0)
                {
                    var popup = pool.Pop();

                    if (popup != null)
                    {
                        Destroy(popup.gameObject);
                    }
                }
            }
        }
    }
}
