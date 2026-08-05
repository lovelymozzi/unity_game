using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(AudioSource))]
    public class Farmer : MonoBehaviour
    {
        [Tooltip("Farmer's movement speed in units per second.")]
        [SerializeField] private float moveSpeed = 3f;

        [Tooltip("References to the tools used by the farmer when working (e.g., seed bag, watering can, scythe).")]
        [SerializeField] private GameObject[] farmTools;

        [Header("VFX and SFX")]
        [SerializeField] private ParticleSystem seedsParticle;
        [SerializeField] private ParticleSystem waterParticle;
        [SerializeField] private ParticleSystem leavesParticle;
        [SerializeField] private AudioClip seedSound;
        [SerializeField] private AudioClip waterSound;
        [SerializeField] private AudioClip scytheSound;

        private NavMeshAgent agent;
        private Animator animator;
        private AudioSource audioSource;

        private FarmerData data;
        private Inventory inventory;
        private State currentState;

        private bool isMoving;
        private bool isWorking;
        private bool isWatering;

        public void Initialize(FarmerData data) => this.data = data;

        void Awake()
        {
            animator = GetComponent<Animator>();
            audioSource = GetComponent<AudioSource>();
            agent = GetComponent<NavMeshAgent>();
            inventory = GetComponent<Inventory>();
        }

        IEnumerator Start()
        {
            // Start state update after a random delay
            yield return new WaitForSeconds(Random.Range(0.5f, 2.5f));
            StartCoroutine(UpdateState());
        }

        IEnumerator UpdateState()
        {
            var siloPos = data.SiloIslandPosition;
            var farmPos = data.FarmIslandPosition;

            // State machine to handle the farmer's behavior
            switch (currentState)
            {
                case State.Idle:
                    yield return Travel(farmPos);
                    currentState = State.AtFarm;
                    break;

                case State.AtFarm:
                    yield return Work();
                    currentState = State.FullBag;
                    break;

                case State.FullBag:
                    yield return Travel(siloPos);
                    currentState = State.AtSilo;
                    break;

                case State.AtSilo:
                    yield return Deposit();
                    currentState = State.Idle;
                    break;

                default:
                    break;
            }

            // Recursively call to continue updating states
            StartCoroutine(UpdateState());
        }

        // Handle movement between islands
        IEnumerator Travel(Vector3 destination)
        {
            agent.SetDestination(destination);
            yield return new WaitUntil(() => HasArrived());
        }

        // Handle movement within the farm area
        IEnumerator Move(Vector3 destination)
        {
            isMoving = true;
            agent.SetDestination(destination);
            yield return new WaitUntil(() => HasArrived() || currentState == State.FullBag);
            isMoving = false;
        }

        private bool HasArrived()
        {
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                    {
                        animator.SetBool("IsMoving", false);
                        return true;
                    }
                }
            }

            animator.SetBool("IsMoving", true);
            return false;
        }

        IEnumerator Work()
        {
            SetWorking(true);

            // Continue working until the inventory is full
            while (inventory.SumItems() < data.Capacity)
            {
                var farmState = data.Farm.GetState();

                // Activate the correct farm tool based on the farm state
                for (int i = 0; i < farmTools.Length; i++)
                {
                    farmTools[i].SetActive(i == (int)farmState);
                }

                animator.SetInteger("FarmState", (int)farmState);

                if (!isMoving)
                {
                    Vector3 targetPos = data.Farm.GetTargetSoilPosition();

                    // Adjust target position if it's too close
                    if (Vector3.Distance(transform.position, targetPos) < 0.5f)
                        targetPos = data.Farm.transform.position;

                    StartCoroutine(Move(targetPos));
                }

                // Start watering plants if the farm is in the planted state
                if (farmState == Farm.State.Planted && !isWatering)
                {
                    StartCoroutine(WaterPlants());
                }

                yield return null;
            }

            // Deactivate tools and reset animations after work is done
            foreach (var tool in farmTools) tool.SetActive(false);
            animator.SetInteger("FarmState", -1);
            SetWorking(false);
        }

        void SetWorking(bool working)
        {
            isWorking = working;

            // Reduce movement speed when working
            agent.speed = isWorking ? moveSpeed / 2f : moveSpeed;
            animator.SetFloat("Speed", isWorking ? 0f : 1f);

            // Smooth transition for layer weights in animation
            float startValue = animator.GetLayerWeight(1);
            float endValue = isWorking ? 1f : 0f;
            DOVirtual.Float(startValue, endValue, 0.5f, value => animator.SetLayerWeight(1, value));
        }

        IEnumerator Deposit()
        {
            string itemId = data.Farm.CropData.ItemId;
            float timer = 0f;
            float deltaTime = 0.02f;
            float spawnTime = 0.05f;

            FullMessage fullMessage = null;

            // Continue depositing until inventory is empty
            while (inventory.SumItems() > 0)
            {
                timer += deltaTime;

                if (data.Silo.DepositCrop(itemId))
                {
                    inventory.SubtractItem(itemId);

                    if (timer >= spawnTime)
                    {
                        AnimateCrop();
                        timer = 0f;
                    }

                    if (fullMessage != null) Destroy(fullMessage.gameObject);
                }
                else if (fullMessage == null)
                {
                    // Display message when silo is full
                    Vector3 displayPosition = transform.TransformPoint(Vector3.up * 2.5f);
                    fullMessage = UIManager.Instance.DisplayFullMessage(displayPosition);
                }

                yield return new WaitForSeconds(deltaTime);
            }
        }

        void AnimateCrop()
        {
            var crop = PoolManager.Instance.SpawnObject(data.Farm.CropData.Drop.name).transform;
            crop.position = transform.TransformPoint(Vector3.up * 2f);

            var randomPos = transform.position + Random.insideUnitSphere.Flatten();

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

        // Event handler for footstep sounds or effects (not used here)
        public void OnStep() { }

        // Event handler for planting seeds
        public void OnPlant(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight < 0.5f) return;

            if (data.Farm != null)
            {
                seedsParticle.Play();
                audioSource.PlayOneShot(seedSound);
                data.Farm.PlantSeeds(transform);
            }
        }

        private IEnumerator WaterPlants()
        {
            isWatering = true;
            waterParticle.Play();
            audioSource.loop = true;
            audioSource.clip = waterSound;
            audioSource.Play();

            // Watering loop until the farm state changes
            while (data.Farm.GetState() == Farm.State.Planted)
            {
                data.Farm.WaterPlants(transform);
                yield return new WaitForSeconds(0.5f);
            }

            isWatering = false;
            waterParticle.Stop();
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
        }

        // Event handler for harvesting crops
        public void OnHarvest(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight < 0.5f) return;

            if (data.Farm.HarvestPlants(transform) > 0)
            {
                leavesParticle.Play();
                audioSource.PlayOneShot(scytheSound);
            }
        }

        // Enumeration for different farmer states
        public enum State
        {
            Idle,
            AtFarm,
            FullBag,
            AtSilo
        }
    }
}
