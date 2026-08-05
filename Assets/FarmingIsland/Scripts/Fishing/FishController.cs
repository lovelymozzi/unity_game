using System.Collections;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(Animator))]
    public class FishController : MonoBehaviour
    {
        [Tooltip("Reference to the data associated with this fish item.")]
        [SerializeField] private ItemData fishItem;

        public ItemData FishItem => fishItem; // Public property to access fish item data.
        public float SwimDepth { get; set; } // The depth at which the fish swims. This value changes when the fish is caught by the player.
        public bool IsPanic { get; set; } // Flag to determine if the fish is in panic mode.
        public int FishIndex { get; private set; } // The index used to determine the correct fish prefab from the list.

        private Vector3 swimSpot; // The central spot where the fish swims around.
        private float swimRadius; // Radius within which the fish will swim.
        private float swimSpeed; // Speed at which the fish swims.

        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void Initialize(int fishIndex, Vector3 swimSpot, float swimRadius, float swimSpeed)
        {
            FishIndex = fishIndex;

            this.swimSpot = swimSpot;
            this.swimRadius = swimRadius;
            this.swimSpeed = swimSpeed;

            SwimDepth = swimSpot.y; // Set the swimming depth based on the y-coordinate of the swim spot.

            StartCoroutine(Swim());
        }

        private IEnumerator Swim()
        {
            // Calculate a random position within a sphere flattened on the y-axis around the swim spot.
            var randomPos = Random.insideUnitSphere.Flatten() * swimRadius;
            var targetPos = new Vector3(swimSpot.x, SwimDepth, swimSpot.z) + randomPos;

            // Move the fish towards the target position.
            while (!IsNearTarget(targetPos, out Vector3 direction))
            {
                float speedMultiplier = IsPanic ? 2f : 1f; // Double speed if in panic mode.
                float distanceDelta = Time.deltaTime * swimSpeed * speedMultiplier;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, distanceDelta);

                if (direction != Vector3.zero)
                {
                    // Rotate the fish to face the direction of movement.
                    var lookRotation = Quaternion.LookRotation(direction);
                    float degreesDelta = Time.deltaTime * 360f; // Rotation speed.
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, degreesDelta);
                }

                yield return null;
            }

            // Recursively continue swimming to a new target position.
            StartCoroutine(Swim());
        }

        private bool IsNearTarget(Vector3 targetPosition, out Vector3 direction)
        {
            // Flatten positions to consider only x and z coordinates for distance check.
            var a = targetPosition.Flatten();
            var b = transform.position.Flatten();
            direction = (a - b).normalized; // Calculate the direction from the current position to the target.

            // Check if the fish is close enough to the target position.
            return Vector3.Distance(a, b) < 0.1f;
        }

        public void Catch()
        {
            StopAllCoroutines();
            animator.SetBool("IsCaught", true);
        }
    }
}
