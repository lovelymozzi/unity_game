using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    [System.Serializable]
    public class GameData
    {
        // The amount of in-game currency (coins)
        public int Coin { get; set; }

        // List of islands that have been unlocked (true = unlocked, false = locked)
        public List<bool> UnlockedIslands { get; set; }

        // List of purchasers that available
        public List<PurchaserData> Purchasers { get; set; }

        // Database of inventory data for various inventories
        public List<InventoryData> InventoryDatabase { get; set; }

        // Database of silo data for various silos
        public List<SiloData> SiloDatabase { get; set; }

        // Levels of animals indexed by animal type
        public int[] AnimalLevels { get; set; }

        /// <summary>
        /// Initializes a new instance of the GameData class with specified values.
        /// </summary>
        /// <param name="coin">The initial amount of coins.</param>
        /// <param name="unlockedIslands">List indicating which islands are unlocked.</param>
        public GameData(int coin, List<bool> unlockedIslands)
        {
            Coin = coin;
            UnlockedIslands = unlockedIslands;
            Purchasers = new List<PurchaserData>();
            InventoryDatabase = new List<InventoryData>();
            SiloDatabase = new List<SiloData>();
            AnimalLevels = new int[System.Enum.GetValues(typeof(AnimalType)).Length];
        }
    }

    [System.Serializable]
    public class InventoryData
    {
        // Unique identifier for the inventory
        public string InventoryId { get; set; }

        // List of items in the inventory
        public List<Item> Items { get; set; }

        /// <summary>
        /// Initializes a new instance of the InventoryData class with specified values.
        /// </summary>
        /// <param name="inventoryId">The unique identifier for the inventory.</param>
        /// <param name="items">List of items contained in the inventory.</param>
        public InventoryData(string inventoryId, List<Item> items)
        {
            InventoryId = inventoryId;
            Items = items;
        }
    }

    [System.Serializable]
    public class SiloData
    {
        // Unique identifier for the silo
        public string SiloId { get; set; }

        // Current level of the silo
        public int SiloLevel { get; set; }

        // Levels of farmers associated with this silo
        public List<int> FarmerLevels { get; set; }

        /// <summary>
        /// Initializes a new instance of the SiloData class with specified values.
        /// </summary>
        /// <param name="siloId">The unique identifier for the silo.</param>
        /// <param name="siloLevel">The current level of the silo.</param>
        /// <param name="farmerLevels">List of levels for farmers associated with the silo.</param>
        public SiloData(string siloId, int siloLevel, List<int> farmerLevels)
        {
            SiloId = siloId;
            SiloLevel = siloLevel;
            FarmerLevels = farmerLevels;
        }
    }

    [System.Serializable]
    public class PurchaserData
    {
        // Location of the Purchaser.
        public int x { get; set; }
        public int y { get; set; }
        public int z { get; set; }
        public Vector3Int Position => new Vector3Int(x, y, z);

        // The current price to be deducted during the purchase.
        public int Price { get; set; }

        public PurchaserData(Vector3Int position, int price)
        {
            x = position.x;
            y = position.y;
            z = position.z;
            this.Price = price;
        }
    }
}
