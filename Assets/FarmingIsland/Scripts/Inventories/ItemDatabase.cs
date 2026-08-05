using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public static class ItemDatabase
    {
        private static Dictionary<string, ItemData> itemDataDictionary = new Dictionary<string, ItemData>();
        private static bool isInitialized = false;

        public static void Initialize()
        {
            if (isInitialized) return;

            foreach (ItemData itemData in Resources.LoadAll<ItemData>("Items"))
            {
                if (itemData != null && !string.IsNullOrEmpty(itemData.ItemId))
                {
                    if (!itemDataDictionary.ContainsKey(itemData.ItemId))
                    {
                        itemDataDictionary.Add(itemData.ItemId, itemData);
                    }
                    else
                    {
                        Debug.LogWarning($"Duplicate item ID {itemData.ItemId} found. Skipping...");
                    }
                }
            }

            isInitialized = true;
        }

        public static ItemData GetItemDataById(string itemId)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (itemDataDictionary.TryGetValue(itemId, out ItemData itemData))
            {
                return itemData;
            }
            else
            {
                Debug.LogWarning($"Item Data with ID: {itemId} not found.");
                return null;
            }
        }

        public static void ClearDatabase()
        {
            itemDataDictionary.Clear();
            isInitialized = false;
        }
    }
}
