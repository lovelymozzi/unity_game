using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hwi.Foundation.Pool
{
    /// <summary>키(string)로 여러 PrefabPool을 관리. 다종 발사체·이펙트에 적합.</summary>
    public sealed class PrefabPoolGroup : IDisposable
    {
        private readonly Dictionary<string, PrefabPool> _pools = new Dictionary<string, PrefabPool>();
        private bool _disposed;

        public int Count => _pools.Count;

        public System.Collections.Generic.IReadOnlyCollection<string> Keys => _pools.Keys;

        public bool Contains(string key) =>
            !string.IsNullOrEmpty(key) && _pools.ContainsKey(key);

        public bool TryGet(string key, out PrefabPool pool)
        {
            if (!string.IsNullOrEmpty(key) && _pools.TryGetValue(key, out pool)) return true;
            pool = null;
            return false;
        }

        public bool GetCounts(string key, out int active, out int inactive, out int all)
        {
            if (!string.IsNullOrEmpty(key) && _pools.TryGetValue(key, out var pool))
            {
                active = pool.CountActive;
                inactive = pool.CountInactive;
                all = pool.CountAll;
                return true;
            }
            active = inactive = all = 0;
            return false;
        }

        public void Register(string key, GameObject prefab, Transform parent = null, int defaultCapacity = 20, int maxSize = 100)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key must not be null/empty", nameof(key));
            if (_pools.ContainsKey(key)) throw new ArgumentException($"key '{key}' already registered", nameof(key));
            _pools[key] = new PrefabPool(prefab, parent, defaultCapacity, maxSize);
        }

        public GameObject Get(string key)
        {
            if (!_pools.TryGetValue(key, out var pool))
            {
                Debug.LogError($"[PrefabPoolGroup] Unregistered key: '{key}'");
                return null;
            }
            return pool.Get();
        }

        public GameObject Get(string key, Vector3 position, Quaternion rotation)
        {
            if (!_pools.TryGetValue(key, out var pool))
            {
                Debug.LogError($"[PrefabPoolGroup] Unregistered key: '{key}'");
                return null;
            }
            return pool.Get(position, rotation);
        }

        public void Release(string key, GameObject instance)
        {
            if (_pools.TryGetValue(key, out var pool)) pool.Release(instance);
        }

        public bool Unregister(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_pools.TryGetValue(key, out var pool)) return false;
            _pools.Remove(key);
            pool.Dispose();
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var pool in _pools.Values) pool.Dispose();
            _pools.Clear();
        }
    }
}
