using System;
using System.Collections.Generic;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 장비 드랍 이벤트를 월드 위치에 붙는 아이콘과 이름표로 표시하고 시간 만료 후 반환합니다.
    /// 수집 및 인벤토리와 표시 수명은 별도로 유지합니다.
    /// </summary>
    public sealed class CombatEquipmentDropPresenter : MonoBehaviour, IEventListener<Event_EquipmentDropped>, IEventListener<Event_GoldDropped>, IEventListener<Event_CombatStageCompleted>
    {
        private RectTransform EffectLayer;
        [SerializeField] private UI_Item_EquipmentDrop LabelPrefab = null;
        [SerializeField, Min(0.5f)] private float DisplayDuration = 5f;
        [SerializeField] private EquipmentIcon[] ItemIcons = Array.Empty<EquipmentIcon>();

        private readonly ComponentPool<UI_Item_EquipmentDrop> Pool = new();
        private readonly List<UI_Item_EquipmentDrop> Labels = new();
        private Canvas ParentCanvas;
        private Camera WorldCamera;
        public int CallbackOrder => 0;

        /// <summary>
        /// 현재 아이콘 주소가 비어 있는 장비 테이블에 사용할 직렬화된 이미지 연결입니다.
        /// </summary>
        [Serializable]
        private sealed class EquipmentIcon
        {
            public string ItemKey;
            public Sprite Icon;
        }

        /// <summary>
        /// HUD가 활성화된 동안 실제 장비 드랍을 구독합니다.
        /// </summary>
        private void OnEnable()
        {
            EffectLayer = UIManager.Get().GetItemCanvas();
            ParentCanvas = EffectLayer.GetComponent<Canvas>();
            WorldCamera = Camera.main;
            EventUtility.AddListener<Event_EquipmentDropped>(this);
            EventUtility.AddListener<Event_GoldDropped>(this);
            EventUtility.AddListener<Event_CombatStageCompleted>(this);
        }

        /// <summary>
        /// 등급 색상, 테이블 이름과 실제 아이콘을 바인딩해 드랍 위치에 표시합니다.
        /// </summary>
        public void OnEvent<TSender>(TSender sender, Event_EquipmentDropped eventData) where TSender : class
        {
            if (!GameSettings.ShowLoot) return;
            if (Pool.IsPoolCreated() == false)
            {
                Pool.CreatePool(LabelPrefab, 0);
            }

            var icon = LobbyPresentationData.GetItemIcon(eventData.ItemKey);

            var color = LobbyPresentationData.GetRarityColor(eventData.Rarity);
            var rarityName = LobbyPresentationData.GetRarityName(eventData.Rarity);
            var item = Pool.Spawn(EffectLayer, Vector3.zero);
            item.RectTransform.anchorMin = EffectLayer.pivot;
            item.RectTransform.anchorMax = EffectLayer.pivot;
            item.Bind(icon, rarityName + "  " + TableManager.Get().GetEquipmentName(eventData.ItemKey), color, eventData.WorldPosition);
            Labels.Add(item);
        }

        /// <summary>
        /// 카메라가 이동해도 원래 드랍 위치를 따라가며 가까운 이름표는 위로 겹침을 피합니다.
        /// </summary>
        public void OnEvent<T>(T sender, Event_GoldDropped value) where T : class
        {
            if (!GameSettings.ShowLoot) return;
            if (!Pool.IsPoolCreated()) Pool.CreatePool(LabelPrefab, 0);
            var item = Pool.Spawn(EffectLayer, Vector3.zero);
            item.RectTransform.anchorMin = item.RectTransform.anchorMax = EffectLayer.pivot;
            item.Bind(value.Icon, $"골드 +{value.Amount:N0}", new Color(1f, .82f, .35f), value.WorldPosition);
            Labels.Add(item);
        }

        public void OnEvent<T>(T sender, Event_CombatStageCompleted value) where T : class => ClearLabels();

        private void ClearLabels()
        {
            for (int i = Labels.Count - 1; i >= 0; i--) Pool.Recycle(Labels[i]);
            Labels.Clear();
        }

        private void LateUpdate()
        {
            if (!GameSettings.ShowLoot) ClearLabels();
            for (int i = Labels.Count - 1; i >= 0; i--)
            {
                if (Time.time - Labels[i].CreatedAt >= DisplayDuration)
                {
                    Pool.Recycle(Labels[i]);
                    Labels.RemoveAt(i);
                }
            }

            if (Labels.Count == 0)
            {
                return;
            }

            ParentCanvas ??= EffectLayer.GetComponentInParent<Canvas>();
            WorldCamera = WorldCamera != null ? WorldCamera : Camera.main;
            if (ParentCanvas == null || WorldCamera == null)
            {
                return;
            }

            var canvasCamera = ParentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ParentCanvas.worldCamera;
            for (int i = 0; i < Labels.Count; i++)
            {
                var item = Labels[i];
                var screen = WorldCamera.WorldToScreenPoint(item.WorldPosition);
                var position = Vector2.zero;
                var visible = screen.z > 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    EffectLayer, screen, canvasCamera, out position);
                item.SetAppearance(Time.time - item.CreatedAt, DisplayDuration, visible);
                if (visible == false)
                {
                    continue;
                }

                position.y += 32;
                for (int j = 0; j < i; j++)
                {
                    var previous = Labels[j].RectTransform;
                    if (Mathf.Abs(position.x - previous.anchoredPosition.x) < (item.RectTransform.rect.width + previous.rect.width) * 0.5f &&
                        Mathf.Abs(position.y - previous.anchoredPosition.y) < 42)
                    {
                        position.y = previous.anchoredPosition.y + 42;
                    }
                }

                item.RectTransform.anchoredPosition = position;
            }
        }

        /// <summary>
        /// HUD 비활성화와 씬 종료에서 이벤트 및 진행 중 이름표를 정리합니다.
        /// </summary>
        private void OnDisable()
        {
            EventUtility.RemoveListener<Event_EquipmentDropped>(this);
            EventUtility.RemoveListener<Event_GoldDropped>(this);
            EventUtility.RemoveListener<Event_CombatStageCompleted>(this);
            for (int i = Labels.Count - 1; i >= 0; i--)
            {
                Pool.Recycle(Labels[i]);
            }

            Labels.Clear();
        }

        /// <summary>
        /// HUD가 파괴되면 대기 중 이름표 인스턴스도 정리합니다.
        /// </summary>
        private void OnDestroy()
        {
            Pool.Release();
        }
    }
}
