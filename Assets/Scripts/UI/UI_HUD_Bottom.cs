using System;
using ThreeSouls.Manager;
using ThreeSouls.UI;
using UnityEngine;

/// <summary>
/// 하단 HUD의 영웅 패널을 전투에 연결하고 표시 수명을 관리합니다.
/// </summary>
public sealed class UI_HUD_Bottom : BaseUI
{
    [SerializeField] private UI_Hero_Panel[] Hero_Panels = Array.Empty<UI_Hero_Panel>();
    private bool IsInitialized;
    [SerializeField] private ThreeSouls.UI.UI_HUD_Sanctuary SanctuaryStatus;
    public override bool IsShowing => isActiveAndEnabled;

    public override void Initialize()
    {
        if (IsInitialized == true)
        {
            return;
        }

        if (Hero_Panels.Length == 0)
        {
            throw new InvalidOperationException("하단 HUD의 영웅 패널을 연결하세요.");
        }

        var combat = GameCombatManager.Get();
        SanctuaryStatus?.SetUp(combat);

        for (var i = 0; i < Hero_Panels.Length; i++)
        {
            if (Hero_Panels[i] == null)
            {
                throw new InvalidOperationException("하단 HUD의 영웅 패널 참조가 누락됐습니다.");
            }

            Hero_Panels[i].SetUp(combat);
        }

        IsInitialized = true;
    }

    public override void Release()
    {
        SanctuaryStatus?.Release();
        for (var i = 0; i < Hero_Panels.Length; i++)
        {
            if (Hero_Panels[i] != null)
            {
                Hero_Panels[i].Release();
            }
        }

        IsInitialized = false;
    }

    protected override void OnDisable()
    {
        Release();
        base.OnDisable();
    }
}
