using UnityEngine;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class CoinDisplay : MonoBehaviour
    {
        // Reference to the TextMeshProUGUI component for displaying the coin amount.
        private TextMeshProUGUI displayText;

        void Awake()
        {
            // Get and cache the TextMeshProUGUI component attached to this GameObject.
            displayText = GetComponent<TextMeshProUGUI>();
        }

        void Start()
        {
            // Subscribe to the coin change event in the IslandManager.
            // This ensures the display is updated whenever the player's coin amount changes.
            IslandManager.Instance.OnCoinChanged += UpdateDisplay;

            // Initialize the display with the current coin amount.
            UpdateDisplay(IslandManager.Instance.Coin);
        }

        /// <summary>
        /// Updates the displayed coin amount on the UI.
        /// </summary>
        /// <param name="coin">The current coin amount.</param>
        void UpdateDisplay(int coin)
        {
            // Format the coin amount and update the text display.
            displayText.text = coin.ToAbbreviatedString();
        }
    }
}
