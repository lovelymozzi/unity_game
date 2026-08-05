namespace CryingSnow.FarmingIsland
{
    [System.Serializable]
    public class Item
    {
        public string ItemId { get; private set; }
        public int Amount { get; set; }

        public ItemData Data => ItemDatabase.GetItemDataById(ItemId);

        public Item(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }
}
