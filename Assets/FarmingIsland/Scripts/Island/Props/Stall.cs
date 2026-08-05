using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class Stall : MonoBehaviour, IProp
    {
        [Tooltip("Maximum time allowed for selling all items at the stall.")]
        [SerializeField] private float maxPurchaseTime = 3f;

        [Tooltip("Transform representing the merchant character at the stall.")]
        [SerializeField] private Transform merchant;

        [Tooltip("List of items that can be accepted and sold at this stall.")]
        [SerializeField] private List<ItemData> acceptedItems;

        public List<ItemData> AcceptedItems => acceptedItems;
        public Location Location => Location.Stall;

        void Awake()
        {
            // Initialize stall and merchant scales to zero for initial animation.
            transform.localScale = Vector3.zero;
            merchant.localScale = Vector3.zero;
        }

        void OnEnable()
        {
            // Register all accepted items to the pool
            acceptedItems.ForEach(item => PoolManager.Instance.RegisterItem(item));
        }

        /// <summary>
        /// Handles the animation sequence for revealing the stall and merchant.
        /// </summary>
        /// <param name="instant">Whether the animation should be instant or gradual.</param>
        /// <param name="onFinished">Callback to invoke when the animation is complete.</param>
        public void Animate(bool instant, System.Action onFinished)
        {
            if (instant)
            {
                // Instantly scale both the stall and merchant to full size.
                transform.localScale = Vector3.one;
                merchant.localScale = Vector3.one;
                onFinished?.Invoke();
                return;
            }

            // Animate the stall's scale with a bounce effect, followed by the merchant.
            transform.DOScale(Vector3.one, 1f)
                .SetEase(Ease.OutBounce)
                .OnComplete(() =>
                {
                    merchant.DOScale(Vector3.one, 0.5f)
                        .SetEase(Ease.OutBounce);

                    onFinished?.Invoke();
                });
        }

        void OnTriggerEnter(Collider other)
        {
            // Start selling items when the player enters the stall's trigger zone.
            if (other.CompareTag("Player"))
            {
                var inventory = other.GetComponent<PlayerController>().Inventory;
                StartCoroutine(SellItems(inventory));
            }
        }

        void OnTriggerExit(Collider other)
        {
            // Stop selling items if the player leaves the stall's trigger zone.
            if (other.CompareTag("Player"))
            {
                StopAllCoroutines();
            }
        }

        /// <summary>
        /// Coroutine for handling the process of selling items from the player's inventory.
        /// Items are sold over time until either all valid items are sold or the maximum time is reached.
        /// </summary>
        IEnumerator SellItems(Inventory inventory)
        {
            float elapsedTime = 0f; // Tracks the elapsed time during selling.
            float initialPitch = 1f; // The starting pitch for the sound effect.
            float finalPitch = 3f; // The ending pitch for the sound effect.

            // Get the list of items in the inventory that are accepted by the stall.
            var validItems = inventory.Items.Where(i => acceptedItems.Contains(i.Data)).ToList();

            while (validItems.Count > 0 && elapsedTime < maxPurchaseTime)
            {
                var item = validItems.FirstOrDefault(); // Select the first valid item.

                float timeRatio = Mathf.Clamp01(elapsedTime / maxPurchaseTime); // Calculate the progress ratio.
                float remainingTime = maxPurchaseTime - elapsedTime;

                // Calculate how much of the item should be sold based on time and amount remaining.
                int deductionAmount = Mathf.Max(1, Mathf.RoundToInt((item.Amount * (1 - timeRatio)) / remainingTime));
                int actualDeduction = Mathf.Min(deductionAmount, item.Amount);

                // Increase the player's coin count and update the inventory.
                IslandManager.Instance.Coin += actualDeduction * item.Data.Price;
                inventory.SubtractItem(item.ItemId, actualDeduction);

                // Refresh the list of valid items after each sale.
                validItems = inventory.Items.Where(i => acceptedItems.Contains(i.Data)).ToList();

                // Adjust the pitch of the sound effect as the selling progresses.
                float currentPitch = Mathf.Lerp(initialPitch, finalPitch, timeRatio);
                AudioManager.Instance.PlaySFX(AudioID.Pop, pitch: currentPitch);

                // Visualize the sale by animating the dropped item.
                var drop = PoolManager.Instance.SpawnObject(item.Data.Drop.name).transform;
                drop.position = inventory.transform.TransformPoint(Vector3.up * 2f);
                AnimateDropItem(drop);

                yield return new WaitForSeconds(0.05f); // Small delay between item sales.

                elapsedTime += Time.deltaTime; // Increment the elapsed time.
            }
        }

        /// <summary>
        /// Animates the dropping of a sold item in front of the stall.
        /// </summary>
        /// <param name="drop">The transform of the dropped item.</param>
        void AnimateDropItem(Transform drop)
        {
            Vector3 stallFront = transform.TransformPoint(Vector3.forward * 3f); // Target position in front of the stall.
            Vector3 randomPos = (stallFront + Random.insideUnitSphere).Flatten(); // Randomize the exact drop location.

            // Animate the drop with a jump effect followed by a bounce.
            drop.DOJump(randomPos, 2f, 1, 0.5f)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    drop.DOJump(drop.position, 1f, 1, 0.5f)
                        .SetEase(Ease.OutBounce)
                        .OnComplete(() =>
                        {
                            PoolManager.Instance.ReturnObject(drop.gameObject); // Return the drop object to the pool.
                            AnimateCoin(drop.position); // Trigger the coin animation.
                        });
                });
        }

        /// <summary>
        /// Animates the appearance of a coin at the given spawn point.
        /// </summary>
        /// <param name="spawnPoint">The location where the coin should appear.</param>
        void AnimateCoin(Vector3 spawnPoint)
        {
            var coin = PoolManager.Instance.SpawnObject("Coin").transform;
            coin.position = spawnPoint;

            AudioManager.Instance.PlaySFX(AudioID.Coin); // Play the coin sound effect.
            coin.DOJump(coin.position, 0.5f, 2, 0.5f)
                .SetEase(Ease.Linear)
                .OnComplete(() => PoolManager.Instance.ReturnObject(coin.gameObject)); // Return the coin object to the pool.
        }
    }
}
