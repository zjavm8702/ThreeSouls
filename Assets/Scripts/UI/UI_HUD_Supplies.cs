using ThreeSouls.Data;
using ThreeSouls.Manager;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 전투 공급품의 남은 수량과 쿨타임을 표시하고 수동 사용을 요청합니다.
    /// </summary>
    public sealed class UI_HUD_Supplies : BaseUI
    {
        [SerializeField] private UnityEngine.UI.Button[] Buttons;
        [SerializeField] private UnityEngine.UI.Text[] Labels;
        [SerializeField] private UnityEngine.UI.Text Status;
        private CombatSupplyManager Supplies;
        private GameCombatManager Combat;

        public override bool IsShowing => isActiveAndEnabled;

        public override void Initialize()
        {
            Combat = GameCombatManager.Get();
            Supplies = DungeonTravelManager.Get().GetSupplies();
            Buttons[0].onClick.AddListener(UseFirst);
            Buttons[1].onClick.AddListener(UseSecond);
            Refresh();
        }

        public override void Release()
        {
            Buttons[0].onClick.RemoveListener(UseFirst);
            Buttons[1].onClick.RemoveListener(UseSecond);
            Combat = null;
            Supplies = null;
        }

        private void UseFirst()
        {
            Combat.TryUseSupply(0);
            Refresh();
        }

        private void UseSecond()
        {
            Combat.TryUseSupply(1);
            Refresh();
        }

        private void Update()
        {
            if (Supplies != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            for (var i = 0; i < Buttons.Length; i++)
            {
                var slot = Supplies.GetSlot(i);
                Labels[i].text = slot.Count == 0 ? "공급품 없음" : $"{ConsumableCatalog.GetName(slot.Type)}  ×{slot.Count}";
                Buttons[i].interactable = Combat.CanUseSupply(i);
            }
            var remaining = Combat.GetSupplyCooldownRemaining();
            Status.text = remaining > 0 ? $"회복 대기 {remaining:0.0}초" : $"자동 회복 · {ConsumableCatalog.GetThresholdLabel(Supplies.GetThreshold())}";
        }

        protected override void OnDisable()
        {
            Release();
            base.OnDisable();
        }
    }
}
