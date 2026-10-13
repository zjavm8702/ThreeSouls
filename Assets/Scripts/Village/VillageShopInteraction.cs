using System;
using ThreeSouls.Data;
using ThreeSouls.UI;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 상점 선택 후 부대의 입구 접근과 도착 시 화면 열기 및 취소를 연결합니다.
    /// </summary>
    public sealed class VillageShopInteraction : MonoBehaviour
    {
        [SerializeField] private VillagePartyController Party;
        [SerializeField] private VillageShopTarget Target;
        [SerializeField] private UI_LobbyTabs Tabs;
        [SerializeField] private UI_EquipmentShop Shop;
        [SerializeField] private UnityEngine.UI.Text Status;
        private bool PendingOpen;
        private bool Started;

        public void Configure(UI_LobbyTabs tabs, UI_EquipmentShop shop, UnityEngine.UI.Text status)
        {
            Tabs = tabs;
            Shop = shop;
            Status = status;
        }

        private void Start()
        {
            if (Party == null || Target == null || Tabs == null || Shop == null || Status == null)
            {
                throw new InvalidOperationException("마을 상점에는 부대/상점/탭/화면/안내를 연결해야 합니다.");
            }

            Started = true;
            Tabs.OnTabChangedEventHandler += OnTabChanged;
            ShowGuide();
        }

        public bool IsInputBlocked()
        {
            return Shop.IsShowing();
        }

        public void CancelPendingOpen()
        {
            if (PendingOpen == true)
            {
                Party.StopMovement();
            }

            PendingOpen = false;
            ShowGuide();
        }

        public bool TryHandleWorldClick(Vector2 position)
        {
            PendingOpen = false;
            ShowGuide();
            if (Target.ContainsPoint(position) == false)
            {
                return false;
            }

            if (!Tabs.IsVillageShowing() || IsInputBlocked()) return true;
            Party.StopMovement();
            Shop.Open();

            return true;
        }

        private void Update()
        {
            if (PendingOpen == false || Party.IsMoving() == true)
            {
                return;
            }

            PendingOpen = false;
            if (Tabs.IsVillageShowing() == true &&
                Vector2.Distance(Party.GetLeaderPosition(), Target.GetEntrancePosition()) <=
                EquipmentShopCatalog.GetVillageShop().InteractRadius)
            {
                ShowGuide();
                Shop.Open();
            }
            else
            {
                Status.text = "상점으로 이동이 취소되었습니다.";
            }
        }

        private void OnTabChanged(LobbyTab tab)
        {
            if (PendingOpen == true)
            {
                Party.StopMovement();
            }

            PendingOpen = false;
            ShowGuide();
        }

        private void ShowGuide()
        {
            Status.text = "바닥: 이동 · 상점 NPC: 거래 · 두루마리: 새 의뢰 · 책: 진행 중";
        }

        private void OnDisable()
        {
            PendingOpen = false;
            if (Tabs != null)
            {
                Tabs.OnTabChangedEventHandler -= OnTabChanged;
            }
        }

        private void OnEnable()
        {
            if (Started == true)
            {
                Tabs.OnTabChangedEventHandler += OnTabChanged;
                ShowGuide();
            }
        }

        private void OnDestroy()
        {
            if (Tabs != null)
            {
                Tabs.OnTabChangedEventHandler -= OnTabChanged;
            }
        }
    }
}
