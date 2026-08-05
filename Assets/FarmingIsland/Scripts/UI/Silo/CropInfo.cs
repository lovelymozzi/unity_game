using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class CropInfo : MonoBehaviour
    {
        [Tooltip("UI Image to display the crop's icon.")]
        [SerializeField] private Image iconImage;

        [Tooltip("SlicedFilledImage component to visually represent the crop's count as a fill.")]
        [SerializeField] private SlicedFilledImage countFill;

        [Tooltip("TextMeshPro component to display the crop's count and capacity.")]
        [SerializeField] private TMP_Text countText;

        /// <summary>
        /// Updates the UI elements with the crop's current information.
        /// </summary>
        /// <param name="crop">The crop item to display.</param>
        /// <param name="capacity">The maximum capacity for this crop.</param>
        public void UpdateInfo(Item crop, int capacity)
        {
            // Set the icon image to represent the crop type.
            iconImage.sprite = crop.Data.Icon;

            // Calculate the fill amount based on the crop's amount relative to its capacity.
            countFill.fillAmount = (float)crop.Amount / capacity;

            // Update the text to display the current amount and capacity.
            countText.text = $"{crop.Amount}/{capacity}";
        }
    }
}
