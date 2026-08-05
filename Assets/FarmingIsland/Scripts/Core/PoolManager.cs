using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public class PoolManager : MonoBehaviour
    {
        // Lazy initialization singleton instance for global access
        private static PoolManager _instance;
        public static PoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PoolManager>();

                    if (_instance == null)
                    {
                        Debug.LogError("No PoolManager found in the scene!");
                    }
                }

                return _instance;
            }
        }

        // Dictionary to store pools for different prefab names
        private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();

        private void Awake()
        {
            // Ensure this is the only instance (Singleton pattern)
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }

            // Set capacity for DOTween tweens to optimize performance
            DG.Tweening.DOTween.SetTweensCapacity(200, 150);

            // Register the 'Coin' item
            RegisterItem(ItemDatabase.GetItemDataById("Coin"));
        }

        /// <summary>
        /// Registers an item in the pool with a specified size.
        /// Creates a pool of inactive game objects (prefabs) to improve performance by reusing objects.
        /// </summary>
        /// <param name="item">The item data that contains the prefab.</param>
        /// <param name="size">The initial size of the pool (default 100).</param>
        public void RegisterItem(ItemData item, int size = 100)
        {
            // Check if a pool for this item already exists
            if (pools.ContainsKey(item.Drop.name))
            {
                // Debug.LogWarning($"Pool for item '{item.Drop.name}' already exists. Skipping registration.");
                return; // Exit early to avoid registering the same item again
            }

            // Create a new pool
            Queue<GameObject> objectPool = new Queue<GameObject>();

            // Instantiate 'size' number of objects and add them to the pool
            for (int i = 0; i < size; i++)
            {
                GameObject obj = Instantiate(item.Drop, transform);
                obj.name = item.Drop.name; // Set name for easier identification
                obj.SetActive(false); // Keep it inactive until needed
                objectPool.Enqueue(obj); // Add object to the pool queue
            }

            // Add this pool to the dictionary
            pools.Add(item.Drop.name, objectPool);
        }

        /// <summary>
        /// Retrieves an object from the pool. If the pool is empty, a new object is instantiated.
        /// </summary>
        /// <param name="prefabName">The name of the prefab to retrieve from the pool.</param>
        /// <returns>An active GameObject from the pool or a newly instantiated one.</returns>
        public GameObject SpawnObject(string prefabName)
        {
            // Check if the requested pool exists
            if (!pools.ContainsKey(prefabName))
            {
                Debug.LogWarning("Pool with name " + prefabName + " doesn't exist! Setup a Stall that accepts " + prefabName + " first!");
                return null;
            }

            Queue<GameObject> objectPool = pools[prefabName];

            // If pool is empty, instantiate a new object
            if (objectPool.Count == 0)
            {
                Debug.LogWarning("Instantiating " + prefabName + " because pool is empty! Consider increasing initial pool size.");

                ItemData newItem = ItemDatabase.GetItemDataById(prefabName);
                GameObject newObj = Instantiate(newItem.Drop, transform);

                newObj.name = prefabName; // Ensure the name is set correctly
                return newObj;
            }

            // Dequeue an object from the pool and return it
            GameObject obj = objectPool.Dequeue();
            obj.SetActive(true); // Activate it before returning
            return obj;
        }

        /// <summary>
        /// Returns a GameObject back to its pool, deactivating it for future reuse.
        /// </summary>
        /// <param name="obj">The object to return to the pool.</param>
        public void ReturnObject(GameObject obj)
        {
            obj.SetActive(false); // Deactivate the object

            string prefabName = obj.name; // Use the name to identify its pool

            // Check if a pool exists for this object and add it back to the pool
            if (pools.ContainsKey(prefabName))
            {
                pools[prefabName].Enqueue(obj);
            }
            else
            {
                Debug.LogWarning("No pool found for object: " + prefabName);
            }
        }
    }
}
