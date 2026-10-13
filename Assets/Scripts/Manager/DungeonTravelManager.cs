using System;
using ThreeSouls.Dungeon;
using ThreeSouls.Enumeration;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 로비 출전안의 수명과 공급품 예약 및 전투·마을 이동을 연결합니다.
    /// </summary>
    public sealed class DungeonTravelManager : BaseManager
    {
        private DungeonDeparturePlan Departure;
        private CombatSupplyManager Supplies;

        public static DungeonTravelManager Get()
        {
            return GameManager.Instance.Get<DungeonTravelManager>();
        }

        public DungeonDeparturePlan GetDeparture()
        {
            return Departure ?? throw new InvalidOperationException("마을에서 출전 준비를 먼저 확정하세요.");
        }

        public CombatSupplyManager GetSupplies()
        {
            return Supplies ?? throw new InvalidOperationException("출전 공급품을 먼저 예약하세요.");
        }

        public bool TryDepart(DungeonDeparturePlan plan, out string error)
        {
            var scenes = SceneLoadManager.Get();
            if (Departure != null || scenes.IsLoading == true || scenes.GetCurrentScene() != SceneName.Scene_Village)
            {
                error = "마을에서만 출전할 수 있습니다.";
                return false;
            }
            if (plan == null || plan.RegionNumber < 1 || plan.RegionNumber > 4 || plan.StageNumber < 1 || plan.StageNumber > 5 ||
                TableManager.Get().GetGroundTableData(plan.RegionNumber, plan.StageNumber) == null)
            {
                error = "선택한 스테이지의 전투 데이터를 확인할 수 없습니다.";
                return false;
            }
            var user = UserManager.Get();
            if (!ThreeSouls.Data.CampaignBalance.CanEnter(user, plan.RegionNumber, plan.StageNumber, out error))
            {
                return false;
            }
            var supplies = new CombatSupplyManager(user.UData.Consumables, user.UData.Supplies);
            var persistence=ThreeSouls.Persistence.PersistentSession.Current;
            if(persistence!=null && !persistence.BeginDeparture(out error,plan.RegionNumber,plan.StageNumber))return false;
            if (supplies.TryReserve(out error) == false)
            {
                persistence?.CancelDeparture();
                return false;
            }
            user.GetRuneManager().BeginCombatSession();
            Departure = plan;
            Supplies = supplies;
            scenes.LoadScene(SceneName.Scene_Dungeon);
            error = string.Empty;
            return true;
        }

        public void ReturnToVillage()
        {
            GetDeparture();
            SceneLoadManager.Get().LoadScene(SceneName.Scene_Village);
        }

        public override void Initialize() { }

        public override void OnSceneChanged()
        {
            if (Departure != null && SceneLoadManager.Get().GetCurrentScene() == SceneName.Scene_Dungeon)
            {
                EndDeparture();
            }
        }

        public override void Release()
        {
            EndDeparture();
        }

        private void EndDeparture()
        {
            Supplies?.ReturnUnused();
            Supplies = null;
            Departure = null;
        }
    }
}
