using ThreeSouls.UI;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 정식 마을 월드와 실행 중 생성되는 로비 UI 및 미니맵을 연결합니다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class VillageWorldPresentation : MonoBehaviour
    {
        [SerializeField] private GameObject World;
        [SerializeField] private VillagePartyController Party;
        [SerializeField] private VillageMoveInput Input;
        [SerializeField] private VillageShopInteraction ShopInteraction;
        [SerializeField] private UI_VillageMinimap MinimapPrefab;
        [SerializeField] private UI_RuneShop RuneShopPrefab;
        [SerializeField] private UI_StoryQuest StoryPrefab;
        [SerializeField] private VillageNpcInteraction NpcInteraction;

        private void Awake()
        {
            World.SetActive(false);
        }

        public void Disconnect()
        {
            World.SetActive(false);
        }

        public VillageShopInteraction GetShopInteraction()
        {
            return ShopInteraction;
        }

        public void Connect(UI_LobbyTabs tabs, UI_EquipmentShop shop)
        {
            var page = tabs.transform.Find("Village");
            page.Find("UIOnlyHome").gameObject.SetActive(false);
            SetRect(tabs.transform.Find("Header"), 0.035f, 0.927f, 0.965f, 0.985f);
            SetRect(tabs.transform.Find("Navigation"), 0.035f, 0.018f, 0.965f, 0.080f);
            page.Find("Welcome").gameObject.SetActive(false);
            page.Find("OpenEquipmentShop").gameObject.SetActive(false);
            tabs.transform.Find("Header/Location").GetComponent<UnityEngine.UI.Text>().text = "성소 광장 · 숲속 마을";
            var status = page.Find("Welcome/Eyebrow").GetComponent<UnityEngine.UI.Text>();
            status.fontSize = 21;
            var user = ThreeSouls.Manager.UserManager.Get();
            var quests = user.GetQuestManager();
            quests.BeginLobbySession();
            var map = tabs.GetComponentInChildren<UI_DungeonSelection>(true);
            var runeShop = Instantiate(RuneShopPrefab, tabs.transform, false);
            runeShop.SetUp(user.GetRuneManager(), user);
            var story = Instantiate(StoryPrefab, page, false);
            story.SetUp(quests, NpcInteraction, map);
            NpcInteraction.Configure(tabs, runeShop, story, map, ShopInteraction, quests,
                ThreeSouls.Data.LobbyTables.Get<ThreeSouls.Data.Generated.RuneShopRuleTableDataTable>().Items[0].InteractionRadius);
            Input.Configure(tabs, NpcInteraction);
            ShopInteraction.Configure(tabs, shop, status);
            var minimap = Instantiate(MinimapPrefab, page, false);
            minimap.Connect(Party);
            World.SetActive(true);
        }

        private static void SetRect(Transform target, float left, float bottom, float right, float top)
        {
            var rect = (RectTransform)target;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
