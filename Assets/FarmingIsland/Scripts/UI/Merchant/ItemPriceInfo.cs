using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class ItemPriceInfo : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text priceText;

        public void Initialize(ItemData itemData)
        {
            iconImage.sprite = itemData.Icon;
            priceText.text = itemData.Price.ToAbbreviatedString();
        }
    }
}
