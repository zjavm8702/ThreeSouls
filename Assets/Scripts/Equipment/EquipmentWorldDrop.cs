using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Equipment
{
    /// <summary>
    /// 전투 씬의 장비를 던전 보상으로 수집하며 승리 전에는 보유 아이템으로 지급하지 않습니다.
    /// </summary>
    public sealed class EquipmentWorldDrop : MonoBehaviour
    {
        private EquipmentInstance Item;
        private bool Collected;
        private float NextCheck;
        private float CreatedAt;

        private static Sprite Marker;
        private static Texture2D MarkerTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMarker()
        {
            if (Marker != null)
            {
                Destroy(Marker);
            }

            if (MarkerTexture != null)
            {
                Destroy(MarkerTexture);
            }

            Marker = null;
            MarkerTexture = null;
        }

        /// <summary>
        /// 장비를 지정 위치 주변에 드랍하고 해당 위치의 이름표 표시 이벤트를 전달합니다.
        /// </summary>
        public static EquipmentWorldDrop Create(Vector3 position, EquipmentInstance item, ItemRarity rarity)
        {
            var root = new GameObject("Equipment_" + item.ItemKey);
            root.transform.position = position + (Vector3)Random.insideUnitCircle * 0.25f;
            var view = root.AddComponent<SpriteRenderer>();

            if (Marker == null)
            {
                MarkerTexture = new Texture2D(12, 12, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                };
                var colors = new Color[144];

                for (int y = 0; y < 12; y++)
                {
                    for (int x = 0; x < 12; x++)
                    {
                        colors[y * 12 + x] = Mathf.Abs(x - 5.5f) + Mathf.Abs(y - 5.5f) < 5.5f ? Color.white : Color.clear;
                    }
                }

                MarkerTexture.SetPixels(colors);
                MarkerTexture.Apply();
                Marker = Sprite.Create(MarkerTexture, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 32);
            }

            view.sprite = Marker;
            view.sortingOrder = 30;
            view.color = ThreeSouls.UI.LobbyPresentationData.GetRarityColor(rarity);
            var drop = root.AddComponent<EquipmentWorldDrop>();
            drop.Item = item;
            drop.CreatedAt = Time.time;
            EventUtility.TriggerEvent(drop, new Event_EquipmentDropped(item.ItemKey, rarity, root.transform.position));

            return drop;
        }

        private void Update()
        {
            if (Collected == true || Time.time < NextCheck || Time.time < CreatedAt + 0.3f)
            {
                return;
            }

            NextCheck = Time.time + 0.2f;
            var heroItems = GameCombatManager.Get().GetAliveHeroes();

            for (var heroIndex = 0; heroIndex < heroItems.Count; heroIndex++)
            {
                var hero = heroItems[heroIndex];

                if (hero.CharHealth == null ||
                    hero.CharHealth.IsAlive == false ||
                    Vector2.SqrMagnitude(hero.GetWorldPosition() - (Vector2)transform.position) > 2.25f)
                {
                    continue;
                }

                TryCollect();

                return;
            }
        }

        /// <summary>
        /// 던전 보상 임시 보관이 성공한 경우에만 수집을 확정하고 드롭을 제거합니다.
        /// </summary>
        internal bool TryCollect()
        {
            if (Collected == true || InventoryManager.Get().TryCollectCombatEquipment(Item) == false)
            {
                return false;
            }

            Collected = true;
            EventUtility.TriggerEvent(this, new Event_EquipmentCollected(Item));
            Destroy(gameObject);

            return true;
        }
    }
}
