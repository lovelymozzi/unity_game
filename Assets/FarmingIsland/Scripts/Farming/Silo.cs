using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cinemachine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class Silo : MonoBehaviour, IProp
    {
        [Tooltip("The minimum distance between the player and the silo for the upgrade menu to appear.")]
        [SerializeField] private float nearDistance = 2.5f;

        [Tooltip("The maximum level the silo can reach through upgrades.")]
        [SerializeField] private int maxLevel = 20;

        [Tooltip("The base capacity for storing crops in the silo (per crop).")]
        [SerializeField] private int baseCapacity = 100;

        [Tooltip("The base price for upgrading the silo (increases exponentially).")]
        [SerializeField] private int basePrice = 1500;

        [Tooltip("The price increase factor for upgrades.")]
        [SerializeField] private float priceFactor = 1.1f;

        [Tooltip("The maximum time required for the player to withdraw all crops from the silo.")]
        [SerializeField] private float maxWithdrawTime = 5f;

        [Tooltip("The virtual camera that will be activated when the player is about to upgrade.")]
        [SerializeField] private CinemachineVirtualCamera virtualCamera;

        [Header("Farmer Settings")]
        [Tooltip("Prefab of the farmer that will be instantiated based on the number of farms assigned to this silo.")]
        [SerializeField] private Farmer farmerPrefab;

        [Tooltip("The maximum level each farmer can achieve through upgrades.")]
        [SerializeField] private int farmerMaxLevel = 10;

        [Tooltip("The base capacity for crops that each farmer can carry from the farm to the silo.")]
        [SerializeField] private int farmerBaseCapacity = 50;

        [Tooltip("The base price for upgrading each farmer (price increases exponentially).")]
        [SerializeField] private int farmerBasePrice = 1200;

        [Tooltip("The factor by which the price increases for each farmer upgrade.")]
        [SerializeField] private float farmerPriceFactor = 1.2f;

        [Tooltip("List of farms assigned to the farmers.")]
        [SerializeField] private List<Farm> farms = new List<Farm>();

        private PlayerController player;
        private Inventory inventory;

        private string siloId;
        private SiloData data;

        private int capacity => baseCapacity * data.SiloLevel;
        private int upgradePrice => data.SiloLevel < maxLevel ? (int)(basePrice * Mathf.Pow(priceFactor, data.SiloLevel - 1)) : -1;

        public Location Location => Location.Silo;

        // For storing all farmers data
        private List<FarmerData> farmerDatabase = new List<FarmerData>();

        private bool isWithdrawing;
        private bool isUpgrading;

        void Awake()
        {
            transform.localScale = Vector3.zero;
            inventory = GetComponent<Inventory>();
        }

        void Start()
        {
            // Generate a unique identifier for the silo
            siloId = $"{gameObject.name} {(transform.parent != null ? transform.parent.name : "")}";

            // Load silo data using the generated siloId
            data = IslandManager.Instance.LoadSiloData(siloId);

            // If no data is found for this siloId, create new silo data with default values
            if (data == null)
            {
                // Initialize farmer levels to 0 (not hired) for each farm in the list
                var farmerLevels = Enumerable.Repeat(0, farms.Count).ToList();

                // Create new SiloData with an initial level of 1 and the initialized farmer levels
                data = new SiloData(siloId, 1, farmerLevels);
            }

            // Iterate through each farm to set up the corresponding farmer
            for (int index = 0; index < farms.Count; index++)
            {
                var farm = farms[index]; // Get the current farm
                int level = 0; // Default level for a new farmer

                // Check if the current index has a level assigned in the loaded data
                if (index < data.FarmerLevels.Count)
                {
                    level = data.FarmerLevels[index]; // Use the existing level
                }
                else
                {
                    data.FarmerLevels.Add(level); // Add the default level if not present
                }

                // Create a FarmerData object with the relevant parameters
                var farmerData = new FarmerData(
                    level,                      // Current level of the farmer
                    farmerMaxLevel,             // Maximum level the farmer can achieve
                    farmerBaseCapacity,         // Base capacity for carrying crops
                    farmerBasePrice,            // Base price for upgrading the farmer
                    farmerPriceFactor,          // Price increase factor for upgrades
                    farm,                       // The farm assigned to the farmer
                    this                        // Reference to the current silo
                );

                // Add the created FarmerData to the farmer database
                farmerDatabase.Add(farmerData);

                // Add an initial entry for the crop associated with the farm to the inventory with a count of 0.
                // This allows the SiloInfo to display the items that might be stored in this silo.
                inventory.AddItem(farm.CropData.ItemId, 0);

                // If the farmer level is greater than 0 (already hired), start a coroutine to spawn the farmer
                if (data.FarmerLevels[index] > 0) StartCoroutine(SpawnFarmer(index));
            }

            // Subscribe to the OnSaveGame event of the IslandManager instance to sync SiloData.
            IslandManager.Instance.OnSaveGame += (gameData) =>
            {
                // Find the saved silo data from the game data using the current siloId.
                var savedSiloData = gameData.SiloDatabase.FirstOrDefault(db => db.SiloId == siloId);

                // Check if there is existing silo data with the same siloId.
                if (savedSiloData != null)
                {
                    // If found, update the existing silo data with the current data.
                    savedSiloData = data;
                }
                else
                {
                    // If not found, add the current silo data to the SiloDatabase in the game data.
                    gameData.SiloDatabase.Add(data);
                }
            };
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // Retrieve the PlayerController component
                player = other.GetComponent<PlayerController>();

                // Display the silo information UI (SiloInfo) at the current position of the silo.
                UIManager.Instance.SiloInfo.Show(transform.position, inventory.Items, capacity);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // Set the player reference to null, as the player is no longer within the trigger.
                player = null;

                // Hide the silo information UI, as the player has exited the trigger area.
                UIManager.Instance.SiloInfo.Hide();
            }
        }

        void Update()
        {
            // Exit early if no player is currently assigned.
            if (player == null) return;

            // Check if the player is near the silo (within nearDistance).
            if (IsPlayerNear())
            {
                // If the player is near and there are crops stored in silo (items exist in the inventory),
                if (inventory.SumItems() > 0)
                {
                    // If the player is not currently withdrawing crops or upgrading,
                    // start the coroutine to withdraw crops.
                    if (!isWithdrawing && !isUpgrading) StartCoroutine(WithdrawCrop());
                }
                else
                {
                    // If the inventory is empty and the player is not upgrading,
                    // display the upgrade option.
                    if (!isUpgrading) ShowUpgrade();
                }
            }
            else
            {
                // If the player is not near and the upgrade option is currently shown,
                // hide the upgrade option.
                if (isUpgrading) HideUpgrade();
            }
        }

        public void Animate(bool instant, System.Action onFinished)
        {
            if (instant)
            {
                transform.localScale = Vector3.one;
                onFinished?.Invoke();
            }
            else
            {
                transform.DOScale(Vector3.one, 1f)
                    .SetEase(Ease.OutBounce)
                    .OnComplete(() => onFinished?.Invoke());
            }
        }

        bool IsPlayerNear()
        {
            float distance = Vector3.Distance(player.transform.position, transform.position);
            return distance <= nearDistance;
        }

        void ShowUpgrade()
        {
            // Define an action for upgrading the silo.
            // This will be called when the player confirms the silo upgrade.
            System.Action onSiloUpgrade = () =>
            {
                // Deduct the upgrade price from the player's coins.
                IslandManager.Instance.Coin -= upgradePrice;

                // Increment the silo level in the data.
                data.SiloLevel++;

                // Refresh the upgrade UI to reflect the new silo level and upgrade price.
                ShowUpgrade();

                // Play a sound effect to indicate the purchase.
                AudioManager.Instance.PlaySFX(AudioID.Cash);
            };

            // Define an action for upgrading a farmer.
            // This will be called when the player confirms a farmer upgrade.
            System.Action<int> onFarmerUpgrade = (index) =>
            {
                // Retrieve the farmer data for the specified index.
                var farmer = farmerDatabase[index];

                // If the farmer's level is 0 (not hired), start the coroutine to spawn the farmer.
                if (farmer.Level == 0) StartCoroutine(SpawnFarmer(index));

                // Deduct the farmer's upgrade price from the player's coins.
                IslandManager.Instance.Coin -= farmer.UpgradePrice;

                // Increment the farmer's level and the corresponding entry in the silo data.
                farmer.Level++;
                data.FarmerLevels[index]++;

                // Refresh the upgrade UI to reflect the new farmer level and upgrade price.
                ShowUpgrade();

                // Play a sound effect to indicate the purchase.
                AudioManager.Instance.PlaySFX(AudioID.Cash);
            };

            // Show the silo upgrade UI with the current silo level, capacity, upgrade price,
            // and the actions defined for silo and farmer upgrades.
            UIManager.Instance.SiloUpgrade.Show(data.SiloLevel, capacity, upgradePrice, onSiloUpgrade,
                farmerDatabase, onFarmerUpgrade
            );

            // Set the upgrading flag to true to indicate that the upgrade UI is currently active.
            isUpgrading = true;

            // Hide the silo information UI (SiloInfo) since the upgrade UI is now visible.
            UIManager.Instance.SiloInfo.Hide();

            // Activate the virtual camera to focus on the silo.
            virtualCamera.gameObject.SetActive(true);
        }

        void HideUpgrade()
        {
            UIManager.Instance.SiloUpgrade.Hide();

            // Set the upgrading flag to false to indicate that the upgrade UI is no longer active.
            isUpgrading = false;

            // Show the silo information UI (SiloInfo) at the silo position,
            // This re-displays the silo information now that the upgrade UI is hidden.
            UIManager.Instance.SiloInfo.Show(transform.position, inventory.Items, capacity);

            // Deactivate the virtual camera, which was focused on the silo.
            virtualCamera.gameObject.SetActive(false);
        }

        IEnumerator SpawnFarmer(int index)
        {
            // Wait until the NavMesh is available for the island to ensure proper farmer spawning.
            yield return new WaitUntil(() => IslandManager.Instance.HasNavMesh);

            // Get the position of the island to determine where to spawn the farmer.
            var islandPos = GetComponentInParent<Island>().GetPosition();

            // Instantiate a new farmer prefab at the island's position with the current silo's rotation.
            var farmer = Instantiate(farmerPrefab, islandPos, transform.rotation);

            // Name the newly instantiated farmer for identification purposes.
            farmer.gameObject.name = $"Farmer_{index}_{islandPos}";

            // Initialize the newly instantiated farmer with the data from the farmer database.
            farmer.Initialize(farmerDatabase[index]);
        }

        public bool DepositCrop(string itemId)
        {
            // Check if the inventory has reached its capacity for the specified item.
            if (inventory.CountItem(itemId) >= capacity) return false;

            // Add the specified item to the inventory.
            inventory.AddItem(itemId);

            // If there is a player present, update the silo information UI with the current inventory items and capacity.
            if (player != null)
                UIManager.Instance.SiloInfo.UpdateCropInfos(inventory.Items, capacity);

            // Return true to indicate that the crop was successfully deposited.
            return true;
        }

        IEnumerator WithdrawCrop()
        {
            // Set the flag indicating that a withdrawal process is in progress.
            isWithdrawing = true;

            // Initialize variables to track the elapsed time and pitch settings for sound effects.
            float elapsedTime = 0f;
            float initialPitch = 1f;
            float finalPitch = 3f;

            // Loop while the player is present, there are crops in silo, and the elapsed time is less than the maximum allowed withdraw time.
            while (player != null && inventory.SumItems() > 0 && elapsedTime < maxWithdrawTime)
            {
                // Get the first crop item in the inventory with a non-zero amount.
                var cropItem = inventory.Items.FirstOrDefault(i => i.Amount > 0);

                // Calculate the ratio of elapsed time to the maximum withdraw time.
                float timeRatio = Mathf.Clamp01(elapsedTime / maxWithdrawTime);
                float remainingTime = maxWithdrawTime - elapsedTime;

                // Determine the amount of crop to withdraw, ensuring it's at least 1 and does not exceed the crop item's available amount.
                int deductionAmount = Mathf.Max(1, Mathf.RoundToInt((cropItem.Amount * (1 - timeRatio)) / remainingTime));
                int actualDeduction = Mathf.Min(deductionAmount, cropItem.Amount);

                // Add the deducted amount to the player's inventory.
                player.Inventory.AddItem(cropItem.ItemId, actualDeduction);

                // Subtract the deducted amount from the silo inventory.
                inventory.SubtractItem(cropItem.ItemId, actualDeduction, false);

                // Update the silo information UI with the current inventory state.
                UIManager.Instance.SiloInfo.UpdateCropInfos(inventory.Items, capacity);

                // Calculate the current pitch for the sound effect based on the time ratio.
                float currentPitch = Mathf.Lerp(initialPitch, finalPitch, timeRatio);

                // Play the sound effect with the current pitch.
                AudioManager.Instance.PlaySFX(AudioID.Pop, pitch: currentPitch);

                // Spawn a crop object from the pool and position it above the ground.
                var crop = PoolManager.Instance.SpawnObject(cropItem.Data.Drop.name).transform;
                crop.position = transform.TransformPoint(Vector3.up * 2f);

                AnimateCrop(crop);

                // Wait for a short period before continuing the loop.
                yield return new WaitForSeconds(0.05f);

                // Increment the elapsed time by the time taken for this frame.
                elapsedTime += Time.deltaTime;
            }

            // Reset the flag to indicate that the withdrawal process is complete.
            isWithdrawing = false;
        }

        void AnimateCrop(Transform crop)
        {
            Vector3 siloFront = transform.TransformPoint(Vector3.forward * 3f);
            Vector3 randomPos = (siloFront + Random.insideUnitSphere).Flatten();

            crop.DOJump(randomPos, 2f, 1, 0.5f)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    crop.DOJump(crop.position, 1f, 1, 0.5f)
                        .SetEase(Ease.OutBounce)
                        .OnComplete(() =>
                        {
                            PoolManager.Instance.ReturnObject(crop.gameObject);
                        });
                });
        }
    }

    public class FarmerData
    {
        public int Level { get; set; }

        private int maxLevel;
        private int baseCapacity;
        private int basePrice;
        private float priceFactor;

        public int Capacity => baseCapacity + (Level * 10);
        public int UpgradePrice => CalculateUpgradePrice();
        public Farm Farm { get; private set; }
        public Silo Silo { get; private set; }
        public Vector3Int FarmIslandPosition { get; private set; }
        public Vector3Int SiloIslandPosition { get; private set; }

        public FarmerData(int level, int maxLevel, int baseCapacity, int basePrice, float priceFactor, Farm farm, Silo silo)
        {
            Level = level;
            this.maxLevel = maxLevel;
            this.baseCapacity = baseCapacity;
            this.basePrice = basePrice;
            this.priceFactor = priceFactor;
            Farm = farm;
            Silo = silo;

            FarmIslandPosition = IslandManager.Instance.GetCurrentIsland(farm.transform.position).GetPosition();
            SiloIslandPosition = IslandManager.Instance.GetCurrentIsland(silo.transform.position).GetPosition();
        }

        private int CalculateUpgradePrice()
        {
            if (Level >= maxLevel) return -1;
            return (int)(basePrice * Mathf.Pow(priceFactor, Level));
        }
    }
}
