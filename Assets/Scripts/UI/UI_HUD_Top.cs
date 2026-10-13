using ThreeSouls.Manager;
using ThreeSouls.Data;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 전투 경과 시간과 사용자 재화만 표시합니다.
    /// </summary>
    public sealed class UI_HUD_Top : BaseUI, IEventListener<Event_CombatStageCompleted>
    {
        public int CallbackOrder => 0;

        [SerializeField] private Text TimeText = null;
        [SerializeField] private Text GoldText = null;
        [SerializeField] private RectTransform GoldTarget = null;
        private UserManager User;

        private int LastDisplayedSecond = -1;
        private bool IsInitialized;

        public override bool IsShowing => isActiveAndEnabled;

        public override void Initialize()
        {
            if (IsInitialized == true)
            {
                return;
            }

            LastDisplayedSecond = -1;
            User = UserManager.Get();
            var userData = User.UData;
            SetTime(0);
            SetGold(userData.Gold);
            User.OnCombatRewardChangedEventHandler += OnCombatRewardChanged;
            EventUtility.AddListener<Event_CombatStageCompleted>(this);

            IsInitialized = true;
        }

        public override void Release()
        {
            if (IsInitialized == false)
            {
                return;
            }

            if (User != null)
            {
                User.OnCombatRewardChangedEventHandler -= OnCombatRewardChanged;
                User = null;
            }

            EventUtility.RemoveListener<Event_CombatStageCompleted>(this);
            IsInitialized = false;
        }

        public void OnEvent<TSender>(TSender sender, Event_CombatStageCompleted eventData) where TSender : class
        {
            OnElapsedTimeChanged(GameCombatManager.Get().ElapsedCombatSeconds);
        }

        private void Update()
        {
            if(IsInitialized)OnElapsedTimeChanged(GameCombatManager.Get().ElapsedCombatSeconds);
        }

        public RectTransform GetGoldRewardTarget()
        {
            return GoldTarget;
        }

        private void OnCombatRewardChanged(int gold, int chestCount)
        {
            SetGold(gold);
        }

        protected override void OnDisable()
        {
            Release();
            base.OnDisable();
        }

        private void OnElapsedTimeChanged(double elapsedSeconds)
        {
            var totalSeconds = Mathf.FloorToInt((float)elapsedSeconds);

            if (totalSeconds == LastDisplayedSecond)
            {
                return;
            }

            LastDisplayedSecond = totalSeconds;
            SetTime(totalSeconds);
        }

        private void SetTime(int totalSeconds)
        {
            if (TimeText != null)
            {
                TimeText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }
        }

        private void SetGold(int value)
        {
            if (GoldText != null)
            {
                GoldText.text = value.ToString("N0");
            }
        }
    }
}
