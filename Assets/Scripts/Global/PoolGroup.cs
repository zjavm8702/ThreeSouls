using UnityEngine;

namespace ThreeSouls.Global
{
    /// <summary>
    /// 풀들이 사용하는 대기 객체의 부모를 보관합니다. 인스턴스 소유권은 각 풀에 있습니다.
    /// </summary>
    public static class PoolGroup
    {
        private static GameObject Group;

        public static Transform GetParent()
        {
            if (Group == null)
            {
                Group = new GameObject(nameof(PoolGroup));
                Object.DontDestroyOnLoad(Group);
            }

            return Group.transform;
        }

        /// <summary>
        /// 모든 풀을 해제한 뒤 공통 부모를 제거합니다.
        /// </summary>
        public static void Release()
        {
            if (Group != null)
            {
                Object.Destroy(Group);
                Group = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Group = null;
        }
    }
}
