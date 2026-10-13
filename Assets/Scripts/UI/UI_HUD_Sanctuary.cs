using ThreeSouls.Manager;
using TMPro;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 현재 성소 버프와 무작위 디버프만 표시하고 변경 알림의 수명을 소유합니다.
    /// </summary>
    public sealed class UI_HUD_Sanctuary : MonoBehaviour
    {
        [SerializeField] private TMP_Text Status;
        private GameCombatManager Combat;

        public void SetUp(GameCombatManager combat)
        {
            Release();
            Combat = combat;
            Connect();
        }

        private void Connect()
        {
            Refresh();
            Combat.OnSanctuaryChangedEventHandler += Refresh;
        }

        private void Refresh()
        {
            Status.text = Combat.GetSanctuaryDescription();
        }

        public void Release()
        {
            if (Combat != null)
            {
                Combat.OnSanctuaryChangedEventHandler -= Refresh;
                Combat = null;
            }
        }

        private void OnDisable()
        {
            if (Combat != null)
            {
                Combat.OnSanctuaryChangedEventHandler -= Refresh;
            }
        }

        private void OnEnable()
        {
            if (Combat != null)
            {
                Connect();
            }
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
