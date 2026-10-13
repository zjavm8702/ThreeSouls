using System;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 상점 건물/간판의 선택 영역과 통행 가능한 입구를 제공합니다.
    /// </summary>
    public sealed class VillageShopTarget : MonoBehaviour
    {
        [SerializeField] private Collider2D[] HitAreas;
        [SerializeField] private Transform Entrance;

        private void Awake()
        {
            if (Entrance == null || HitAreas == null || HitAreas.Length == 0)
            {
                throw new InvalidOperationException("상점 선택 영역과 입구를 연결해야 합니다.");
            }

            for (var i = 0; i < HitAreas.Length; i++)
            {
                if (HitAreas[i] == null)
                {
                    throw new InvalidOperationException("상점 선택 영역이 누락됐습니다.");
                }
            }
        }

        public bool ContainsPoint(Vector2 position)
        {
            for (var i = 0; i < HitAreas.Length; i++)
            {
                if (HitAreas[i].OverlapPoint(position) == true)
                {
                    return true;
                }
            }

            return false;
        }

        public Vector2 GetEntrancePosition()
        {
            return Entrance.position;
        }
    }
}
