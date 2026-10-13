using ThreeSouls.Character;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Projectile;
using UnityEngine;

namespace ThreeSouls.Interface
{
    /// <summary>
    /// 무기가 투사체 생성을 요청하는 계약입니다. 풀 수명은 스폰 담당자가 소유합니다.
    /// </summary>
    public interface IProjectileSpawner
    {
        WeaponProjectile SpawnProjectile(Transform parent, Vector3 position,
            SpriteLibrarys library, BaseEntity source, BaseEntity target);
    }
}
