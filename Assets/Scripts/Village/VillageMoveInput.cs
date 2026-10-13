using System;
using System.Collections.Generic;
using ThreeSouls.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 마을 탭의 클릭과 터치를 월드 이동 요청으로 바꾸며 UI 위의 입력을 제외합니다.
    /// </summary>
    public sealed class VillageMoveInput : MonoBehaviour
    {
        [SerializeField] private Camera WorldCamera;
        [SerializeField] private VillagePartyController Party;
        [SerializeField] private UI_LobbyTabs Tabs;
        [SerializeField] private VillageShopInteraction ShopInteraction;
        private EventSystem Events;
        private VillageNpcInteraction Npcs;
        private PointerEventData Pointer;
        private readonly List<RaycastResult> UIHits = new();

        public void Configure(UI_LobbyTabs tabs, VillageNpcInteraction npcs = null)
        {
            Tabs = tabs;
            Npcs = npcs;
        }

        private void Start()
        {
            if (WorldCamera == null || Party == null || Tabs == null || ShopInteraction == null || EventSystem.current == null)
            {
                throw new InvalidOperationException("마을 입력에는 카메라, 부대, 로비 탭과 EventSystem이 필요합니다.");
            }

            Events = EventSystem.current;
            Pointer = new PointerEventData(Events);
        }

        private void Update()
        {
            if (Tabs.IsMoveInputAllowed() == false || ShopInteraction.IsInputBlocked() == true || Npcs?.IsInputBlocked() == true)
            {
                return;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame == true)
            {
                var touch = Touchscreen.current.primaryTouch;
                RequestMove(touch.position.ReadValue(), touch.touchId.ReadValue());
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame == true)
            {
                RequestMove(Mouse.current.position.ReadValue(), -1);
            }
        }

        private void RequestMove(Vector2 screenPosition, int pointerId)
        {
            Pointer.Reset();
            Pointer.position = screenPosition;
            Pointer.pointerId = pointerId;
            UIHits.Clear();
            Events.RaycastAll(Pointer, UIHits);

            if (UIHits.Count > 0)
            {
                return;
            }

            var ray = WorldCamera.ScreenPointToRay(screenPosition);
            var floor = new Plane(Vector3.forward, Vector3.zero);

            if (floor.Raycast(ray, out var distance) == true)
            {
                var position = (Vector2)ray.GetPoint(distance);
                if (Npcs != null && Npcs.TryHandleWorldClick(position) == true)
                {
                    return;
                }

                if (ShopInteraction.TryHandleWorldClick(position) == false)
                {
                    Party.TryMoveTo(position);
                }
            }
        }
    }
}
