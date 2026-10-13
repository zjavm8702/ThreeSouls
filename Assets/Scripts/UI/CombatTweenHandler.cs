using System;
using DG.Tweening;
using ThreeSouls.Manager;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 외부에서 받은 UI 원본, Canvas와 카메라로 전투 수치 표시와 트윈 수명을 연결합니다.
    /// UI 생성과 반환은 UIManager, 모션 구성은 각 트윈 객체에 위임합니다.
    /// </summary>
    public sealed class CombatTweenHandler
    {
        private readonly UIManager UIManager;
        private readonly UI_Item_CombatValue Origin;
        private readonly RectTransform Parent;
        private readonly Canvas ParentCanvas;
        private readonly Camera WorldCamera;
        private readonly CombatDamageTween DamageTween = new();
        private readonly CombatRewardTween RewardTween = new();
        private int DamageLane;

        public CombatTweenHandler(UIManager uiManager, UI_Item_CombatValue origin, RectTransform parent, Camera worldCamera)
        {
            if (uiManager == null || origin == null || parent == null || worldCamera == null)
            {
                throw new ArgumentException("전투 트윈의 UIManager, 원본, 부모 Canvas와 월드 카메라가 필요합니다.");
            }

            if (parent.TryGetComponent<Canvas>(out var canvas) == false ||
                (canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null))
            {
                throw new InvalidOperationException("전투 트윈 부모의 Canvas와 렌더 카메라를 확인하세요.");
            }

            UIManager = uiManager;
            Origin = origin;
            Parent = parent;
            ParentCanvas = canvas;
            WorldCamera = worldCamera;
            UIManager.OnClearUIEventHandler += KillAll;
        }

        public void SpawnDamageText(Vector3 worldPosition, float damage)
        {
            SpawnDamageText(worldPosition, new ThreeSouls.Combat.CombatDamageInfo(damage));
        }

        public void SpawnDamageText(Vector3 worldPosition, ThreeSouls.Combat.CombatDamageInfo damage)
        {
            if (TryGetStartPosition(worldPosition, out var start) == false)
            {
                return;
            }

            if (GameSettings.ShowDamage == false) return;
            var lane = (DamageLane++ % 5) - 2;
            start += new Vector2(lane * 14f, 24f);
            var item = SpawnItem(start);
            item.BindDamage(damage);
            PlaySequence(DamageTween.Play((RectTransform)item.transform, item.GetCanvasGroup(), start, damage, lane), item);
        }

        public void SpawnRewardText(Vector3 worldPosition, RectTransform target, Sprite icon, string label)
        {
            if (target == null || TryGetStartPosition(worldPosition, out var start) == false)
            {
                return;
            }

            var targetCanvas = target.GetComponentInParent<Canvas>();

            if (targetCanvas == null)
            {
                throw new InvalidOperationException("보상 도착 지점은 Canvas 아래에 있어야 합니다.");
            }

            var targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            var screenPosition = RectTransformUtility.WorldToScreenPoint(targetCamera, target.position);

            if (TryGetLocalPosition(screenPosition, out var destination) == false)
            {
                return;
            }

            var item = SpawnItem(start);
           
            item.Bind(icon, label);
          
            var sequence = RewardTween.Play((RectTransform)item.transform, item.GetCanvasGroup(), start, destination, target);
           
            PlaySequence(sequence, item);
        }

        public void KillAll()
        {
            DOTween.Kill(this);
        }

        public void Release()
        {
            KillAll();

            if (UIManager != null)
            {
                UIManager.OnClearUIEventHandler -= KillAll;
            }
        }

        private UI_Item_CombatValue SpawnItem(Vector2 position)
        {
            var item = UIManager.CreateUIItem(Origin, Parent);
            var rect = (RectTransform)item.transform;
           
            rect.anchorMin = Parent.pivot;
            rect.anchorMax = Parent.pivot;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
           
            item.GetCanvasGroup().alpha = 1;
           
            return item;
        }

        private void PlaySequence(Sequence sequence, UI_Item_CombatValue item)
        {
            sequence.SetId(this);
            sequence.onKill += () =>
            {
                if (UIManager != null)
                {
                    UIManager.ReleaseUIItem(item);
                }
            };
            sequence.Play();
        }

        private bool TryGetStartPosition(Vector3 worldPosition, out Vector2 localPoint)
        {
            var screenPoint = WorldCamera.WorldToScreenPoint(worldPosition);
           
            localPoint = default;

            if (screenPoint.z <= 0)
            {
                return false;
            }

            return TryGetLocalPosition(screenPoint, out localPoint);
        }

        private bool TryGetLocalPosition(Vector2 screenPosition, out Vector2 localPoint)
        {
            var camera = ParentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ParentCanvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(Parent, screenPosition, camera, out localPoint);
        }
    }
}
