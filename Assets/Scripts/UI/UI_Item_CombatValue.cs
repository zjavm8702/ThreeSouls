using ThreeSouls.UI;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class UI_Item_CombatValue : BaseUIItem
{
    [SerializeField] private Image IconImage = null;
    [SerializeField] private Text ValueText = null;
    private Color DefaultTextColor;
    private Vector2 DefaultTextPosition;
    private Vector2 DefaultTextSize;
    private bool DefaultRichText;
    private int DefaultFontSize;
    private FontStyle DefaultFontStyle;
    private CanvasGroup Group;
    private Text HitLabel;

    protected override void Awake()
    {
        base.Awake();
        DefaultTextColor = ValueText.color;
        DefaultFontSize = ValueText.fontSize;
        DefaultFontStyle = ValueText.fontStyle;
        DefaultTextPosition = ValueText.rectTransform.anchoredPosition;
        DefaultTextSize = ValueText.rectTransform.sizeDelta;
        DefaultRichText = ValueText.supportRichText;
        IconImage.raycastTarget = false;
        ValueText.raycastTarget = false;
        Group = GetComponent<CanvasGroup>();
        Group.interactable = false;
        Group.blocksRaycasts = false;
        var labelObject = new GameObject("Hit Type", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
        labelObject.transform.SetParent(transform, false);
        HitLabel = labelObject.GetComponent<UnityEngine.UI.Text>();
        HitLabel.font = ValueText.font;
        HitLabel.fontSize = 22;
        HitLabel.fontStyle = FontStyle.Bold;
        HitLabel.alignment = TextAnchor.MiddleCenter;
        HitLabel.raycastTarget = false;
        HitLabel.rectTransform.anchorMin = HitLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        HitLabel.rectTransform.sizeDelta = new Vector2(260, 70);
        HitLabel.rectTransform.anchoredPosition = new Vector2(0, 52);
        var outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.06f, 0.1f, 0.95f);
        outline.effectDistance = new Vector2(1, -1);
        HitLabel.enabled = false;
    }

    public CanvasGroup GetCanvasGroup()
    {
        return Group;
    }

    public void Bind(Sprite spr, string rewardTextValue)
    {
        IconImage.enabled = spr != null;
        IconImage.sprite = spr;
        ValueText.text = rewardTextValue;
        ValueText.color = DefaultTextColor;
        ValueText.fontSize = DefaultFontSize;
        ValueText.fontStyle = DefaultFontStyle;
        ValueText.rectTransform.anchoredPosition = DefaultTextPosition;
        ValueText.rectTransform.sizeDelta = DefaultTextSize;
        ValueText.supportRichText = DefaultRichText;
        HitLabel.enabled = false;
    }

    public void BindDamage(float damage, Color color)
    {
        Bind(null, damage.ToString("0.##"));
        ValueText.color = color;
        ValueText.fontSize = 50;
        ValueText.rectTransform.anchoredPosition = Vector2.zero;
    }

    public void BindDamage(ThreeSouls.Combat.CombatDamageInfo hit)
    {
        var prefix = hit.IsSkill ? (hit.IsCritical ? "SKILL CRIT!\n" : "SKILL\n") : hit.IsCritical ? "CRIT!\n" : "";
        var color = hit.IsSkill ? (hit.IsCritical ? new Color(1f, 0.55f, 1f) : new Color(0.5f, 0.85f, 1f)) :
            hit.IsCritical ? new Color(1f, 0.8f, 0.18f) : new Color(1f, 0.96f, 0.88f);
        Bind(null, hit.Amount.ToString("0.##"));
        HitLabel.enabled = prefix.Length > 0;
        HitLabel.text = prefix.TrimEnd('\n');
        HitLabel.color = color;
        ValueText.color = color;
        ValueText.fontSize = hit.IsCritical ? 62 : hit.IsSkill ? 54 : 42;
        ValueText.fontStyle = hit.IsCritical || hit.IsSkill ? FontStyle.Bold : DefaultFontStyle;
        ValueText.rectTransform.anchoredPosition = Vector2.zero;
        ValueText.rectTransform.sizeDelta = new Vector2(260, 160);
    }
}
