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
    public class SceneLoadManager : MonoBehaviour, IManager
    {
        public SceneName PrevSceneName { get; private set; }

		public static SceneLoadManager Get() { return GameManager.Instance.Get<SceneLoadManager>(); }

        public void Initialize() { }
		public void OnSceneChanged() { }
        public void Release() { }

        public SceneName GetCurrentScene()
        {
            return (SceneName)Enum.Parse(typeof(SceneName), SceneManager.GetActiveScene().name);
        }

        public void LoadScene(SceneName sceneName, bool isLoadingScene = false)
        {
			PrevSceneName = sceneName;

			StartCoroutine(LoadSceneAsync(sceneName.ToString(), isLoadingScene));
        }

        private IEnumerator LoadSceneAsync(string sceneName, bool isLoadingScene)
        {
            EventUtility.TriggerEvent(this, new Event_SceneState(SceneState.Load_SceneChangeEnter));

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
        }
    }
}
