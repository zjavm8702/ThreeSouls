using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class ComponentPool<T> where T : Component
{
    private T Origin;
    private readonly Stack<T> Inactive = new();
    private readonly HashSet<T> Active = new();
    private GameObject PoolParent;

    public void CreatePool(T origin, int initialPoolSize = 1)
    {
        if (origin == null)
        {
            throw new ArgumentNullException(nameof(origin));
        }

        if (initialPoolSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialPoolSize));
        }

        if (Origin != null)
        {
            if (Origin != origin)
            {
                throw new InvalidOperationException("ComponentPool is already assigned to another origin.");
            }

            EnsurePoolParent();
            return;
        }

        Origin = origin;
        EnsurePoolParent();

        for (int i = 0; i < initialPoolSize; i++)
        {
            Inactive.Push(CreateInstance());
        }
    }

    public T Spawn(Transform parent, Vector3 position, Quaternion rotation = default)
    {
        if (Origin == null)
        {
            throw new InvalidOperationException("CreatePool must be called before Spawn.");
        }

        EnsurePoolParent();

        T instance = null;

        while (Inactive.Count > 0 && instance == null)
        {
            instance = Inactive.Pop();
        }

        if (instance == null)
        {
            instance = CreateInstance();
        }

        if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
        {
            rotation = Quaternion.identity;
        }

        instance.transform.SetParent(parent, false);
        instance.transform.SetLocalPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);
        Active.Add(instance);

        return instance;
    }

    public void Release()
    {
        DestroyPooled();
        DestroySpawned();

        if (PoolParent != null)
        {
            Object.Destroy(PoolParent);
        }

        PoolParent = null;
        Origin = null;
    }

    public bool IsPoolCreated()
    {
        return Origin != null && PoolParent != null;
    }

    public int CountPooled()
    {
        return Inactive.Count;
    }

    public void DestroyPooled()
    {
        while (Inactive.Count > 0)
        {
            var instance = Inactive.Pop();

            if (instance != null)
            {
                Object.Destroy(instance.gameObject);
            }
        }
    }

    public void DestroySpawned()
    {
        foreach (T instance in Active)
        {
            if (instance != null)
            {
                Object.Destroy(instance.gameObject);
            }
        }

        Active.Clear();
    }

    public void Recycle(T instance)
    {
        if (Active.Remove(instance) == false || instance == null)
        {
            return;
        }

        EnsurePoolParent();

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(PoolParent.transform, false);
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        Inactive.Push(instance);
    }

    private T CreateInstance()
    {
        var instance = Object.Instantiate(Origin, PoolParent.transform, false);
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void EnsurePoolParent()
    {
        if (PoolParent == null)
        {
            PoolParent = new GameObject($"{typeof(T).Name} Group");
        }
    }
}
