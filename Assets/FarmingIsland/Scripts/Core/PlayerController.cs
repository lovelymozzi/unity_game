using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(Inventory))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField, Tooltip("Speed at which the player moves (units per second)")]
        private float moveSpeed = 3.5f;

        [SerializeField, Tooltip("Speed at which the player turns (degrees per second)")]
        private float turnSpeed = 360f;

        [SerializeField, Tooltip("Footstep sounds for walking on islands")]
        private AudioClip[] islandFootsteps;

        [SerializeField, Tooltip("Footstep sounds for walking on bridges")]
        private AudioClip[] bridgeFootsteps;

        [SerializeField, Tooltip("Array of farm tool GameObjects used when working")]
        private GameObject[] farmTools;

        [SerializeField, Tooltip("Fishing rod GameObject for fishing mini game")]
        private GameObject fishingRod;

        [Header("Visual & Sound Effects")]
        [SerializeField, Tooltip("Material for the player when fully visible")]
        private Material opaqueMaterial;

        [SerializeField, Tooltip("Material for the player when transparent")]
        private Material transparentMaterial;

        [SerializeField, Tooltip("Particle system for planting seeds")]
        private ParticleSystem seedsParticle;

        [SerializeField, Tooltip("Particle system for watering plants")]
        private ParticleSystem waterParticle;

        [SerializeField, Tooltip("Particle system for harvesting plants")]
        private ParticleSystem leavesParticle;

        [SerializeField, Tooltip("Sound played when planting seeds")]
        private AudioClip seedSound;

        [SerializeField, Tooltip("Sound played when watering plants")]
        private AudioClip waterSound;

        [SerializeField, Tooltip("Sound played when harvesting plants")]
        private AudioClip scytheSound;

        // Public Getters
        public bool IsWorking => farm != null;
        public bool IsControllable { get; set; } = true;
        public Inventory Inventory { get; private set; }
        public GameObject FishingRod => fishingRod;

        // Component References
        private Animator animator;
        private CharacterController controller;
        private AudioSource audioSource;
        private SkinnedMeshRenderer skinRenderer;

        // Inverse Kinematics
        private float ik_weight;
        private Transform leftHandTarget;
        private Transform rightHandTarget;

        // Movement Variables
        private float speedFactor;
        private Vector3 movement;
        private Vector3 velocity;
        private bool isGrounded;
        private bool hasDestination;

        // The current farm instance that the player is interacting with
        private Farm farm;

        const float gravityValue = -9.81f;

        private void Awake()
        {
            // Cache the essential components
            animator = GetComponent<Animator>();
            controller = GetComponent<CharacterController>();
            audioSource = GetComponent<AudioSource>();
            skinRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            Inventory = GetComponent<Inventory>();
        }

        private void Update()
        {
            // Check if the player is grounded and reset vertical velocity if falling
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = 0f;
            }

            // Update movement input based on player control status
            if (IsControllable)
            {
                movement.x = SimpleInput.GetAxis("Horizontal");
                movement.z = SimpleInput.GetAxis("Vertical");
                movement.Normalize(); // Normalize movement vector to ensure consistent speed
            }
            else if (!hasDestination)
            {
                movement = Vector3.zero; // Disable movement if not controllable
            }

            // Adjust movement speed factor based on working status
            speedFactor = Mathf.Lerp(speedFactor, IsWorking ? 0.5f : 1f, 0.1f);
            controller.Move(movement * moveSpeed * speedFactor * Time.deltaTime); // Move player based on input and speed factor

            // Rotate player towards movement direction
            if (movement != Vector3.zero)
            {
                var lookRotation = Quaternion.LookRotation(movement); // Calculate desired rotation
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, Time.deltaTime * turnSpeed); // Smoothly rotate towards target direction
            }

            // Apply gravity to the player
            velocity.y += gravityValue * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime); // Move player with applied gravity

            // Update animator parameters based on movement status
            animator.SetBool("IsMoving", movement != Vector3.zero); // Set "IsMoving" parameter if there is any movement
            animator.SetFloat("Speed", IsWorking ? 0f : 1f); // Adjust "Speed" parameter based on working status
        }

        /// <summary>
        /// Sets the player's working state and updates related visuals and sounds based on the provided farm.
        /// </summary>
        /// <param name="farm">The Farm instance that the player is working on, or null if not working.</param>
        public void SetWorking(Farm farm)
        {
            // Assign the provided farm to the class variable
            this.farm = farm;

            // Smoothly transition the weight of the working animation layer
            float startValue = animator.GetLayerWeight(1); // Get the current weight of the working layer
            float endValue = IsWorking ? 1f : 0f; // Determine the target weight based on whether the player is working
            DOVirtual.Float(startValue, endValue, 0.5f, value => animator.SetLayerWeight(1, value)); // Transition to target weight

            // If there is a valid farm instance, configure the farm tools and animator
            if (farm != null)
            {
                // Get the current state of the farm
                var farmState = farm.GetState();

                // Start watering plants if the farm state is "Planted"
                if (farmState == Farm.State.Planted)
                {
                    StartCoroutine(WaterPlants());
                }

                // Activate the appropriate farm tool based on the farm state
                for (int i = 0; i < farmTools.Length; i++)
                {
                    farmTools[i].SetActive(i == (int)farmState); // Only activate the tool that matches the current farm state
                }

                // Update the animator with the current farm state
                animator.SetInteger("FarmState", (int)farmState);
            }
            else
            {
                // Deactivate all farm tools if there is no farm instance
                foreach (var tool in farmTools) tool.SetActive(false);

                // Reset the animator's farm state parameter
                animator.SetInteger("FarmState", -1); // Set the "FarmState" parameter to -1 to indicate no farm
            }
        }

        /// <summary>
        /// Sets the visibility of the player and adjusts the material and transparency accordingly.
        /// </summary>
        /// <param name="visible">Whether the player should be visible (true) or invisible (false).</param>
        public void SetVisible(bool visible)
        {
            // Set the material to transparent if the player is not visible
            if (!visible)
                skinRenderer.material = transparentMaterial;

            // Determine the end value for material fade based on visibility
            float endValue = visible ? 1f : 0f;

            // Fade the material's alpha value to the end value over a duration of 1 second
            skinRenderer.material.DOFade(endValue, 1f)
                .OnComplete(() =>
                {
                    // Restore the opaque material if the player becomes visible
                    if (visible)
                        skinRenderer.material = opaqueMaterial;
                });

            // Set the player's controllability based on visibility
            IsControllable = visible;
        }

        /// <summary>
        /// Moves the player towards the specified destination until close enough.
        /// Terminates if a timeout is reached before arriving.
        /// </summary>
        /// <param name="destination">The target position to move towards.</param>
        /// <returns>IEnumerator for coroutine handling.</returns>
        public IEnumerator SetDestination(Vector3 destination)
        {
            // Mark that the player currently has a destination to reach
            hasDestination = true;

            // Set a maximum time limit for reaching the destination
            float timeout = 5f;

            // Continue moving until the player is close enough to the destination
            while (Vector3.Distance(transform.position, destination) > 0.1f)
            {
                // Calculate the movement direction towards the destination
                movement = (destination - transform.position).normalized;

                // Decrease the timeout as time passes
                timeout -= Time.deltaTime;
                // Exit the loop if the timeout has elapsed, preventing indefinite movement
                if (timeout <= 0f) break;

                yield return null;
            }

            // Mark that the destination has been reached
            hasDestination = false;
        }

        /// <summary>
        /// Handles the player's footstep sound when stepping on a surface.
        /// </summary>
        /// <param name="animationEvent">The animation event that triggers this method.</param>
        public void OnStep(AnimationEvent animationEvent)
        {
            // Check if the animation clip weight is below 0.5; if so, exit early
            if (animationEvent.animatorClipInfo.weight < 0.5f) return;

            // Get the current island at the player's position
            var currentIsland = IslandManager.Instance.GetCurrentIsland(transform.position);

            // Exit if no island is found
            if (currentIsland == null) return;

            // Play the appropriate footstep sound based on whether the island is a bridge
            bool isBridge = currentIsland.IsBridge;
            AudioClip[] footstepSounds = isBridge ? bridgeFootsteps : islandFootsteps;
            int randomIndex = Random.Range(0, footstepSounds.Length);
            audioSource.PlayOneShot(footstepSounds[randomIndex]);
        }

        /// <summary>
        /// Handles the planting action when the player performs a planting animation.
        /// </summary>
        /// <param name="animationEvent">The animation event that triggers this method.</param>
        public void OnPlant(AnimationEvent animationEvent)
        {
            // Check if the animation clip weight is below 0.5; if so, exit early
            if (animationEvent.animatorClipInfo.weight < 0.5f) return;

            // Proceed if the player is working on a farm
            if (farm != null)
            {
                // Play particle effect and sound for planting
                seedsParticle.Play();
                audioSource.PlayOneShot(seedSound);

                // Call the farm's method to plant seeds
                farm.PlantSeeds(transform);
            }
        }

        /// <summary>
        /// Coroutine to water plants while the player is working.
        /// </summary>
        /// <returns>IEnumerator for coroutine execution.</returns>
        private IEnumerator WaterPlants()
        {
            // Play the water particle effect and start playing the water sound in a loop
            waterParticle.Play();
            audioSource.loop = true;
            audioSource.clip = waterSound;
            audioSource.Play();

            // Continue watering plants while the player is working
            while (IsWorking)
            {
                // Call the farm's method to water plants
                farm.WaterPlants(transform);

                // Wait for half a second before watering again
                yield return new WaitForSeconds(0.5f);
            }

            // Stop the water particle effect and sound when done
            waterParticle.Stop();
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
        }

        /// <summary>
        /// Called when the player harvests plants.
        /// </summary>
        /// <param name="animationEvent">Animation event containing information about the current animation state.</param>
        public void OnHarvest(AnimationEvent animationEvent)
        {
            // Check if the animation weight is sufficient to trigger harvesting
            if (animationEvent.animatorClipInfo.weight < 0.5f) return;

            // Ensure there is a farm instance to work with
            if (farm != null)
            {
                // Harvest plants and get the count of harvested items
                int harvestCount = farm.HarvestPlants(transform);

                // If any plants were harvested, play the leaves particle effect and harvest sound
                if (harvestCount > 0)
                {
                    leavesParticle.Play();
                    audioSource.PlayOneShot(scytheSound);
                    Vibration.Vibrate();
                }
            }
        }

        /// <summary>
        /// Teleports the player to a specified destination.
        /// </summary>
        /// <param name="destination">The new position to which the player will be teleported.</param>
        public void Teleport(Vector3 destination)
        {
            // Temporarily disable the CharacterController to allow for instant position change.
            // This prevents any interference from CharacterController.Move, which is physics-based,
            // and could otherwise impact the direct setting of the transform position.
            controller.enabled = false;

            // Set the player's position and reset their rotation
            transform.position = destination;
            transform.rotation = Quaternion.identity;

            // Re-enable the CharacterController after teleporting
            controller.enabled = true;
        }

        public void SetAnimatorBool(string parameter, bool value)
        {
            animator.SetBool(parameter, value);
        }

        public void SetAnimatorInt(string parameter, int value)
        {
            animator.SetInteger(parameter, value);
        }

        /// <summary>
        /// Sets the targets for inverse kinematics (IK) and adjusts the weight of the IK effect.
        /// </summary>
        /// <param name="leftHandTarget">The target position and rotation for the left hand.</param>
        /// <param name="rightHandTarget">The target position and rotation for the right hand.</param>
        /// <param name="weight">The weight of the IK effect, controlling the influence of the IK targets.</param>
        public void SetIK(Transform leftHandTarget, Transform rightHandTarget, float weight)
        {
            // Assign the target transforms for the left and right hands.
            this.leftHandTarget = leftHandTarget;
            this.rightHandTarget = rightHandTarget;

            // Smoothly interpolate the IK weight over 0.5 seconds.
            DOVirtual.Float(ik_weight, weight, 0.5f, x => ik_weight = x);
        }

        /// <summary>
        /// Updates the inverse kinematics (IK) settings for the Animator component each frame.
        /// This method is called by Unity during the IK pass of animation to set the IK targets and weights.
        /// </summary>
        void OnAnimatorIK()
        {
            // If a target is assigned for the left hand, set its IK position and rotation weights.
            if (leftHandTarget != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, ik_weight);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, ik_weight);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
                animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
            }

            // If a target is assigned for the right hand, set its IK position and rotation weights.
            if (rightHandTarget != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, ik_weight);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, ik_weight);
                animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
                animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandTarget.rotation);
            }
        }
    }
}
