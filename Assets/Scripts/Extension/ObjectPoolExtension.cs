using System.Collections.Generic;
using ThreeSouls.Global;
using UnityEngine;

namespace ThreeSouls.Extension
{
    public static class ObjectPoolExtensions
    {
	    /// <summary>
	    /// 풀 생성
	    /// </summary>
	    /// <param name="origin">Origin resource</param>
	    /// <param name="initialPoolSize">PoolSize</param>
	    /// <typeparam name="T">Component</typeparam>
		public static void CreatePool<T>(this T origin, int initialPoolSize = 1) where T : Component
		{
			ObjectPool.CreatePool(origin, initialPoolSize);
		}

		/// <summary>
		/// 풀 생성
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="initialPoolSize">PoolSize</param>
		public static void CreatePool(this GameObject origin, int initialPoolSize = 1)
		{
			ObjectPool.CreatePool(origin, initialPoolSize);
		}
		
		/// <summary>
		/// Pool이 생성 되어있는지 확인
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <returns>IsCreated</returns>
		public static bool IsPoolCreated(this GameObject origin)
		{
			return ObjectPool.IsPoolCreated(origin);
		}

		/// <summary>
		/// 오브젝트 스폰
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="parent">Spawn object parent</param>
		/// <param name="position">Spawn object position</param>
		/// <param name="rotation">Spawn object rotation</param>
		/// <returns>SpawnType</returns>
		public static GameObject SpawnByOrigin(this GameObject origin, Transform parent = null, Vector3 position = default, Quaternion rotation = default)
		{
			return ObjectPool.SpawnByOrigin(origin, parent, position, rotation);
		}

		/// <summary>
		/// 오브젝트 스폰
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="parent">Spawn object parent</param>
		/// <param name="position">Spawn object position</param>
		/// <param name="rotation">Spawn object rotation</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>SpawnType</returns>
		public static T SpawnByOrigin<T>(this T origin, Transform parent = null, Vector3 position = default, Quaternion rotation = default) where T : Component
		{
			return ObjectPool.SpawnByOrigin(origin, parent, position, rotation).GetComponent<T>();
		}
		
		/// <summary>
		/// 오브젝트 풀링
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <param name="isDestroy">풀에 넣은 오브젝트 Recycle시에 삭제 여부</param>
		public static void RecycleBySpawnObject(this GameObject prefab, bool isDestroy = true)
		{
			ObjectPool.RecycleBySpawnObject(prefab, isDestroy);
		}

		/// <summary>
		/// 오브젝트 풀링
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <param name="isDestroy">풀에 넣은 오브젝트 Recycle시에 삭제 여부</param>
		/// <typeparam name="T">Component</typeparam>
		public static void RecycleBySpawnObject<T>(this T prefab, bool isDestroy = true) where T : Component
		{
			ObjectPool.RecycleBySpawnObject(prefab, isDestroy);
		}
		
		/// <summary>
		/// 오브젝트 전체 풀링
		/// </summary>
		/// <param name="origin">Origin resource</param>
		public static void RecycleAllByOrigin(this GameObject origin)
		{
			ObjectPool.RecycleAllByOrigin(origin);
		}
		
		/// <summary>
		/// 오브젝트 전체 풀링
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		public static void RecycleAllByOrigin<T>(this T origin) where T : Component
		{
			ObjectPool.RecycleAllByOrigin(origin);
		}
		
		/// <summary>
		/// 스폰 확인
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <returns>IsSpawned</returns>
		public static bool IsSpawnedBySpawnObject(this GameObject prefab)
		{
			return ObjectPool.IsSpawnedBySpawnObject(prefab);
		}
		
		/// <summary>
		/// 풀에 있는 오브젝트의 수
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>Pool Object Count</returns>
		public static int CountPooledByOrigin<T>(this T origin) where T : Component
		{
			return ObjectPool.CountPooledByOrigin(origin);
		}
		
		/// <summary>
		/// 스폰한 오브젝트의 수
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>Spawn Object Count</returns>
		public static int CountSpawnedByOrigin<T>(this T origin) where T : Component
		{
			return ObjectPool.CountSpawnedByOrigin(origin);
		}
		
		/// <summary>
		/// 풀에 있는 모든 오브젝트
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="list">Object List</param>
		/// <param name="isAppendList">IsAppend</param>
		/// <returns>Object list</returns>
		public static List<GameObject> GetPooledByOrigin(this GameObject origin, List<GameObject> list, bool isAppendList)
		{
			return ObjectPool.GetPooledByOrigin(origin, list, isAppendList);
		}
		
		/// <summary>
		/// 풀에 있는 모든 오브젝트
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="list">Object List</param>
		/// <param name="isAppendList">IsAppend</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>Object list</returns>
		public static List<T> GetPooledByOrigin<T>(this T origin, List<T> list, bool isAppendList) where T : Component
		{
			return ObjectPool.GetPooledByOrigin(origin, list, isAppendList);
		}
		
		/// <summary>
		/// 스폰 되어 있는 오브젝트
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <param name="list">Object List</param>
		/// <param name="isAppendList">IsAppend</param>
		/// <returns>Object list</returns>
		public static List<GameObject> GetSpawnedBySpawnObject(this GameObject prefab, List<GameObject> list, bool isAppendList)
		{
			return ObjectPool.GetSpawnedBySpawnObject(prefab, list, isAppendList);
		}
		
		/// <summary>
		/// 스폰 되어 있는 오브젝트
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <param name="list">Object List</param>
		/// <param name="isAppendList">IsAppend</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>Object list</returns>
		public static List<T> GetSpawnedBySpawnObject<T>(this T prefab, List<T> list, bool isAppendList) where T : Component
		{
			return ObjectPool.GetSpawnedBySpawnObject(prefab, list, isAppendList);
		}
		
		/// <summary>
		/// 풀에 있는 오브젝트 삭제
		/// </summary>
		/// <param name="origin">Origin resource</param>
		public static void DestroyPooledByOrigin(this GameObject origin)
		{
			ObjectPool.DestroyPooledByOrigin(origin);
		}
		
		/// <summary>
		/// 풀에 있는 오브젝트 삭제
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		public static void DestroyPooledByOrigin<T>(this T origin) where T : Component
		{
			ObjectPool.DestroyPooledByOrigin(origin);
		}

		/// <summary>
		/// 스폰 되어 있는 오브젝트 삭제
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		public static void DestroySpawnedBySpawnObject(this GameObject prefab)
		{
			ObjectPool.DestroySpawnedBySpawnObject(prefab);
		}
		
		/// <summary>
		/// 스폰 되어 있는 오브젝트 삭제
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <typeparam name="T">Component</typeparam>
		public static void DestroySpawnedBySpawnObject<T>(this T prefab) where T : Component
		{
			ObjectPool.DestroySpawnedBySpawnObject(prefab);
		}
		
		/// <summary>
		/// 모든 오브젝트 삭제
		/// </summary>
		/// <param name="origin">Origin resource</param>
		public static void DestroyAllByOrigin(this GameObject origin)
		{
			ObjectPool.DestroyAllByOrigin(origin);
		}

		/// <summary>
		/// 모든 오브젝트 삭제
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		public static void DestroyAllByOrigin<T>(this T origin) where T : Component
		{
			ObjectPool.DestroyAllByOrigin(origin);
		}
		
		/// <summary>
		/// 모든 오브젝트 삭제
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		public static void DestroyAllBySpawnObject(this GameObject prefab)
		{
			ObjectPool.DestroyAllBySpawnObject(prefab);
		}
		
		/// <summary>
		/// 모든 오브젝트 삭제
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <typeparam name="T">Component</typeparam>
		public static void DestroyAllBySpawnObject<T>(this T prefab) where T : Component
		{
			ObjectPool.DestroyAllBySpawnObject(prefab);
		}
    }
}