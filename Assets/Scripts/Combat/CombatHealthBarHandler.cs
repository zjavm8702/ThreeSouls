using System;
using System.Collections.Generic;
using ThreeSouls.Character;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 전투 캐릭터와 체력바의 바인딩 수명을 관리합니다.
    /// UI 인스턴스의 생성과 풀 반환은 UIManager에 위임합니다.
    /// </summary>
    internal sealed class CombatHealthBarHandler
    {
        private const float OverlapSpacing = 3f;
        private readonly Dictionary<BaseEntity, UI_Item_HealthBar> HealthBarContainer = new();
        private readonly List<HealthBarLayout> LayoutContainer = new();
        private readonly List<Rect> PlacedRectContainer = new();
        private UI_Item_HealthBar HealthBarOriginPrefab = null;

        public void Initialize()
        {
            var asset = AddressableManager.Get().LoadAsset<GameObject>(Prefabs.UI_Item_HealthBar.ToAddress());

            if (asset == null)
            {
                throw new InvalidOperationException($"Required combat asset was not loaded: {Prefabs.UI_Item_HealthBar.ToAddress()}");
            }

            if (asset.TryGetComponent<UI_Item_HealthBar>(out var component) == false)
            {
                throw new InvalidOperationException($"Required component {typeof(UI_Item_HealthBar).Name} was not found: {Prefabs.UI_Item_HealthBar.ToAddress()}");
            }

            HealthBarOriginPrefab = component;

            Canvas.willRenderCanvases -= UpdateLayout;
            Canvas.willRenderCanvases += UpdateLayout;
        }

        public void Register(BaseEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (HealthBarContainer.ContainsKey(entity) == true)
            {
                throw new InvalidOperationException($"Health bar is already registered: {entity.name}");
            }

            var healthBar = UIManager.Get().CreateUIItem(HealthBarOriginPrefab);
            healthBar.Bind(entity);
            HealthBarContainer.Add(entity, healthBar);
            UpdateLayout();
        }

        public void Unregister(BaseEntity entity)
        {
            if (entity == null || HealthBarContainer.Remove(entity, out var healthBar) == false)
            {
                return;
            }

            UIManager.Get().ReleaseUIItem(healthBar);
        }

        public void Release()
        {
            Canvas.willRenderCanvases -= UpdateLayout;
            using var healthBars = HealthBarContainer.Values.GetEnumerator();

            while (healthBars.MoveNext() == true)
            {
                UIManager.Get().ReleaseUIItem(healthBars.Current);
            }

            HealthBarContainer.Clear();
            LayoutContainer.Clear();
            PlacedRectContainer.Clear();
            HealthBarOriginPrefab = null;
        }

        private void UpdateLayout()
        {
            LayoutContainer.Clear();
            PlacedRectContainer.Clear();
        
            using var healthBars = HealthBarContainer.Values.GetEnumerator();

            while (healthBars.MoveNext() == true)
            {
                var healthBar = healthBars.Current;

                if (healthBar != null &&
                    healthBar.isActiveAndEnabled == true &&
                    healthBar.TryGetDesiredPosition(out var desiredPosition) == true)
                {
                    LayoutContainer.Add(new HealthBarLayout(healthBar, desiredPosition));
                }
            }

            LayoutContainer.Sort(CompareLayout);

            for (var i = 0; i < LayoutContainer.Count; i++)
            {
                var layout = LayoutContainer[i];
                var resolvedPosition = layout.DesiredPosition;
                var layoutRect = layout.HealthBar.GetLayoutRect(resolvedPosition);

                for (var j = 0; j < PlacedRectContainer.Count; j++)
                {
                    var placedRect = PlacedRectContainer[j];

                    if (layoutRect.Overlaps(placedRect) == false)
                    {
                        continue;
                    }

                    var moveUpDistance = placedRect.yMax - layoutRect.yMin + OverlapSpacing;
                    resolvedPosition.y += moveUpDistance;
                    layoutRect.position += Vector2.up * moveUpDistance;
                }

                layout.HealthBar.SetLayoutPosition(resolvedPosition);
                PlacedRectContainer.Add(layoutRect);
            }
        }

        private static int CompareLayout(HealthBarLayout left, HealthBarLayout right)
        {
            var yCompare = left.DesiredPosition.y.CompareTo(right.DesiredPosition.y);

            return yCompare != 0 ? yCompare : left.DesiredPosition.x.CompareTo(right.DesiredPosition.x);
        }

        private readonly struct HealthBarLayout
        {
            public UI_Item_HealthBar HealthBar { get; }
            public Vector2 DesiredPosition { get; }

            public HealthBarLayout(UI_Item_HealthBar healthBar, Vector2 desiredPosition)
            {
                HealthBar = healthBar;
                DesiredPosition = desiredPosition;
            }
        }
    }
}
