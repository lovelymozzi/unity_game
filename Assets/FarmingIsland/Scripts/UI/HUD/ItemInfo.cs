using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class ItemInfo : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amount;

        private ItemData data;

        public void Init(ItemData data)
        {
            this.data = data;
        }

        public void UpdateInfo(Item item)
        {
            icon.sprite = data.Icon;
            amount.text = item.Amount.ToAbbreviatedString();
        }
    }
}
