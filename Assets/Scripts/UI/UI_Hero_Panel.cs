using System;
using ThreeSouls.Character;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 영웅의 이름, 초상, 체력과 스킬 쿨타임을 표시하고 체력 구독을 관리합니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class UI_Hero_Panel : MonoBehaviour
{
    [SerializeField] private ClassType Class;
    [SerializeField] private Image HeroIconImage = null;
    [SerializeField] private Image HealthFill = null;
    [SerializeField] private Text NameText = null;
    [SerializeField] private Text HealthText = null;
    [SerializeField] private SkillSlot[] SkillSlots = Array.Empty<SkillSlot>();

    private CanvasGroup Group;
    private BaseEntity Hero;
    private const string HealthString = "{0} / {1}";

    /// <summary>
    /// 프리팹에 연결된 스킬 표시 요소와 해당 테이블 행을 보관합니다.
    /// </summary>
    [Serializable]
    private sealed class SkillSlot
    {
        public string SkillKey;
        public Image Icon;
        public Image CooldownFill;
        public Text CooldownText;
        [NonSerialized] public SkillTableDataRow Data;
    }

    private void Awake()
    {
        Group = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 스폰이 끝난 전투의 영웅과 스킬 행을 연결하고 최초 표시를 적용합니다.
    /// </summary>
    public void SetUp(GameCombatManager combat)
    {
        Release();

        if (combat == null)
        {
            throw new ArgumentNullException(nameof(combat));
        }

        Group ??= GetComponent<CanvasGroup>();

        if (HeroIconImage == null || HealthFill == null || NameText == null || HealthText == null || SkillSlots.Length == 0)
        {
            throw new InvalidOperationException($"영웅 패널의 표시 요소를 연결하세요 : {name}");
        }

        var heroKey = GetHeroKey();
            HealthFill.color = ThreeSouls.UI.UI_Item_HealthBar.HeroColor;
        var names = TableManager.Get().GetStringTableDataTable();

        for (var i = 0; i < names.Count; i++)
        {
            if (names[i].StrKey == heroKey)
            {
                NameText.text = names[i].KrValue;
                break;
            }
        }

        var skills = combat.GetSkills();

        for (var i = 0; i < SkillSlots.Length; i++)
        {
            var slot = SkillSlots[i];

            if (slot == null || slot.Icon == null || slot.CooldownFill == null || slot.CooldownText == null)
            {
                throw new InvalidOperationException($"영웅 패널의 스킬 표시 요소를 연결하세요 : {name}, {i}");
            }

            for (var j = 0; j < skills.Count; j++)
            {
                if (skills[j].Key == slot.SkillKey && skills[j].HeroKey == heroKey)
                {
                    slot.Data = skills[j];
                    break;
                }
            }

            if (slot.Data == null)
            {
                throw new InvalidOperationException($"영웅의 스킬 테이블 행을 확인하세요 : {heroKey}, {slot.SkillKey}");
            }

            slot.Icon.sprite = combat.GetSkillIcon(slot.SkillKey);
            slot.Icon.enabled = true;
            slot.Icon.raycastTarget = false;
            slot.CooldownFill.sprite = slot.Icon.sprite;
            slot.CooldownFill.raycastTarget = false;
            slot.CooldownText.raycastTarget = false;
        }

        var heroes = GameCombatManager.Get().GetAliveHeroes();

        for (var i = 0; i < heroes.Count; i++)
        {
            if (heroes[i].CharacterData.CharacterKey == heroKey)
            {
                Hero = heroes[i];
                Hero.CharHealth.OnHealthChangeEventHandler += OnHealthChangeEventHandler;
                Hero.CharHealth.GetHealth(out var maximum, out var current);
                OnHealthChangeEventHandler(false, maximum, current, 0, null);
                UpdateCooldowns();
                return;
            }
        }

        ShowHealth(0, 0, false);
        UpdateCooldowns();
    }

    private string GetHeroKey()
    {
        switch (Class)
        {
            case ClassType.Knight:
                return "Key_night";
            case ClassType.Archer:
                return "Key_archer";
            case ClassType.Wizard:
                return "Key_wizard";
            default:
                throw new InvalidOperationException($"영웅 패널의 직업을 확인하세요 : {Class}");
        }
    }

    private void OnHealthChangeEventHandler(
        bool takeDamage,
        float maximum,
        float current,
        float damage,
        BaseEntity sourceEntity)
    {
        ShowHealth(current, maximum, Hero.CharHealth.IsAlive);
    }

    private void ShowHealth(float current, float maximum, bool alive)
    {
        HealthFill.fillAmount = maximum > 0 ? Mathf.Clamp01(current / maximum) : 0;
        HealthText.text = string.Format(HealthString, Mathf.CeilToInt(current), Mathf.CeilToInt(maximum));
        Group.alpha = alive == true ? 1 : 0.4f;
    }

    private void Update()
    {
        UpdateCooldowns();
    }

    private void UpdateCooldowns()
    {
        for (var i = 0; i < SkillSlots.Length; i++)
        {
            var slot = SkillSlots[i];
            var remaining = GameCombatManager.Get().GetSkillCooldownRemaining(slot.SkillKey);
            slot.CooldownFill.fillAmount = Mathf.Clamp01(remaining / slot.Data.Cooldown);
            slot.CooldownText.text = remaining > 0 ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
        }
    }

    /// <summary>
    /// 이전 영웅의 체력 구독과 전투 참조를 해제합니다.
    /// </summary>
    public void Release()
    {
        if (Hero != null)
        {
            Hero.CharHealth.OnHealthChangeEventHandler -= OnHealthChangeEventHandler;
        }

        Hero = null;

        for (var i = 0; i < SkillSlots.Length; i++)
        {
            if (SkillSlots[i] != null)
            {
                SkillSlots[i].Data = null;
            }
        }
    }

    private void OnDisable()
    {
        Release();
    }
}
