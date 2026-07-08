using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Hwi.Foundation.Pool
{
    /// <summary>
    /// 단일 prefab 풀. UnityEngine.Pool.ObjectPool 위의 wrapping.
    /// Editor에서는 collectionCheck로 이중 release를 검출, 빌드에서는 자동 off.
    /// Get/Release 순서는 LIFO (UnityEngine.Pool.ObjectPool의 디폴트 Stack 동작).
    /// </summary>
    public sealed class PrefabPool : IDisposable
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly ObjectPool<GameObject> _pool;
        private readonly HashSet<GameObject> _active = new HashSet<GameObject>();
        private bool _disposed;

        public int CountInactive => _pool.CountInactive;
        public int CountActive => _pool.CountActive;
        public int CountAll => _pool.CountAll;

        public PrefabPool(
            GameObject prefab,
            Transform parent = null,
            int defaultCapacity = 20,
            int maxSize = 100)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            _prefab = prefab;
            _parent = parent;

            _pool = new ObjectPool<GameObject>(
                createFunc: CreateInstance,
                actionOnGet:     go => go.SetActive(true),
                actionOnRelease: go => go.SetActive(false),
                actionOnDestroy: go => { if (go != null) UnityEngine.Object.Destroy(go); },
                collectionCheck: Application.isEditor,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        private GameObject CreateInstance()
        {
            var go = UnityEngine.Object.Instantiate(_prefab, _parent);
            go.SetActive(false);
            return go;
        }

        public GameObject Get()
        {
            var go = _pool.Get();
            _active.Add(go);
            return go;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            var go = _pool.Get();
            _active.Add(go);
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        public void Release(GameObject instance)
        {
            if (instance == null) return;
            _active.Remove(instance);
            _pool.Release(instance);
        }

        public void Prewarm(int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PrefabPool));
            if (count <= 0) return;
            var temp = new List<GameObject>(count);
            for (int i = 0; i < count; i++) temp.Add(_pool.Get());
            for (int i = 0; i < temp.Count; i++) _pool.Release(temp[i]);
        }

        public void Clear() => _pool.Clear();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var go in _active)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _active.Clear();
            _pool.Clear();
            _pool.Dispose();
        }
    }
}
