using ThreeSouls.Data.Generated;
using ThreeSouls.Growth;

namespace ThreeSouls.Interface
{
    /// <summary>
    /// 영웅이 현재 성장 수치를 적용하고 확정된 변경을 구독하는 계약입니다.
    /// </summary>
    public interface IHeroGrowthSource
    {
        delegate void OnHeroGrowthChangedDelegate(string heroKey);
        event OnHeroGrowthChangedDelegate OnHeroGrowthChangedEventHandler;
        HeroGrowthBonuses GetBonuses(CharacterTableDataRow hero);
    }
}
