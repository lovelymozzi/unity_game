using UnityEngine;
using Hwi.Foundation.Pool;

namespace Hwi.Foundation.Samples.PoolBullet
{
    /// <summary>Sample shooter. PrefabPool 사용 시연.</summary>
    public sealed class Shooter2D : MonoBehaviour
    {
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float fireInterval = 0.1f;
        [SerializeField] private float bulletSpeed = 10f;
        [SerializeField] private float bulletLifetime = 2f;
        [SerializeField] private int defaultCapacity = 32;
        [SerializeField] private int maxSize = 128;

        private PrefabPool _pool;
        private float _nextFireTime;

        private void Awake()
        {
            _pool = new PrefabPool(bulletPrefab, transform, defaultCapacity, maxSize);
            _pool.Prewarm(defaultCapacity);
        }

        private void OnDestroy()
        {
            _pool?.Dispose();
        }

        private void Update()
        {
            if (Time.time < _nextFireTime) return;
            _nextFireTime = Time.time + fireInterval;

            var dir = Random.insideUnitCircle.normalized;
            var bullet = _pool.Get(transform.position, Quaternion.identity);
            var rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = dir * bulletSpeed;
            StartCoroutine(ReleaseAfter(bullet, bulletLifetime));
        }

        private System.Collections.IEnumerator ReleaseAfter(GameObject bullet, float t)
        {
            yield return new WaitForSeconds(t);
            _pool.Release(bullet);
        }
    }
}
