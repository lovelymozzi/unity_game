using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class FarmerUpgrade : MonoBehaviour
    {
        [Tooltip("Text component for displaying the farmer's title (e.g., Farmer 1 (Lv. X)).")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("Image component for displaying the crop icon associated with the farmer's farm.")]
        [SerializeField] private Image cropIcon;

        [Tooltip("Text component for displaying the farmer's current capacity.")]
        [SerializeField] private TMP_Text capacityText;

        [Tooltip("Button used to either hire or upgrade the farmer.")]
        [SerializeField] private Button upgradeButton;

        [Tooltip("Color used for the upgrade button when hiring a new farmer.")]
        [SerializeField] private Color hireColor;

        [Tooltip("Color used for the upgrade button when upgrading an existing farmer.")]
        [SerializeField] private Color upgradeColor;

        [Tooltip("Text component for displaying 'Upgrade' or 'Hire' on the button.")]
        [SerializeField] private TMP_Text upgradeText;

        [Tooltip("Text component for displaying the upgrade or hire price.")]
        [SerializeField] private TMP_Text priceText;

        /// <summary>
        /// Updates the farmer upgrade UI display based on the current farmer's data.
        /// </summary>
        /// <param name="index">The index of the farmer being displayed.</param>
        /// <param name="farmer">The FarmerData containing the farmer's current stats.</param>
        /// <param name="onFarmerUpgrade">Callback action for when the upgrade button is clicked.</param>
        public void UpdateDisplay(int index, FarmerData farmer, System.Action<int> onFarmerUpgrade)
        {
            int level = farmer.Level;
            Farm farm = farmer.Farm;
            int capacity = farmer.Capacity;
            int price = farmer.UpgradePrice;

            // Determine the title based on the farmer's level and farm readiness.
            string title = "No Farm Found";
            if (level > 0) title = $"Farmer {index + 1} (Lv. {level})";
            else if (farm.IsReady) title = "No Farmer Hired";
            titleText.text = title;

            // Set the crop icon associated with the farm.
            cropIcon.sprite = farm.CropData.Icon;

            // Display the capacity text and highlight potential upgrades if the farmer is at a level.
            if (level > 0)
                capacityText.text = $"Capacity: {capacity} {(price > 0 ? "<color=#3FC36D>(+10)</color>" : "")}";
            else
                capacityText.text = "";

            // Set up the upgrade button's appearance and interactivity.
            upgradeButton.interactable = price > 0 && IslandManager.Instance.Coin >= price && farm.IsReady;
            upgradeButton.image.color = level > 0 ? upgradeColor : hireColor;
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(() => onFarmerUpgrade?.Invoke(index));

            // Set the button text to either "Upgrade" or "Hire" depending on the farmer's status.
            upgradeText.text = level > 0 ? "Upgrade" : "Hire";

            // Display the price or "MAX" if no further upgrades are available.
            priceText.text = price > 0 ? price.ToAbbreviatedString() : "MAX";
        }
    }
}
