using System;
using System.Collections.Generic;
using DG.Tweening;
using ThreeSouls.Enumeration;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 결과창 등장, 결과 타이틀, 골드 숫자 증가와 장비 순차 등장을 재생합니다.
    /// </summary>
    internal sealed class CombatResultTween
    {
        private readonly CanvasGroup Dim;
        private readonly CanvasGroup Contents;
        private readonly RectTransform Panel;
        private readonly RectTransform Title;
        private readonly RectTransform GoldIcon;
        private Sequence Sequence;
        private int DisplayedGold;

        internal CombatResultTween(CanvasGroup dim, CanvasGroup contents, RectTransform panel, RectTransform title, RectTransform goldIcon)
        {
            Dim = dim;
            Contents = contents;
            Panel = panel;
            Title = title;
            GoldIcon = goldIcon;
        }

        internal void Play(int gold, Action<int> displayGold, IReadOnlyList<UI_Item_ResultReward> items, CombatOutcome outcome)
        {
            Clear();
            Dim.alpha = 0f;
            Contents.alpha = 0f;
            Panel.localScale = Vector3.one * 0.88f;
            Title.localScale = Vector3.one * 0.75f;
            DisplayedGold = 0;
            displayGold(0);

            Sequence = DOTween.Sequence().SetUpdate(true);
            Sequence.Insert(0f, Dim.DOFade(1f, 0.25f));
            Sequence.Insert(0.1f, Contents.DOFade(1f, 0.25f));
            var entranceEase = outcome == CombatOutcome.Victory ? Ease.OutBack : Ease.OutCubic;
            Sequence.Insert(0.1f, Panel.DOScale(1f, 0.45f).SetEase(entranceEase));
            Sequence.Insert(0.2f, Title.DOScale(1f, 0.55f).SetEase(entranceEase));
            Sequence.Insert(0.5f, DOTween.To(() => DisplayedGold, value =>
            {
                DisplayedGold = value;
                displayGold(value);
            }, gold, 1.15f).SetEase(Ease.OutCubic));
            if (outcome == CombatOutcome.Victory && gold > 0)
            {
                Sequence.Insert(1.65f, GoldIcon.DOPunchScale(Vector3.one * 0.16f, 0.35f, 4, 0.5f));
            }

            for (var i = 0; i < items.Count; i++)
            {
                var itemGroup = items[i].GetPresentationGroup();
                var itemRect = items[i].GetPresentationRect();
                var delay = 0.75f + Mathf.Min(i, 12) * 0.08f;
                itemGroup.alpha = 0f;
                itemRect.localScale = Vector3.one * 0.85f;
                Sequence.Insert(delay, itemGroup.DOFade(1f, 0.25f));
                Sequence.Insert(delay, itemRect.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
            }

            Sequence.OnComplete(() => displayGold(gold));
        }

        internal void PlayExit(Action completed)
        {
            Clear();
            Contents.interactable = false;
            Sequence = DOTween.Sequence().SetUpdate(true);
            Sequence.Join(Contents.DOFade(0f, 0.12f));
            Sequence.Join(Panel.DOScale(0.98f, 0.12f).SetEase(Ease.InQuad));
            Sequence.OnComplete(() => { Sequence = null; completed?.Invoke(); });
        }

        internal void Clear()
        {
            Sequence?.Kill();
            Sequence = null;
            Dim.alpha = 1f;
            Contents.alpha = 1f;
            Contents.interactable = true;
            Panel.localScale = Vector3.one;
            Title.localScale = Vector3.one;
            GoldIcon.localScale = Vector3.one;
        }
    }
}
