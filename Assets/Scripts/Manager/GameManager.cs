using System;
using System.Collections.Generic;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 하나의 그룹에서 필요한 매니저를 생성하고 초기화와 종료를 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour, IManager
    {
        private static GameManager instance;
        private static bool IsQuitting;
        private readonly Dictionary<Type, IManager> ManagerContainer = new();
        private readonly List<IManager> InitializedManagers = new();
        private Transform ManagerGroup;
        private bool IsInitialized;
        private bool IsReleased;

        public static GameManager Instance
        {
            get
            {
                if (instance == null)
                {
                    if (IsQuitting == true)
                    {
                        throw new InvalidOperationException("종료 중에는 매니저를 생성할 수 없습니다.");
                    }

                    instance = FindAnyObjectByType<GameManager>();

                    if (instance == null)
                    {
                        new GameObject(nameof(GameManager)).AddComponent<GameManager>();
                    }
                    else
                    {
                        instance.Initialize();
                    }
                }

                return instance;
            }
        }

        /// <summary>
        /// 새 객체를 생성하지 않고 현재 실행 중인 매니저가 있는지 확인합니다.
        /// </summary>
        public static bool TryGetInstance(out GameManager manager)
        {
            manager = instance;
            return manager != null && manager.IsReleased == false;
        }

        /// <summary>
        /// 등록된 매니저를 조회하며 없으면 씬의 인스턴스를 사용하거나 그룹 아래에 생성합니다.
        /// </summary>
        public T Get<T>() where T : BaseManager
        {
            if (ManagerContainer.TryGetValue(typeof(T), out var existing) == true &&
                existing is T registered && registered != null)
            {
                return registered;
            }

            if (IsReleased == true || IsQuitting == true)
            {
                throw new InvalidOperationException("종료 중에는 매니저를 생성할 수 없습니다.");
            }

            var managers = FindObjectsByType<T>(FindObjectsInactive.Exclude);
            T manager = null;

            for (var i = 0; i < managers.Length; i++)
            {
                if (manager == null)
                {
                    manager = managers[i];
                }
                else
                {
                    Destroy(managers[i]);
                }
            }

            if (manager == null)
            {
                var managerObject = new GameObject(typeof(T).Name);
                managerObject.transform.SetParent(ManagerGroup, false);
                manager = managerObject.AddComponent<T>();
            }
            RegisterManager(manager);
            return manager;
        }

        /// <summary>
        /// 씬에서 생성된 매니저도 동일한 등록과 중복 제거 경로를 사용합니다.
        /// </summary>
        internal void RegisterManager(BaseManager manager)
        {
            if (ManagerContainer.TryGetValue(manager.GetType(), out var existing) == true)
            {
                if (ReferenceEquals(existing, manager) == true)
                {
                    InitializeManager(manager);
                }
                else
                {
                    manager.enabled = false;
                    Destroy(manager);
                }

                return;
            }

            if (IsReleased == true || IsQuitting == true)
            {
                throw new InvalidOperationException("종료 중에는 매니저를 등록할 수 없습니다.");
            }

            ManagerContainer.Add(manager.GetType(), manager);

            if (manager.gameObject != gameObject)
            {
                manager.transform.SetParent(ManagerGroup, false);
            }

            InitializeManager(manager);
        }

        internal void RemoveManager(BaseManager manager)
        {
            if (ManagerContainer.TryGetValue(manager.GetType(), out var existing) == false ||
                ReferenceEquals(existing, manager) == false)
            {
                return;
            }

            ManagerContainer.Remove(manager.GetType());
            InitializedManagers.Remove(manager);
            manager.Release();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            Initialize();
        }

        public void Initialize()
        {
            if (IsInitialized == true)
            {
                return;
            }

            instance = this;
            IsInitialized = true;
            var managers = GetComponentsInChildren<MonoBehaviour>(true);
            var group = GameObject.Find("ManagerGroup") ?? new GameObject("ManagerGroup");
            ManagerGroup = group.transform;
            ManagerGroup.SetParent(null);
            if (ManagerGroup != transform)
            {
                transform.SetParent(ManagerGroup, false);
            }
            DontDestroyOnLoad(group);
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 60;

            for (var i = 0; i < managers.Length; i++)
            {
                var component = managers[i];

                if (component == this || component is not IManager manager)
                {
                    continue;
                }

                if (ManagerContainer.TryAdd(component.GetType(), manager) == false)
                {
                    Destroy(component);
                    continue;
                }

                if (component.gameObject != gameObject)
                {
                    component.transform.SetParent(ManagerGroup, false);
                }
            }

            // 전부 등록한 뒤 초기화하여 서로 조회해도 같은 인스턴스를 반환합니다.
            var registered = new List<IManager>(ManagerContainer.Values);

            for (var i = 0; i < registered.Count; i++)
            {
                InitializeManager(registered[i]);
            }
        }

        private void InitializeManager(IManager manager)
        {
            if (InitializedManagers.Contains(manager) == true)
            {
                return;
            }

            InitializedManagers.Add(manager);
            manager.Initialize();
        }

        public void Release()
        {
            if (IsReleased == true)
            {
                return;
            }

            IsReleased = true;

            for (var i = 0; i < InitializedManagers.Count; i++)
            {
                var component = (MonoBehaviour)InitializedManagers[i];
                component.enabled = false;
                component.StopAllCoroutines();
            }

            for (var i = InitializedManagers.Count - 1; i >= 0; i--)
            {
                InitializedManagers[i].Release();
            }

            InitializedManagers.Clear();
            ManagerContainer.Clear();
            ObjectPool.ReleasePool();
            PoolGroup.Release();
        }

        public void OnSceneChanged()
        {
            var managers = InitializedManagers.ToArray();
            for (var i = 0; i < managers.Length; i++)
            {
                // UI는 전환 시작 이벤트에서 먼저 연출을 취소합니다.
                if (managers[i] is UIManager)
                {
                    continue;
                }
                managers[i].OnSceneChanged();
            }
        }

        private void OnApplicationQuit()
        {
            IsQuitting = true;
            Release();
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            Release();
            instance = null;

            if (ManagerGroup != null && ManagerGroup != transform)
            {
                Destroy(ManagerGroup.gameObject);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            IsQuitting = false;
        }
    }
}
