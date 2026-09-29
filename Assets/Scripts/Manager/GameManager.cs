using System;
using System.Collections.Generic;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Manager
{
    public class GameManager : MonoBehaviour, IManager
    {
        private static GameManager instance;
        public static GameManager Instance => instance ? instance : (instance = FindAnyObjectByType<GameManager>());

        private readonly Dictionary<Type, IManager> ManagerContainer = new();

        public T Get<T>() where T : IManager
        {
            var managerType = typeof(T);
            
            if (ManagerContainer.TryGetValue(managerType, out var value) == false)
            {
                throw new Exception($" Manager not found type : {managerType}");
            }
            
            return (T)value;
        }
        
        private void Awake()
        {
            Initialize();
        }
        
        public void Initialize()
        {
            DontDestroyOnLoad(this);

            AppOption();
            BindManager();
            ManagerInitialize();
        }

        public void Release()
        {
            using var enumerator = ManagerContainer.GetEnumerator();

            while (enumerator.MoveNext())
            {
                enumerator.Current.Value.Release();
            }
            
            ObjectPool.ReleasePool();
        }

        public void OnSceneChanged()
        {
           
        }
        
        private void ManagerInitialize()
        {
            using var enumerator = ManagerContainer.GetEnumerator();

            while (enumerator.MoveNext())
            {
                enumerator.Current.Value.Initialize();
            }
        }
        
        private void BindManager()
        {
            var managers = GetComponentsInChildren<IManager>();

            if (managers == null)
            {
                return;
            }
            
            foreach (var t in managers)
            {
                var managerType = t.GetType();

                if (managerType == typeof(GameManager))
                {
                    continue;
                }

                ManagerContainer.TryAdd(managerType, t);
            }
        }
        
        private void AppOption()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 60;
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }
            
            Release();
            instance = null;
        }
    }
}
