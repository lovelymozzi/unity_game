using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(AnimalController))]
    public class Animal : MonoBehaviour
    {
        [Tooltip("The product that this animal produces (e.g., egg, milk, wool).")]
        [SerializeField] private ItemData product;

        [Tooltip("The type of animal, which determines production time.")]
        [SerializeField] private AnimalType type;

        [Tooltip("The number of items dropped when the product is collected.")]
        [SerializeField] private int amount = 4;

        [Tooltip("Offset for the product info UI relative to the animal.")]
        [SerializeField] private Vector3 infoOffset;

        private AudioSource audioSource;        // The audio source used to play sounds related to the animal.
        protected PlayerController player;      // Reference to the player interacting with the animal.
        protected AnimalController controller;  // Controller managing the animal's behavior.
        private ProductInfo productInfo;        // Displays product type and indicates when it's ready for collection.
        private bool hasProduct;                // Flag indicating whether the animal has a product ready for collection.

        protected virtual void Awake()
        {
            // Cache the required components.
            controller = GetComponent<AnimalController>();
            audioSource = GetComponent<AudioSource>();

            // Ensure the trigger is set correctly.
            GetComponent<SphereCollider>().isTrigger = true;
        }

        private IEnumerator Start()
        {
            // Wait for game data initialization.
            yield return new WaitForSeconds(1f);

            // Start the production cycle.
            StartCoroutine(Produce());
        }

        private void OnTriggerEnter(Collider other)
        {
            // Exit if product is not ready yet.
            if (!hasProduct) return;

            if (other.CompareTag("Player"))
            {
                // Cache the player reference if it hasn't been set.
                if (player == null)
                    player = other.GetComponent<PlayerController>();

                // Exit if the player is busy or cannot be controlled (doing other activities).
                if (player.IsWorking || !player.IsControllable) return;

                // Disable farming and activity buttons during the collection process.
                UIManager.Instance.DisableFarmingButton();
                UIManager.Instance.ToggleActivityButton(null, null);

                // Start the collection process.
                StartCoroutine(Collect());
            }
        }

        protected virtual IEnumerator Produce()
        {
            float interval = Barn.Instance.CalculateProductionTime(type);
            yield return new WaitForSeconds(interval);

            // Mark the product as ready for collection.
            hasProduct = true;

            // Display the product info UI or reactivate it if already present.
            if (productInfo == null)
                productInfo = UIManager.Instance.DisplayProductInfo(transform, infoOffset, product.Icon);
            else
                productInfo.gameObject.SetActive(true);
        }

        private IEnumerator Collect()
        {
            // Mark the product as collected.
            hasProduct = false;

            // Hide the product info UI.
            if (productInfo != null) productInfo.gameObject.SetActive(false);

            // Start any additional minigame interaction.
            yield return MiniGame();

            Vibration.Vibrate();

            // Play the collection sound (this animal sound).
            audioSource.Play();

            // Drop the specified amount of product items.
            for (int i = 0; i < amount; i++)
            {
                // Spawn the product object from the pool.
                var productDrop = PoolManager.Instance.SpawnObject(product.Drop.name).transform;

                // Set the initial position to the animal's position.
                productDrop.transform.position = transform.position;

                // Calculate a random nearby position for the item drop.
                var randomPos = transform.position + Random.insideUnitSphere.Flatten();

                // Animate the drop with a jump effect.
                productDrop.DOJump(randomPos, 2f, 1, 0.5f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        // Add a bounce effect on landing and then return the object to the pool.
                        productDrop.DOJump(productDrop.position, 1f, 1, 0.5f)
                            .SetEase(Ease.OutBounce)
                            .OnComplete(() =>
                            {
                                PoolManager.Instance.ReturnObject(productDrop.gameObject);
                                player.Inventory.AddItem(product.ItemId); // Add the product to the player's inventory.
                            });
                    });

                // Adjust pitch for a pop sound based on item count ratio.
                float ratio = Mathf.Clamp01((float)i / amount);
                float pitch = Mathf.Lerp(1f, 2f, ratio);
                AudioManager.Instance.PlaySFX(AudioID.Pop, pitch: pitch);

                // Wait between drops.
                yield return new WaitForSeconds(0.1f);
            }

            // Restart the production cycle.
            StartCoroutine(Produce());
        }

        // Virtual coroutine to handle any additional minigame logic.
        protected virtual IEnumerator MiniGame()
        {
            yield return null;
        }
    }
}
