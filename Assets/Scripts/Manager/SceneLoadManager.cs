using System;
using System.Collections;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeSouls.Manager
{
    public class SceneLoadManager : BaseManager, IManager
    {
        public SceneName PrevSceneName { get; private set; }
        public bool IsLoading { get; private set; }

		public static SceneLoadManager Get() { return GameManager.Instance.Get<SceneLoadManager>(); }

        public override void Initialize() { }
		public override void OnSceneChanged() { }
        public override void Release() { }

        public SceneName GetCurrentScene()
        {
            return (SceneName)Enum.Parse(typeof(SceneName), SceneManager.GetActiveScene().name);
        }

        public void LoadScene(SceneName sceneName, bool isLoadingScene = false)
        {
            if (IsLoading == true)
            {
                throw new InvalidOperationException("씬 전환을 중복 요청할 수 없습니다.");
            }
            if (Application.CanStreamedLevelBeLoaded(sceneName.ToString()) == false)
            {
                throw new InvalidOperationException($"Build Settings에 씬을 등록하세요: {sceneName}");
            }
            PrevSceneName = GetCurrentScene();
            IsLoading = true;

			StartCoroutine(LoadSceneAsync(sceneName.ToString(), isLoadingScene));
        }

        private IEnumerator LoadSceneAsync(string sceneName, bool isLoadingScene)
        {
            EventUtility.TriggerEvent(this, new Event_SceneState(SceneState.Load_SceneChangeEnter));
            GameManager.Instance.OnSceneChanged();
            // 풀/씬/UI 객체의 Destroy와 구독 해제가 완료된 뒤 원본을 해제합니다.
            yield return null;
            AddressableManager.Get().SceneChangeReleaseAsset();

            AsyncOperation op = null;

            if (isLoadingScene == true)
            {
                op = SceneManager.LoadSceneAsync(StringConstants.LOADING_SCENE_NAME, LoadSceneMode.Single);

                yield return null;

                while (op.isDone == false)
                {
                    yield return null;
                }

                EventUtility.TriggerEvent(this, new Event_SceneState(SceneState.Load_LoadingScene));
            }

            op = isLoadingScene ? SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive) : SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            while(op.isDone == false)
            {
                yield return null;
            }


            if (isLoadingScene == true)
            {
                op = SceneManager.UnloadSceneAsync(StringConstants.LOADING_SCENE_NAME);
            }

            EventUtility.TriggerEvent(this, new Event_SceneState(SceneState.Load_SceneChange));

			while (op.isDone == false)
            {
                yield return null;
            }

            EventUtility.TriggerEvent(this, new Event_SceneState(SceneState.Load_SceneChangeComplete));
            IsLoading = false;
        }
    }
}
