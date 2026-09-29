using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Character
{
    /// <summary>미완성 몬스터 구현의 공통 기반입니다.</summary>
    public class Monster : BaseEntity
    {
        public override LayerMask EnemyLayerMask => LayerMask.GetMask(StringConstants.LAYERMASK_ENEMY_HERO);

        public override void SetUp(
            CharacterTableDataRow chareData,
            ThreeSoulsRuleDataRow ruleData,
            SpriteLibraryAsset sprLibraryAssetData,
            ICombatWorld combatWorld)
        {
            base.SetUp(chareData, ruleData, sprLibraryAssetData, combatWorld);
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var newP = new Vector2(transform.position.x, transform.position.y + 0.5f);

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(
                newP,
                Vector3.forward,
                Constants.CIRCLE_RADIUS_MONSTER
            );
        }
        #endif
    }
}
