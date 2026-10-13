using ThreeSouls.Data;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Interface
{
    /// <summary>
    /// 영웅이 장비 변경을 구독하고 현재 보너스를 조회하는 계약입니다.
    /// </summary>
    public interface IHeroEquipmentSource
    {
        delegate void OnEquipmentChangedDelegate(string characterKey);

        event OnEquipmentChangedDelegate OnEquipmentChangedEventHandler;
        EquipmentBonuses GetEquipmentBonuses(CharacterTableDataRow hero);
    }
}
