using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    /// <summary>
    /// Manages a farm, including soil tiles, planting, watering, and harvesting.
    /// </summary>
    public class Farm : MonoBehaviour, IProp
    {
        [SerializeField, Range(2, 6)]
        [Tooltip("Length of the farm grid (number of tiles in the X direction)")]
        private int length = 6;

        [SerializeField, Range(2, 6)]
        [Tooltip("Width of the farm grid (number of tiles in the Z direction)")]
        private int width = 5;

        [SerializeField] private Soil soilPrefab;
        [SerializeField] private CropData cropData;

#if UNITY_EDITOR
        [SerializeField]
        [Tooltip("Toggle to draw farm tiles in the editor (for debugging)")]
        private bool drawFarmTiles = true;
#endif

        [Header("Plant Settings")]

        [Tooltip("Distance at which planting can occur")]
        [SerializeField] private float plantingDistance = 2f;

        [Tooltip("Radius from the center of the area (farmer's forward direction + planting distance) within which planting occurs")]
        [SerializeField] private float plantingRadius = 1.2f;

        [Tooltip("Distance at which watering can occur")]
        [SerializeField] private float wateringDistance = 1.8f;

        [Tooltip("Radius from the center of the area (farmer's forward direction + watering distance) within which watering occurs")]
        [SerializeField] private float wateringRadius = 1f;

        [Tooltip("Distance at which harvesting can occur")]
        [SerializeField] private float harvestingDistance = 1.5f;

        [Tooltip("Radius from the center of the area (farmer's forward direction + harvesting distance) within which harvesting occurs")]
        [SerializeField] private float harvestingRadius = 0.9f;


        [Header("Soil Settings")]

        [Tooltip("Color for dry soil (first variant)")]
        [SerializeField] private Color soilDryColor1 = new Color(0.7725f, 0.5647f, 0.3176f, 1.0f);

        [Tooltip("Color for wet soil (first variant)")]
        [SerializeField] private Color soilWetColor1 = new Color(0.6031f, 0.4109f, 0.2737f, 1.0f);

        [Tooltip("Color for dry soil (second variant)")]
        [SerializeField] private Color soilDryColor2 = new Color(0.7300f, 0.5465f, 0.3285f, 1.0f);

        [Tooltip("Color for wet soil (second variant)")]
        [SerializeField] private Color soilWetColor2 = new Color(0.5600f, 0.3934f, 0.2744f, 1.0f);

        [Tooltip("Time duration for soil to remain wet")]
        [SerializeField] private float soilWetTime = 1f;

        [Tooltip("Time duration for soil to dry")]
        [SerializeField] private float soilDryTime = 0.5f;


        [Header("Growth Times")]

        [Tooltip("Time required for a sprout to grow")]
        [SerializeField] private float sproutGrowTime = 1f;

        [Tooltip("Time required for a plant to grow")]
        [SerializeField] private float plantGrowTime = 1f;

        [Tooltip("Time required for a crop to grow")]
        [SerializeField] private float cropGrowTime = 0.5f;

        #region Public Getters
        public CropData CropData => cropData;

        public float PlantingDistance => plantingDistance;
        public float PlantingRadius => plantingRadius;
        public float WateringDistance => wateringDistance;
        public float WateringRadius => wateringRadius;
        public float HarvestingDistance => harvestingDistance;
        public float HarvestingRadius => harvestingRadius;

        public Color SoilDryColor(int index) => (index == 0) ? soilDryColor1 : soilDryColor2;
        public Color SoilWetColor(int index) => (index == 0) ? soilWetColor1 : soilWetColor2;
        public float SoilWetTime => soilWetTime;
        public float SoilDryTime => soilDryTime;

        public float SproutGrowTime => sproutGrowTime;
        public float PlantGrowTime => plantGrowTime;
        public float CropGrowTime => cropGrowTime;

        public State GetState() => state;

        public bool IsReady => farmTrigger.enabled;

        public Location Location => Location.Farm;
        #endregion

        public enum State { None, Planted, Grown }  // Different states the farm can be in
        private State state;  // Current state of the farm

        private BoxCollider farmTrigger;  // Collider for farm interaction

        private int soilLayer;  // Layer mask for soil

        private List<Soil> soils = new List<Soil>();  // List of soil tiles in the farm

        private PlayerController player;  // Reference to the player or farmer interacting with the farm

        void Awake()
        {
            // Initialize soil layer and create farm trigger and soil tiles
            soilLayer = 1 << LayerMask.NameToLayer("Soil");
            CreateTrigger();
            CreateSoils();
        }

        void CreateTrigger()
        {
            // Create and configure the collider for farm interaction
            farmTrigger = gameObject.AddComponent<BoxCollider>();
            farmTrigger.isTrigger = true;
            farmTrigger.size = new Vector3(length, 2f, width);
            farmTrigger.center = (farmTrigger.size / 2f);
            farmTrigger.enabled = false;
        }

        void CreateSoils()
        {
            // Create and initialize soil tiles in the farm grid
            for (int x = 0; x < length; x++)
            {
                for (int z = 0; z < width; z++)
                {
                    Vector3 position = new Vector3(x + 0.5f, 0f, z + 0.5f);
                    var soil = Instantiate(soilPrefab, transform);
                    soil.transform.localPosition = position;
                    soil.Initialize(this);
                    soil.OnStateChanged += UpdateFarmState;
                    soils.Add(soil);
                    soil.gameObject.SetActive(false);
                }
            }
        }

        void IProp.Animate(bool instant, System.Action onFinished)
        {
            if (instant)
            {
                soils.ForEach(soil =>
                {
                    soil.gameObject.SetActive(true);
                    soil.DisableShadowCasting();
                });

                farmTrigger.enabled = true;
            }
            else
            {
                StartCoroutine(AnimateSoils());
            }
        }

        IEnumerator AnimateSoils()
        {
            // Animate soil tiles with a tweening effect
            foreach (var soil in soils)
            {
                soil.gameObject.SetActive(true);

                soil.transform.localScale = Vector3.zero;
                soil.transform.localPosition += Vector3.up * 2f;

                var sequence = DOTween.Sequence();
                float duration = 0.5f;
                sequence.Append(soil.transform.DOScale(Vector3.one, duration));
                sequence.Join(soil.transform.DOLocalMoveY(0f, duration));
                sequence.OnComplete(() => soil.DisableShadowCasting());

                yield return new WaitForSeconds(0.05f);
            }

            farmTrigger.enabled = true;
        }

        void UpdateFarmState()
        {
            // Update the state of the farm based on the states of the soil tiles
            switch (state)
            {
                case State.None:
                    HandleStateTransition(
                        () => soils.All(soil => soil.GetSoilState() != Soil.State.None),
                        () => soils.All(soil => soil.GetSoilState() == Soil.State.Sprout),
                        State.Planted
                    );
                    break;

                case State.Planted:
                    HandleStateTransition(
                        () => soils.All(soil => soil.GetSoilState() != Soil.State.Sprout),
                        () => soils.All(soil => soil.GetSoilState() == Soil.State.Harvest),
                        State.Grown
                    );
                    break;

                case State.Grown:
                    HandleStateTransition(
                        () => soils.All(soil => soil.GetSoilState() != Soil.State.Harvest),
                        () => soils.All(soil => soil.GetSoilState() == Soil.State.None),
                        State.None
                    );
                    break;
            }
        }

        void HandleStateTransition(System.Func<bool> stopWorkingCondition, System.Func<bool> transitionCondition, State nextFarmState)
        {
            // Handle the transition between farm states and update UI accordingly
            if (stopWorkingCondition())
            {
                // Disable farming button if player was not initially inside the trigger zone
                if (player != null)
                    UIManager.Instance.DisableFarmingButton();

                player?.SetWorking(null);
            }

            if (transitionCondition())
            {
                state = nextFarmState;

                if (player != null && player.IsControllable)
                    UIManager.Instance.ToggleFarmingButton(state, () => player?.SetWorking(this));
            }
        }

        void OnTriggerEnter(Collider other)
        {
            // Handle player entering the farm trigger zone
            if (other.CompareTag("Player"))
            {
                player = other.GetComponent<PlayerController>();

                UIManager.Instance.ToggleFarmingButton(state, () => player?.SetWorking(this));

                // Ensure the Farming Button is up-to-date with the farm state upon player entry
                UpdateFarmState();
            }
        }

        void OnTriggerExit(Collider other)
        {
            // Handle player exiting the farm trigger zone
            if (other.CompareTag("Player"))
            {
                UIManager.Instance.DisableFarmingButton();
                player.SetWorking(null);
                player = null;
            }
        }

        public void PlantSeeds(Transform farmer)
        {
            WorkOnNearbySoils(
                farmer,
                Soil.State.None,
                Soil.State.Seed,
                plantingDistance,
                plantingRadius
            );
        }

        public void WaterPlants(Transform farmer)
        {
            WorkOnNearbySoils(
                farmer,
                Soil.State.Sprout,
                Soil.State.Ripe,
                wateringDistance,
                wateringRadius
            );
        }

        public int HarvestPlants(Transform farmer)
        {
            return WorkOnNearbySoils(
                farmer,
                Soil.State.Harvest,
                Soil.State.Dead,
                harvestingDistance,
                harvestingRadius
            );
        }

        private int WorkOnNearbySoils(Transform farmer, Soil.State currentState, Soil.State newState, float workDistance, float workRadius)
        {
            // Work on nearby soil tiles based on their state and update their state
            Vector3 workPosition = farmer.TransformPoint(Vector3.forward * workDistance);

            var nearbySoils = Physics.OverlapSphere(
                workPosition,
                workRadius,
                soilLayer,
                QueryTriggerInteraction.Collide
            );

            int validSoils = 0;
            foreach (var nearbySoil in nearbySoils)
            {
                var soil = nearbySoil.GetComponent<Soil>();
                if (soil.GetSoilState() == currentState)
                {
                    soil.SetSoilState(newState, farmer);
                    validSoils++;
                }
            }

            return validSoils;
        }

        public Vector3 GetTargetSoilPosition()
        {
            // Get a random position of a soil tile based on the current farm state
            Soil targetSoil = soils[Random.Range(0, soils.Count)];
            Vector3 defaultPos = targetSoil.transform.position;

            Soil.State targetSoilState;
            switch (state)
            {
                case State.None:
                    targetSoilState = Soil.State.None;
                    break;
                case State.Planted:
                    targetSoilState = Soil.State.Sprout;
                    break;
                case State.Grown:
                    targetSoilState = Soil.State.Harvest;
                    break;
                default:
                    targetSoilState = Soil.State.None;
                    break;
            }

            targetSoil = soils.Where(s => s.GetSoilState() == targetSoilState)
                .OrderBy(s => Random.value)
                .FirstOrDefault();

            return targetSoil != null ? targetSoil.transform.position : defaultPos;
        }

        void OnDestroy()
        {
            // Unsubscribe from soil state change events
            soils.ForEach(soil => soil.OnStateChanged -= UpdateFarmState);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // Draw gizmos for farm tiles in the editor for debugging
            if (!drawFarmTiles) return;

            Gizmos.color = Color.yellow;

            for (int x = 0; x < length; x++)
            {
                for (int z = 0; z < width; z++)
                {
                    Vector3 position = transform.position + new Vector3(x + 0.5f, 0f, z + 0.5f);
                    Gizmos.DrawWireCube(position, new Vector3(1f, 0.2f, 1f));
                }
            }
        }
#endif
    }
}
