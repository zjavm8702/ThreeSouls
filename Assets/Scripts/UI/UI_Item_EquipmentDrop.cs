using ThreeSouls.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 드랍 위치에 표시할 장비 아이콘과 등급별 이름표를 바인딩합니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class UI_Item_EquipmentDrop : BaseUIItem
{
    [SerializeField] private Image ItemIcon = null;
    [SerializeField] private Image Border = null;
    [SerializeField] private Text NameText = null;
    [SerializeField] private CanvasGroup Group = null;

    public Vector3 WorldPosition { get; private set; }
    public float CreatedAt { get; private set; }
    public RectTransform RectTransform => (RectTransform)transform;

    /// <summary>
    /// 재사용 시 아이콘, 색상과 표시 시간을 초기화합니다.
    /// </summary>
    public void Bind(Sprite icon, string name, Color color, Vector3 position)
    {
        ItemIcon.sprite = icon;
        NameText.text = name;
        NameText.color = color;
        Border.color = new Color(color.r, color.g, color.b, 0.65f);
        WorldPosition = position;
        CreatedAt = Time.time;
        Group.interactable = false;
        Group.blocksRaycasts = false;
        Group.alpha = 1;
        RectTransform.sizeDelta = new Vector2(Mathf.Clamp(NameText.preferredWidth + 64, 150, 420), 38);
    }

    /// <summary>
    /// 출현 크기와 종료 페이드를 표시하되 월드 드랍 위치는 유지합니다.
    /// </summary>
    public void SetAppearance(float age, float duration, bool visible)
    {
        Group.alpha = visible == true ? Mathf.Clamp01((duration - age) / 0.35f) : 0;
        RectTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1, Mathf.Clamp01(age / 0.12f));
    }

    /// <summary>
    /// 반환된 이름표의 이전 장비 표시를 지웁니다.
    /// </summary>
    public override void Clear()
    {
        ItemIcon.sprite = null;
        NameText.text = string.Empty;
    }
}
