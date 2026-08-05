using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(AudioSource))]
    public class FishingSpot : MonoBehaviour, IProp
    {
        [Tooltip("Icon representing the fishing rod in the user interface.")]
        [SerializeField] private Sprite fishingRodIcon;

        [Tooltip("The virtual camera used to view the fishing spot.")]
        [SerializeField] private CinemachineVirtualCamera virtualCamera;

        [Tooltip("Prefab for the fishing bobber that appears on the water.")]
        [SerializeField] private GameObject bobberPrefab;

        [Tooltip("Minimum time required to catch a fish.")]
        [SerializeField] private float minCatchTime = 3f;

        [Tooltip("Maximum time required to catch a fish.")]
        [SerializeField] private float maxCatchTime = 7f;

        [Tooltip("Minimum time interval to spawn a fish.")]
        [SerializeField] private float minSpawnTime = 60f;

        [Tooltip("Maximum time interval to spawn a fish.")]
        [SerializeField] private float maxSpawnTime = 180f;

        [Tooltip("Radius within which fish will swim around their target position.")]
        [SerializeField] private float swimRadius = 2.0f;

        [Tooltip("Speed at which the fish swims.")]
        [SerializeField] private float swimSpeed = 1.5f;

        [Tooltip("Array of fish prefabs that can be used in the fishing mini game.")]
        [SerializeField] private FishController[] fishPrefabs;

        [Header("Sound Effects")]

        [Tooltip("Sound effect played when the fishing rod is used.")]
        [SerializeField] private AudioClip fishingRodSound;

        [Tooltip("Sound effect played when a fish is hooked.")]
        [SerializeField] private AudioClip fishHookedSound;

        [Tooltip("Sound effect played when a fish escapes.")]
        [SerializeField] private AudioClip fishEscapedSound;

        [Tooltip("Sound effect played when reeling in the fishing line.")]
        [SerializeField] private AudioClip reelingInSound;

        [Tooltip("Sound effect played when letting out the fishing line.")]
        [SerializeField] private AudioClip reelingOutSound;

        [Tooltip("Sound effect played when a fish is successfully caught.")]
        [SerializeField] private AudioClip fishCaughtSound;

        [Header("Advanced Fishing Settings")]

        [Tooltip("Base rate at which progress increases during the fishing minigame.")]
        [SerializeField] private float baseProgressIncreaseRate = 0.25f;

        [Tooltip("Base rate at which progress decreases during the fishing minigame.")]
        [SerializeField] private float baseProgressDecreaseRate = 0.15f;

        [Tooltip("Minimum amount by which tension increases when reeling in a fish.")]
        [SerializeField] private float minTensionIncrease = 0.01f;

        [Tooltip("Maximum amount by which tension increases when reeling in a fish.")]
        [SerializeField] private float maxTensionIncrease = 0.7f;

        [Tooltip("Base rate at which tension decreases when not reeling in a fish.")]
        [SerializeField] private float baseTensionDecreaseRate = 0.3f;

        public Location Location => Location.Fish;

        private AudioSource audioSource;

        private List<FishController> fishList = new List<FishController>();
        private bool isPlayerFishing;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void Animate(bool instant, System.Action onFinished)
        {
            for (int i = 0; i < fishPrefabs.Length; i++)
            {
                float spawnDelay = instant ? 0f : (i + 1) * 0.1f;
                StartCoroutine(SpawnFish(i, spawnDelay));
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (fishList.Count > 0 && other.CompareTag("Player"))
            {
                var player = other.GetComponent<PlayerController>();
                UIManager.Instance.ToggleActivityButton(fishingRodIcon, () => StartCoroutine(Fishing(player)));
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                UIManager.Instance.ToggleActivityButton(null, null);
            }
        }

        IEnumerator Fishing(PlayerController player)
        {
            isPlayerFishing = true;

            // Disable UI activity button and the virtual joystick during fishing
            UIManager.Instance.ToggleActivityButton(null, null);
            UIManager.Instance.SetActiveJoystick(false);
            UIManager.Instance.ToggleHUD(false);

            // Activate the virtual camera to focus on the fishing spot
            virtualCamera.gameObject.SetActive(true);

            // Temporarily disable player control to focus on fishing
            player.IsControllable = false;

            // Rotate the player to face the fishing spot
            Vector3 direction = (transform.position.Flatten() - player.transform.position.Flatten()).normalized;
            Quaternion look = Quaternion.LookRotation(direction);
            yield return player.transform.DORotateQuaternion(look, 0.3f)
                .WaitForCompletion();

            // Cast the fishing line and show the fishing rod
            player.SetAnimatorInt("FishingIndex", 1);
            player.FishingRod.SetActive(true);
            AudioManager.Instance.PlaySFX(fishingRodSound);

            // Spawn the fishing bobber at the specified position and animate it
            yield return new WaitForSeconds(0.5f);
            var bobberSpawnPosition = player.transform.TransformPoint(new Vector3(0.5f, 2.5f, -0.5f));
            var bobber = Instantiate(bobberPrefab, bobberSpawnPosition, Quaternion.identity).transform;
            float bobberOscillationHeight = transform.position.y + 0.1f;
            bobber.DOJump(transform.position, 5f, 1, 1f)
                .SetEase(Ease.OutQuint)
                .OnComplete(() => bobber.DOMoveY(bobberOscillationHeight, 1f).SetLoops(-1, LoopType.Yoyo));

            // Wait for a random amount of time before catching a fish
            float catchTime = Random.Range(minCatchTime, maxCatchTime);
            yield return new WaitForSeconds(catchTime);

            // Stop the bobber animation and destroy it
            DOTween.Kill(bobber);
            Destroy(bobber.gameObject);

            // Select a random fish and set its swim depth to simulate hooking
            var hookedFish = fishList[Random.Range(0, fishList.Count)];
            hookedFish.SwimDepth -= 1f;

            // Play the effects for hooking a fish
            AudioManager.Instance.PlaySFX(fishHookedSound);
            Vibration.Vibrate();

            // Start reeling sound effect using reeling out clip initially
            audioSource.loop = true;
            audioSource.clip = reelingOutSound;
            audioSource.Play();

            // Make all fish panic and swim deeper except the hooked one
            fishList.ForEach(fish =>
            {
                fish.IsPanic = true;
                if (fish != hookedFish) fish.SwimDepth -= 3f;
            });

            // Update UI for fishing progress and line tension
            player.SetAnimatorInt("FishingIndex", 2);
            UIManager.Instance.ProgressBar.Toggle("Catch the fish!");
            UIManager.Instance.LineTension.Toggle(true);

            float progress = 0.3f;
            float tension = 0.5f;
            bool isCaught = true;

            // Continue updating progress and tension while fishing
            while (progress < 1f && isCaught)
            {
                bool isReeling = Input.GetMouseButton(0);

                float tensionIncreaseRate = Random.Range(minTensionIncrease, maxTensionIncrease);

                // Increase or decrease progress and tension based on reeling status
                if (isReeling)
                {
                    progress += Time.deltaTime * baseProgressIncreaseRate;
                    tension += Time.deltaTime * tensionIncreaseRate;
                }
                else
                {
                    progress -= Time.deltaTime * baseProgressDecreaseRate;
                    tension -= Time.deltaTime * baseTensionDecreaseRate;
                }

                // Clamp values to ensure they stay within the valid range
                progress = Mathf.Clamp01(progress);
                tension = Mathf.Clamp01(tension);

                // Update UI with the current progress and tension values
                UIManager.Instance.ProgressBar.UpdateDisplay(progress);
                UIManager.Instance.LineTension.UpdateDisplay(tension);

                // Check if the fish has escaped or progress is zero
                if (progress <= 0f || tension >= 1f)
                {
                    isCaught = false;
                    AudioManager.Instance.PlaySFX(fishEscapedSound);
                }

                // Update reeling sound effect based on reeling status
                var reelingSound = isReeling ? reelingInSound : reelingOutSound;
                if (audioSource.clip != reelingSound)
                {
                    audioSource.clip = reelingSound;
                    audioSource.Play();
                }

                yield return null;
            }

            // Restore swim depth for all fish after the fishing attempt
            hookedFish.SwimDepth += 1f;
            fishList.ForEach(fish =>
            {
                fish.IsPanic = false;
                if (fish != hookedFish) fish.SwimDepth += 3f;
            });

            // Hide progress and tension UI elements
            UIManager.Instance.ProgressBar.Toggle("");
            UIManager.Instance.LineTension.Toggle(false);

            // Stop reeling sound and cleanup
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;

            player.SetAnimatorInt("FishingIndex", 3);

            if (isCaught)
            {
                // Catch the fish and play the corresponding sound effect
                hookedFish.Catch();
                AudioManager.Instance.PlaySFX(fishCaughtSound);

                // Move the caught fish near the player and add it to the inventory
                Vector3 catchPoint = player.transform.TransformPoint(Vector3.right);
                yield return hookedFish.transform.DOJump(catchPoint, 5f, 1, 1f)
                    .SetEase(Ease.InOutFlash)
                    .WaitForCompletion();

                yield return new WaitForSeconds(3f);

                player.Inventory.AddItem(hookedFish.FishItem.ItemId);

                // Spawn a new fish after a random delay and remove the caught fish from the list
                float spawnDelay = Random.Range(minSpawnTime, maxSpawnTime);
                StartCoroutine(SpawnFish(hookedFish.FishIndex, spawnDelay));

                fishList.Remove(hookedFish);
                Destroy(hookedFish.gameObject);
            }

            // Deactivate the virtual camera and restore UI and player control
            virtualCamera.gameObject.SetActive(false);

            yield return new WaitForSeconds(1f);

            player.FishingRod.SetActive(false);
            player.IsControllable = true;
            UIManager.Instance.SetActiveJoystick(true);
            UIManager.Instance.ToggleHUD(true);

            isPlayerFishing = false;
        }

        IEnumerator SpawnFish(int prefabIndex, float spawnDelay)
        {
            yield return new WaitForSeconds(spawnDelay);

            // Ensure that the player is not currently fishing before spawning the fish
            yield return new WaitWhile(() => isPlayerFishing);

            // Calculate a random position within the defined swim radius
            var randomPos = Random.insideUnitSphere.Flatten() * swimRadius;
            var spawnPos = transform.position + randomPos;

            // Instantiate the fish prefab at the calculated position
            var fish = Instantiate(fishPrefabs[prefabIndex], spawnPos, Quaternion.identity);
            fish.transform.SetParent(transform);
            fishList.Add(fish);

            // Initialize the fish with its specific settings
            fish.Initialize(prefabIndex, transform.position, swimRadius, swimSpeed);

            // If there was a delay before spawning (which occurs when a fishing spot is activated for the first time after a bridge purchase),
            // animate the fish's scale from zero to its normal size.
            if (spawnDelay > 0f)
            {
                fish.transform.localScale = Vector3.zero;
                fish.transform.DOScale(Vector3.one, 1f)
                    .SetEase(Ease.OutBounce);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, swimRadius);
        }
#endif
    }
}
