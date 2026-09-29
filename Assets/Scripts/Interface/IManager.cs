namespace ThreeSouls.Interface
{
    public interface IManager
    {
        /// <summary>
        /// 매니저 초기화 시 최초 호출
        /// </summary>
        void Initialize();

        /// <summary>
        /// 매니저에 할당된 멤버 변수 메모리 해제 시 호출
        /// </summary>
        void Release();

        /// <summary>
        /// 씬 이동 시 자동 호출
        /// </summary>
        void OnSceneChanged();
    }
}