using System.Collections;
using UnityEngine;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class Purchaser : MonoBehaviour
    {
        [Tooltip("Maximum duration allowed for completing the purchase process.")]
        [SerializeField] private float maxPurchaseTime = 5f;

        [Tooltip("Text component displaying the purchase price.")]
        [SerializeField] private TMP_Text priceText;

        [Tooltip("Particle effect that plays when the purchase is completed.")]
        [SerializeField] private ParticleSystem waterSplashPrefab;

        private Island island; // Reference to the island that will be activated upon purchase.
        private int price; // The current price to be deducted during the purchase.
        private PurchaserData purchaserData;

        /// <summary>
        /// Initializes the Purchaser with the target island and its purchase price.
        /// </summary>
        /// <param name="island">The island being purchased.</param>
        /// <param name="price">The price of the island.</param>
        public void Init(Island island, int price, PurchaserData purchaserData)
        {
            this.island = island;
            this.price = price;
            this.purchaserData = purchaserData;
        }

        void Start()
        {
            // Display the initial purchase price in the text component.
            priceText.text = price.ToAbbreviatedString();
        }

        void OnTriggerEnter(Collider other)
        {
            // Start the purchase process if the player enters the trigger zone.
            if (other.CompareTag("Player"))
            {
                StartCoroutine(BeginPurchase());
            }
        }

        void OnTriggerExit(Collider other)
        {
            // Stop the purchase process if the player exits the trigger zone.
            if (other.CompareTag("Player"))
            {
                StopAllCoroutines();
            }
        }

        /// <summary>
        /// Coroutine that handles the timed purchase process. Deducts coins from the player
        /// over time until the purchase is complete or the time runs out.
        /// </summary>
        IEnumerator BeginPurchase()
        {
            float elapsedTime = 0f; // Tracks the elapsed time during the purchase.
            float initialPitch = 1f; // The starting pitch for the sound effect.
            float finalPitch = 3f; // The ending pitch for the sound effect.

            while (price > 0 && IslandManager.Instance.Coin > 0 && elapsedTime < maxPurchaseTime)
            {
                // Calculate the current progress as a ratio of time passed.
                float timeRatio = Mathf.Clamp01(elapsedTime / maxPurchaseTime);

                // Calculate the amount to deduct based on the remaining price and time left.
                int deductionAmount = Mathf.Max(1, Mathf.RoundToInt((price * (1 - timeRatio)) / (maxPurchaseTime - elapsedTime)));
                int actualDeduction = Mathf.Min(deductionAmount, price, IslandManager.Instance.Coin);

                // Deduct the calculated amount from the price and the player's coins.
                price -= actualDeduction;
                purchaserData.Price = price;
                IslandManager.Instance.Coin -= actualDeduction;

                // Update the displayed price.
                priceText.text = price.ToAbbreviatedString();

                // Adjust the pitch of the sound effect based on the time ratio.
                float currentPitch = Mathf.Lerp(initialPitch, finalPitch, timeRatio);
                AudioManager.Instance.PlaySFX(AudioID.Pop, pitch: currentPitch);

                // Wait until the next frame.
                yield return new WaitForEndOfFrame();

                // Increment the elapsed time.
                elapsedTime += Time.deltaTime;
            }

            // If the price is not fully paid off, exit the coroutine.
            if (price > 0) yield break;

            // Play the water splash effect when the purchase is completed.
            Instantiate(waterSplashPrefab, transform.position, Quaternion.identity);

            // Activate the island and destroy this Purchaser object.
            island.Activate();
            Destroy(gameObject);
        }
    }
}
