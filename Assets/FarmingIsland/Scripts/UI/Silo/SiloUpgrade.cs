using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class SiloUpgrade : MonoBehaviour
    {
        [Tooltip("Displays the title with the current silo level.")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("Displays the current and upgraded storage capacity.")]
        [SerializeField] private TMP_Text capacityText;

        [Tooltip("Button to trigger the silo upgrade.")]
        [SerializeField] private Button upgradeButton;

        [Tooltip("Displays the price of the next upgrade.")]
        [SerializeField] private TMP_Text priceText;

        [Tooltip("Prefab used to display farmer upgrade options.")]
        [SerializeField] private FarmerUpgrade farmerUpgradePrefab;

        private List<FarmerUpgrade> farmerUpgrades = new List<FarmerUpgrade>(); // List to store instantiated FarmerUpgrade components.

        void Awake()
        {
            // Initially hide the UI until it is needed.
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Displays the silo upgrade UI with the given information.
        /// </summary>
        /// <param name="level">Current silo level.</param>
        /// <param name="capacity">Current silo capacity.</param>
        /// <param name="upgradePrice">Cost of the next upgrade.</param>
        /// <param name="onSiloUpgrade">Action triggered when the upgrade button is pressed.</param>
        /// <param name="farmers">List of farmers associated with the silo.</param>
        /// <param name="onFarmerUpgrade">Action triggered when a farmer is upgraded.</param>
        public void Show(int level, int capacity, int upgradePrice, System.Action onSiloUpgrade,
            List<FarmerData> farmers, System.Action<int> onFarmerUpgrade)
        {
            // Set the silo title with the current level.
            titleText.text = $"Silo (Lv. {level})";

            // Display the current capacity and indicate additional capacity from the upgrade.
            capacityText.text = $"Capacity: {capacity} {(upgradePrice > 0 ? "<color=#3FC36D>(+100)" : "")}";

            // Enable or disable the upgrade button based on the upgrade price and player's coin amount.
            upgradeButton.interactable = upgradePrice > 0 && IslandManager.Instance.Coin >= upgradePrice;
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(() => onSiloUpgrade?.Invoke());

            // Show the upgrade price or "MAX" if the upgrade is unavailable.
            priceText.text = upgradePrice > 0 ? upgradePrice.ToAbbreviatedString() : "MAX";

            // Loop through the farmers and display their respective upgrade options.
            for (int index = 0; index < farmers.Count; index++)
            {
                var farmer = farmers[index];
                FarmerUpgrade farmerUpgrade;

                // Reuse existing FarmerUpgrade components if available, otherwise instantiate new ones.
                if (index < farmerUpgrades.Count)
                {
                    farmerUpgrade = farmerUpgrades[index];
                }
                else
                {
                    farmerUpgrade = Instantiate(farmerUpgradePrefab, transform);
                    farmerUpgrades.Add(farmerUpgrade);
                }

                // Update the farmer upgrade UI with the relevant data.
                farmerUpgrade.UpdateDisplay(index, farmer, onFarmerUpgrade);
            }

            // Show the UI after setting up the data.
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the silo upgrade UI and cleans up any instantiated farmer upgrade components.
        /// </summary>
        public void Hide()
        {
            // Destroy all farmer upgrade UI elements and clear the list.
            farmerUpgrades.ForEach(f => Destroy(f.gameObject));
            farmerUpgrades.Clear();

            // Hide the main UI.
            gameObject.SetActive(false);
        }
    }
}
