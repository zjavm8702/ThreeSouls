using ThreeSouls.Character;

namespace ThreeSouls.Interface
{
    /// <summary>
    /// 몬스터가 처치 보상 지급과 표시를 요청하는 계약입니다.
    /// 몬스터는 UI, 에셋과 구체 전투 매니저를 알지 않습니다.
    /// </summary>
    public interface ICombatRewardWorld
    {
        void SpawnRewardText(Monster monster);
    }
}
