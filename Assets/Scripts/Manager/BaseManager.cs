using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 매니저의 생성과 파괴를 그룹에 알리고 같은 타입의 인스턴스 중복을 막습니다.
    /// </summary>
    public abstract class BaseManager : MonoBehaviour, IManager
    {
        protected virtual void Awake()
        {
            GameManager.Instance.RegisterManager(this);
        }

        protected virtual void OnDestroy()
        {
            if (GameManager.TryGetInstance(out var manager) == true)
            {
                manager.RemoveManager(this);
            }
        }

        public abstract void Initialize();
        public abstract void Release();
        public abstract void OnSceneChanged();
    }
}
