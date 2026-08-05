using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    [CreateAssetMenu()]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private int price;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject drop;

        public string ItemId => itemId;
        public int Price => price;
        public Sprite Icon => icon;
        public GameObject Drop => drop;
    }
}
