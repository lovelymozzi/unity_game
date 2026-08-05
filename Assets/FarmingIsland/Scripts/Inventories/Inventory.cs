using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public class Inventory : MonoBehaviour
    {
        public List<Item> Items { get; private set; } = new List<Item>();

        // Calculate the total amount of all items in the inventory
        public int SumItems() => Items.Sum(i => i.Amount);

        // Retrieve the amount of a specific item by its ID
        public int CountItem(string itemId)
        {
            var item = Items.FirstOrDefault(i => i.ItemId == itemId);
            return item?.Amount ?? 0;
        }

        private string inventoryId;

        private void Start()
        {
            // Generate a unique inventory ID based on the object's name and parent name
            inventoryId = $"{gameObject.name} {(transform.parent != null ? transform.parent.name : "")}";

            // Load saved inventory data
            var loadedInventoryData = IslandManager.Instance.LoadInventoryData(inventoryId);
            if (loadedInventoryData != null)
            {
                Items = loadedInventoryData.Items;

                // Update UI if this inventory belongs to a player
                if (gameObject.CompareTag("Player"))
                    UIManager.Instance.UpdateItemInfos(Items);
            }

            // Save inventory data when the game is saved
            IslandManager.Instance.OnSaveGame += (gameData) =>
            {
                var savedInventoryData = gameData.InventoryDatabase.FirstOrDefault(db => db.InventoryId == inventoryId);
                if (savedInventoryData != null)
                {
                    // Update existing inventory data
                    savedInventoryData.Items = Items;
                }
                else
                {
                    // Add new inventory data to the game database
                    var newInventoryData = new InventoryData(inventoryId, Items);
                    gameData.InventoryDatabase.Add(newInventoryData);
                }
            };
        }

        /// <summary>
        /// Adds a specified amount of an item to the inventory. If the item already exists in the inventory, 
        /// its amount is increased by the specified amount. If the item does not exist, it is added to the inventory.
        /// </summary>
        /// <param name="itemId">The unique identifier of the item to be added.</param>
        /// <param name="amount">The amount of the item to be added. Default is 1.</param>
        public void AddItem(string itemId, int amount = 1)
        {
            var existingItem = Items.FirstOrDefault(x => x.ItemId == itemId);

            if (existingItem != null) existingItem.Amount += amount;
            else Items.Add(new Item(itemId, amount));

            // Update UI if this inventory belongs to a player
            if (gameObject.CompareTag("Player"))
                UIManager.Instance.UpdateItemInfos(Items);
        }

        /// <summary>
        /// Removes a specified amount of an item from the inventory. If the item amount is reduced to zero or below, 
        /// it will be removed from the inventory if <paramref name="removeEmpty"/> is set to true. 
        /// </summary>
        /// <param name="itemId">The unique identifier of the item to be subtracted from the inventory.</param>
        /// <param name="amount">The amount of the item to be subtracted. Default is 1.</param>
        /// <param name="removeEmpty">Specifies whether to remove the item from the inventory if its amount reaches zero. Default is true.</param>
        public void SubtractItem(string itemId, int amount = 1, bool removeEmpty = true)
        {
            var targetItem = Items.FirstOrDefault(x => x.ItemId == itemId);

            if (targetItem != null)
            {
                if (targetItem.Amount > 0)
                {
                    targetItem.Amount -= amount;
                    targetItem.Amount = Mathf.Clamp(targetItem.Amount, 0, int.MaxValue);
                }
                else if (removeEmpty)
                {
                    Items.Remove(targetItem);
                }

                // Update UI if this inventory belongs to a player
                if (gameObject.CompareTag("Player"))
                    UIManager.Instance.UpdateItemInfos(Items);
            }
            else
            {
                Debug.LogWarning($"Item with ID: {itemId} not found in inventory.");
            }
        }
    }
}
