using System;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    public enum LobbyTab
    {
        Village,
        Inventory,
        Characters,
        Supplies
    }

    /// <summary>
    /// 로비 탭의 활성 화면과 재화 표시를 관리합니다. 탭 추가는 연결 배열을 확장합니다.
    /// </summary>
    public sealed class UI_LobbyTabs : MonoBehaviour
    {
        public delegate void OnTabChangedDelegate(LobbyTab tab);
        private OnTabChangedDelegate onTabChangedEventHandler;
        public event OnTabChangedDelegate OnTabChangedEventHandler
        {
            add
            {
                onTabChangedEventHandler -= value;
                onTabChangedEventHandler += value;
            }
            remove
            {
                onTabChangedEventHandler -= value;
            }
        }
        [SerializeField] private Button[] TabButtons;
        [SerializeField] private GameObject[] Pages;
        [SerializeField] private Image[] Indicators;
        [SerializeField] private Text Gold;
        private UserManager User;

        private void Awake()
        {
            if (TabButtons.Length != Pages.Length || Indicators.Length != Pages.Length)
            {
                throw new InvalidOperationException("로비 탭과 화면 연결 수가 일치해야 합니다.");
            }

            for (var i = 0; i < TabButtons.Length; i++)
            {
                var tab = (LobbyTab)i;
                TabButtons[i].onClick.AddListener(() => SelectTab(tab));
            }

            SelectTab(ThreeSouls.Data.LobbyTables.GetRules().InitialTab);
        }

        public void SetUp(UserManager user)
        {
            Disconnect();
            User = user ?? throw new ArgumentNullException(nameof(user));

            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        public void SelectTab(LobbyTab tab)
        {
            var index = (int)tab;

            if (index < 0 || index >= Pages.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(tab));
            }

            for (var i = 0; i < Pages.Length; i++)
            {
                Pages[i].SetActive(i == index);
                Indicators[i].color = i == index ? new Color32(226, 183, 105, 255)
                    : new Color32(53, 66, 76, 255);
            }

            onTabChangedEventHandler?.Invoke(tab);
        }

        public void OpenDungeonMap()
        {
            SelectTab(LobbyTab.Village);
            GetComponentInChildren<UI_DungeonSelection>(true).Open();
        }

        public bool IsVillageShowing()
        {
            return Pages[(int)LobbyTab.Village].activeSelf;
        }

        public bool IsMoveInputAllowed()
        {
            var keys = ThreeSouls.Data.LobbyTables.GetVillageRules().MoveInputTabs;
            for (var i = 0; i < keys.Length; i++)
            {
                var tab = (LobbyTab)Enum.Parse(typeof(LobbyTab), keys[i]);
                if (Pages[(int)tab].activeSelf == true)
                {
                    return true;
                }
            }
            return false;
        }

        private void Connect()
        {
            ShowCurrency(User.UData.Gold, User.UData.ChestCount);
            User.OnCombatRewardChangedEventHandler += ShowCurrency;
        }

        private void Disconnect()
        {
            if (User != null)
            {
                User.OnCombatRewardChangedEventHandler -= ShowCurrency;
            }
        }

        private void ShowCurrency(int gold, int chestCount)
        {
            Gold.text = gold.ToString("N0");
        }

        private void OnEnable()
        {
            if (User != null)
            {
                Connect();
            }
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
