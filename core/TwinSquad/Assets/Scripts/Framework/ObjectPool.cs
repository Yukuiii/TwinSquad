using System.Collections.Generic;
using UnityEngine;

namespace TwinSquad.Framework
{
    /// <summary>
    /// 池化对象生命周期回调（可选实现）。
    /// 池里的实例每次出/入池会调用一次。
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary>
    /// 实例上挂的标记组件，记录"我属于哪个 Pool"。
    /// PoolManager.Despawn 时通过它找到归属池。
    /// </summary>
    public class PoolMarker : MonoBehaviour
    {
        [HideInInspector] public GameObjectPool Pool;
    }

    /// <summary>
    /// 单个 prefab 对应的对象池（用 Stack 实现，LIFO）。
    /// </summary>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Stack<GameObject> _stack = new();
        private readonly Transform _root;

        public GameObject Prefab => _prefab;
        public int IdleCount => _stack.Count;

        public GameObjectPool(GameObject prefab, Transform root, int prewarm = 0)
        {
            _prefab = prefab;
            _root = root;
            for (int i = 0; i < prewarm; i++) _stack.Push(CreateInstance());
        }

        public GameObject Spawn(Vector3 pos, Quaternion rot, Transform parent = null)
        {
            var go = _stack.Count > 0 ? _stack.Pop() : CreateInstance();
            var t = go.transform;
            t.SetParent(parent != null ? parent : _root, false);
            t.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
            if (go.TryGetComponent<IPoolable>(out var p)) p.OnSpawn();
            return go;
        }

        public void Despawn(GameObject go)
        {
            if (go == null) return;
            if (go.TryGetComponent<IPoolable>(out var p)) p.OnDespawn();
            go.SetActive(false);
            go.transform.SetParent(_root, false);
            _stack.Push(go);
        }

        public void Clear()
        {
            while (_stack.Count > 0)
            {
                var go = _stack.Pop();
                if (go != null) Object.Destroy(go);
            }
        }

        private GameObject CreateInstance()
        {
            var go = Object.Instantiate(_prefab, _root);
            go.name = _prefab.name;
            go.SetActive(false);
            var marker = go.GetComponent<PoolMarker>() ?? go.AddComponent<PoolMarker>();
            marker.Pool = this;
            return go;
        }
    }

    /// <summary>
    /// 全局对象池入口。按 prefab 区分池子，业务零样板：
    ///     var enemy = PoolManager.Spawn(enemyPrefab, pos, rot);
    ///     PoolManager.Despawn(enemy);
    ///
    /// 设计原则：
    /// - 静态访问点，无需 MonoBehaviour
    /// - 实例挂 PoolMarker，Despawn 自动找池
    /// - 全部池实例放在 [PoolRoot] 下，避免污染场景层级
    /// - 跨场景持久（DontDestroyOnLoad）
    /// </summary>
    public static class PoolManager
    {
        private static readonly Dictionary<GameObject, GameObjectPool> _pools = new();
        private static Transform _root;

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("[PoolRoot]");
                    Object.DontDestroyOnLoad(go);
                    _root = go.transform;
                }
                return _root;
            }
        }

        public static void Prewarm(GameObject prefab, int count)
        {
            GetOrCreate(prefab, count);
        }

        public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[PoolManager] Spawn 失败：prefab 为空");
                return null;
            }
            return GetOrCreate(prefab, 0).Spawn(pos, rot, parent);
        }

        public static T Spawn<T>(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null) where T : Component
        {
            var go = Spawn(prefab, pos, rot, parent);
            return go != null ? go.GetComponent<T>() : null;
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (instance.TryGetComponent<PoolMarker>(out var marker) && marker.Pool != null)
            {
                marker.Pool.Despawn(instance);
            }
            else
            {
                // 不是池化对象，直接销毁
                Object.Destroy(instance);
            }
        }

        public static void Clear(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out var pool))
            {
                pool.Clear();
                _pools.Remove(prefab);
            }
        }

        public static void ClearAll()
        {
            foreach (var p in _pools.Values) p.Clear();
            _pools.Clear();
        }

        private static GameObjectPool GetOrCreate(GameObject prefab, int prewarm)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new GameObjectPool(prefab, Root, prewarm);
                _pools[prefab] = pool;
            }
            return pool;
        }
    }
}
