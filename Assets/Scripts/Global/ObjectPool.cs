using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreeSouls.Global
{
	public static class ObjectPool 
	{
		private static readonly Dictionary<GameObject, List<GameObject>> PooledContainer = new();
		private static readonly Dictionary<GameObject, GameObject> SpawnedContainer = new();
		private static readonly Dictionary<GameObject, GameObject> SpawnedParentContainer = new();
		private static GameObject ParentObject;

		/// <summary>
		/// 풀 생성
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="initialPoolSize">PoolSize</param>
		/// <typeparam name="T">Component</typeparam>
		public static void CreatePool<T>(T origin, int initialPoolSize) where T : Component
		{
			Create(origin.gameObject, initialPoolSize);
		}

		/// <summary>
		/// 풀 생성
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="initialPoolSize">PoolSize</param>
		public static void CreatePool(GameObject origin, int initialPoolSize)
		{
			Create(origin, initialPoolSize);
		}

		/// <summary>
		/// 오브젝트 스폰
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="parent">Spawn object parent</param>
		/// <param name="position">Spawn object position</param>
		/// <param name="rotation">Spawn object rotation</param>
		/// <returns>SpawnType</returns>
		public static GameObject SpawnByOrigin(GameObject origin, Transform parent, Vector3 position, Quaternion rotation)
		{
			return Spawn(origin, parent, position, rotation);
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
		public static T SpawnByOrigin<T>(T origin, Transform parent, Vector3 position, Quaternion rotation) where T : Component
		{
			return Spawn(origin.gameObject, parent, position, rotation).GetComponent<T>();
		}
		
		/// <summary>
		/// 오브젝트 풀링
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <param name="isDestroy">풀에 넣은 오브젝트 Recycle시에 삭제 여부</param>
		public static void RecycleBySpawnObject(GameObject prefab, bool isDestroy)
		{
			Recycle(prefab, isDestroy);
		}

		/// <summary>
		/// 오브젝트 풀링
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <param name="isDestroy">풀에 넣은 오브젝트 Recycle시에 삭제 여부</param>
		/// <typeparam name="T">Component</typeparam>
		public static void RecycleBySpawnObject<T>(T prefab, bool isDestroy) where T : Component
		{
			Recycle(prefab.gameObject, isDestroy);
		}
		
		/// <summary>
		/// 오브젝트 전체 풀링
		/// </summary>
		/// <param name="origin">Origin resource</param>
		public static void RecycleAllByOrigin(GameObject origin)
		{
			RecycleAll(origin);
		}
		
		/// <summary>
		/// 오브젝트 전체 풀링
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		public static void RecycleAllByOrigin<T>(T origin) where T : Component
		{
			RecycleAll(origin.gameObject);
		}
		
		/// <summary>
		/// 스폰 확인
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <returns>IsSpawned</returns>
		public static bool IsSpawnedBySpawnObject(GameObject prefab)
		{
			return SpawnedContainer.ContainsKey(prefab);
		}

		/// <summary>
		/// Pool이 생성 되어있는지 확인
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <returns>IsCreated</returns>
		public static bool IsPoolCreated(GameObject origin)
		{
			return PooledContainer.ContainsKey(origin);
		}

		/// <summary>
		/// 풀에 있는 오브젝트의 수
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>Pool Object Count</returns>
		public static int CountPooledByOrigin<T>(T origin) where T : Component
		{
			return PooledContainer.TryGetValue(origin.gameObject, out var list) ? list.Count : 0;
		}
		
		/// <summary>
		/// 스폰한 오브젝트의 수
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>Spawn Object Count</returns>
		public static int CountSpawnedByOrigin<T>(T origin) where T : Component
		{
			return SpawnedContainer.Values.Count(x => x == origin.gameObject);
		}
		
		/// <summary>
		/// 전체 풀에 있는 오브젝트 수
		/// </summary>
		/// <returns>Total spawn Object Count</returns>
		public static int CountAllPooled()
		{
			return PooledContainer.Values.Sum(x => x.Count);
		}
		
		/// <summary>
		/// 풀에 있는 모든 오브젝트
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="list">Object List</param>
		/// <param name="isAppendList">IsAppend</param>
		/// <returns>Object list</returns>
		public static List<GameObject> GetPooledByOrigin(GameObject origin, List<GameObject> list, bool isAppendList)
		{
			list ??= new List<GameObject>();
			
			if (isAppendList == false)
			{
				list.Clear();
			}

			if (PooledContainer.TryGetValue(origin, out var pooled))
			{
				list.AddRange(pooled);
			}
			
			return list;
		}
		
		/// <summary>
		/// 풀에 있는 모든 오브젝트
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <param name="list">Object List</param>
		/// <param name="isAppendList">IsAppend</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>Object list</returns>
		public static List<T> GetPooledByOrigin<T>(T origin, List<T> list, bool isAppendList) where T : Component
		{
			list ??= new List<T>();
			
			if (isAppendList == false)
			{
				list.Clear();
			}

			if (PooledContainer.TryGetValue(origin.gameObject, out var pooled))
			{
				list.AddRange(pooled.Select(t => t.GetComponent<T>()));
			}
			
			return list;
		}
		
		/// <summary>
		/// 스폰 되어 있는 오브젝트
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <param name="list">Object List</param>
		/// <param name="isAppendList">IsAppend</param>
		/// <returns>Object list</returns>
		public static List<GameObject> GetSpawnedBySpawnObject(GameObject prefab, List<GameObject> list, bool isAppendList)
		{
			list ??= new List<GameObject>();
			
			if (isAppendList == false)
			{
				list.Clear();
			}

			list.AddRange(from item in SpawnedContainer where item.Value == prefab select item.Key);

			return list;
		}
		
		/// <summary>
		/// 스폰 되어 있는 오브젝트
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <param name="list">Object List</param>
		/// <param name="isAppendList">IsAppend</param>
		/// <typeparam name="T">Component</typeparam>
		/// <returns>Object list</returns>
		public static List<T> GetSpawnedBySpawnObject<T>(T prefab, List<T> list, bool isAppendList) where T : Component
		{
			list ??= new List<T>();
			
			if (isAppendList == false)
			{
				list.Clear();
			}

			list.AddRange(from item in SpawnedContainer where item.Value == prefab.gameObject select item.Key.GetComponent<T>());

			return list;
		}
		
		/// <summary>
		/// 풀에 있는 오브젝트 삭제
		/// </summary>
		/// <param name="origin">Origin resource</param>
		public static void DestroyPooledByOrigin(GameObject origin)
		{
			DestroyByOriginType(origin);
		}
		
		/// <summary>
		/// 풀에 있는 오브젝트 삭제
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		public static void DestroyPooledByOrigin<T>(T origin) where T : Component
		{
			DestroyByOriginType(origin.gameObject);
		}

		/// <summary>
		/// 스폰 되어 있는 오브젝트 삭제
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		public static void DestroySpawnedBySpawnObject(GameObject prefab)
		{
			DestroyByObject(prefab);
		}
		
		/// <summary>
		/// 스폰 되어 있는 오브젝트 삭제
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <typeparam name="T">Component</typeparam>
		public static void DestroySpawnedBySpawnObject<T>(T prefab) where T : Component
		{
			DestroyByObject(prefab.gameObject);
		}
		
		/// <summary>
		/// 모든 오브젝트 삭제
		/// </summary>
		/// <param name="origin">Origin resource</param>
		public static void DestroyAllByOrigin(GameObject origin)
		{
			DestroyAllByOriginType(origin);
		}

		/// <summary>
		/// 모든 오브젝트 삭제
		/// </summary>
		/// <param name="origin">Origin resource</param>
		/// <typeparam name="T">Component</typeparam>
		public static void DestroyAllByOrigin<T>(T origin) where T : Component
		{
			DestroyAllByOriginType(origin.gameObject);
		}
		
		/// <summary>
		/// 모든 오브젝트 삭제
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		public static void DestroyAllBySpawnObject(GameObject prefab)
		{
			DestroyAllByObject(prefab);
		}
		
		/// <summary>
		/// 모든 오브젝트 삭제
		/// </summary>
		/// <param name="prefab">Spawn Object</param>
		/// <typeparam name="T">Component</typeparam>
		public static void DestroyAllBySpawnObject<T>(T prefab) where T : Component
		{
			DestroyAllByObject(prefab.gameObject);
		}

		/// <summary>
		/// 모든 데이터 리셋
		/// </summary>
		public static void ReleasePool()
		{
			Release();
		}

		#region Engine
		private static void Create(GameObject origin, int initialPoolSize)
		{
			if (origin == null)
			{
				Debug.LogError($"Origin object null");
				return;
			}
			
			if (PooledContainer.ContainsKey(origin) == true)
			{
				return;
			}

			if (SpawnedParentContainer.ContainsKey(origin) == false)
			{
				CreateParentObject();

				var parentObject = new GameObject($"{origin.name} Group");
				
				parentObject.transform.SetParent(ParentObject.transform);
				parentObject.transform.localPosition = Vector3.zero;

				SpawnedParentContainer.Add(origin, parentObject);
			}

			var poolList = new List<GameObject>();
			
			for (var i = 0; i < initialPoolSize; i++)
			{
				var obj = UnityEngine.Object.Instantiate(origin, SpawnedParentContainer[origin].transform, true);
				obj.name =  origin.name;
				obj.SetActive(false);
				
				poolList.Add(obj);
			}
			
			PooledContainer.Add(origin, poolList);
		}
		
		private static GameObject Spawn(GameObject origin, Transform parent, Vector3 position, Quaternion rotation)
		{
			if (PooledContainer.TryGetValue(origin, out var poolList) == true)
			{
				if (poolList.Count > 0)
				{
					var poolObject = poolList[0];
					
					if (poolObject == null)
					{
						throw new Exception($"PooledContainer {origin.name} pool list index 0 : object null");
					}
					
					poolList.RemoveAt(0);
					
					poolObject.transform.SetParent(parent);
					poolObject.transform.localPosition = position;
					poolObject.transform.localRotation = rotation;
					poolObject.SetActive(true);

					SpawnedContainer.Add(poolObject, origin);
					return poolObject;
				}
				else
				{
					var instance = UnityEngine.Object.Instantiate(origin, ObjectPool.SpawnedParentContainer[origin].transform, true);
					instance.transform.SetParent(parent);
					
					instance.name = origin.name;
					instance.transform.localPosition = position;
					instance.transform.localRotation = rotation;
					instance.SetActive(true);
					
					SpawnedContainer.Add(instance, origin);
					return instance;
				}
			}
			else
			{
				throw new Exception("No data in object pool. Make sure you have created a pool");
			}
		}
		
		private static void Recycle(GameObject prefab, bool isDestroy = true)
		{
			if (prefab == null)
			{
				Debug.LogWarning($"Recycle object null or destroy");
				return;
			}

			if (SpawnedContainer.TryGetValue(prefab, out var origin) == true)
			{ 
				Recycle(origin, prefab);
			}
			else
			{
				if (isDestroy == false)
				{
					return;
				}
				
				Debug.LogWarning($"Pool object destroy [Object name : <color=white>{prefab.name}</color>]");
				UnityEngine.Object.Destroy(prefab);
			}
		}
		
		private static void Recycle(GameObject origin, GameObject prefab)
		{
			if (PooledContainer.ContainsKey(origin) == false)
			{
				Debug.LogError($"PooledContainer not found key [Key : <color=white>{origin}</color>]");
				return;
			}

			PooledContainer[origin].Add(prefab);
			SpawnedContainer.Remove(prefab);
			
			prefab.transform.SetParent(SpawnedParentContainer[origin].transform);
			prefab.transform.localPosition = Vector3.zero;
			prefab.SetActive(false);
		}
		
		private static void RecycleAll(GameObject origin)
		{
			if (origin == null)
			{
				Debug.LogError($"Origin object null");
				return;
			}


			var list = SpawnedContainer
				.Where(pair => pair.Value == origin)
				.Select(pair => pair.Key).ToList();

			for (var i = 0; i < list.Count; i++)
			{
				Recycle(origin, list[i]);
			}
		}
		
		private static void DestroyByOriginType(GameObject origin)
		{
			if (origin == null)
			{
				Debug.LogError($"Origin object null");
				return;
			}

			if (PooledContainer.TryGetValue(origin, out var poolList) == false)
			{
				return;
			}

			for (var i = 0; i < poolList.Count; i++)
			{
				UnityEngine.Object.Destroy(poolList[i]);
			}
		}

		private static void DestroyByObject(GameObject prefab)
		{
			if (prefab == null)
			{
				Debug.LogWarning($"spawn object not found");
				return;
			}

			if (SpawnedContainer.TryGetValue(prefab, out var origin) == false)
			{
				return;
			}

			var spawnList = SpawnedContainer.Where(x => x.Value == origin).Select(x => x.Key).ToList();

			for (var i = 0; i < spawnList.Count; i++)
			{
				UnityEngine.Object.Destroy(spawnList[i]);
				SpawnedContainer.Remove(spawnList[i]);
			}
		}

		private static void DestroyAllByOriginType(GameObject origin)
		{
			if (origin == null)
			{
				Debug.LogError($"Origin object null");
				return;
			}
			
			DestroyByOriginType(origin);
			DestroyByObject(SpawnedContainer.Where(x => origin == x.Value).Select(x => x.Key).FirstOrDefault());
		}

		private static void DestroyAllByObject(GameObject prefab)
		{
			if (prefab == null)
			{
				Debug.LogError($"Spawn object null");
				return;
			}
			
			DestroyByOriginType(SpawnedContainer.GetValueOrDefault(prefab));
			DestroyByObject(prefab);
		}

		
		private static void CreateParentObject()
		{
			if (ParentObject)
			{
				return;
			}
		
			ParentObject = new GameObject("ObjectPool");
			ParentObject.transform.position = new Vector3(1000, 1000);
			UnityEngine.Object.DontDestroyOnLoad(ParentObject);
		}
		
		private static void Release()
		{
			using var poolEnumerator = PooledContainer.GetEnumerator();

			while (poolEnumerator.MoveNext())
			{
				var list = poolEnumerator.Current.Value;
				
				for (var i = 0; i < list.Count; i++)
				{
					UnityEngine.Object.Destroy(list[i]);
				}

				list.Clear();
			}
			
			PooledContainer.Clear();
			
			using var spawnEnumerator = SpawnedContainer.GetEnumerator();

			while (spawnEnumerator.MoveNext())
			{
				UnityEngine.Object.Destroy(spawnEnumerator.Current.Key);
			}
			
			SpawnedContainer.Clear();

			using var parentEnumerator = SpawnedParentContainer.GetEnumerator();

			while (parentEnumerator.MoveNext())
			{
				UnityEngine.Object.Destroy(spawnEnumerator.Current.Value);
			}
		
			SpawnedParentContainer.Clear();
			
			UnityEngine.Object.Destroy(ParentObject);
		}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PooledContainer.Clear();
            SpawnedContainer.Clear();
            SpawnedParentContainer.Clear();
            ParentObject = null;
        }

        #endregion
    }
}
