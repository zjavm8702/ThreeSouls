using UnityEngine.EventSystems;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 반복 목록에서 재사용되는 UI 항목의 공통 기반 클래스입니다.
    /// 구체 항목은 데이터 바인딩과 자신의 이벤트 정리를 구현합니다.
    /// </summary>
    public abstract class BaseUIItem : UIBehaviour
    {
        public virtual void Bind(int id) { }

        public virtual void Clear() { }

        protected override void OnDisable()
        {
            Clear();
            base.OnDisable();
        }
    }
}
