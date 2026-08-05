using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(Animator))]
    public class AnimalController : MonoBehaviour, IProp
    {
        [Tooltip("The speed at which the animal moves while wandering.")]
        [SerializeField] private float movingSpeed = 1f;

        [Tooltip("The rotation speed of the animal in degrees per second.")]
        [SerializeField] private float turningSpeed = 360f;

        [Tooltip("The base offset of the animal's NavMeshAgent from the ground.")]
        [SerializeField] private float baseOffset = 0f;

        [Tooltip("The radius within which the animal will wander.")]
        [SerializeField] private float wanderRadius = 4f;

        [Tooltip("Possible animation triggers that the animal may randomly perform.")]
        [SerializeField] private string[] actionTriggers;

        public Location Location => Location.None; // Represents the location type; not used here.

        private NavMeshAgent agent; // The NavMeshAgent that controls the animal's movement.
        private Animator animator; // Animator component controlling the animal's animations.

        void Awake()
        {
            animator = GetComponent<Animator>();
            gameObject.SetActive(false);
        }

        void IProp.Animate(bool instant, System.Action onFinished)
        {
            gameObject.SetActive(true);
            StartCoroutine(Initialize(instant));
        }

        IEnumerator Initialize(bool instant)
        {
            if (instant)
            {
                // Wait until the NavMesh is ready before creating the agent.
                yield return new WaitUntil(() => IslandManager.Instance.HasNavMesh);
                CreateAgent();
            }
            else
            {
                // Scale in the animal smoothly using a bounce effect.
                transform.localScale = Vector3.zero;
                yield return transform.DOScale(Vector3.one, 1f)
                    .SetEase(Ease.OutBounce)
                    .WaitForCompletion();

                CreateAgent();
            }

            // Start the wandering behavior after initialization.
            StartCoroutine(Wander());
        }

        private void CreateAgent()
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.baseOffset = baseOffset;
            agent.speed = movingSpeed;
            agent.angularSpeed = turningSpeed;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        IEnumerator Wander()
        {
            Vector3 destination = Vector3.zero;
            yield return new WaitUntil(() => GetRandomPoint(out destination)); // Find a random destination within range.
            agent.SetDestination(destination);
            animator.SetBool("IsMoving", true);

            yield return new WaitUntil(() => HasArrived());
            animator.SetBool("IsMoving", false);

            // Wait for a random amount of time before deciding the next action.
            yield return new WaitForSeconds(Random.Range(1f, 5f));
            DecideNextAction();
        }

        // Attempts to find a valid random point within the wander radius for the animal to move to.
        private bool GetRandomPoint(out Vector3 result)
        {
            var center = transform.position;
            // 50% chance to wander between islands.
            if (Random.value < 0.5f) center = IslandManager.Instance.GetRandomIsland().GetPosition();

            // Try to find a valid point within the NavMesh.
            for (int i = 0; i < 30; i++)
            {
                Vector3 randomPoint = center + Random.insideUnitSphere * wanderRadius;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPoint, out hit, 1.0f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }
            result = Vector3.zero;
            return false;
        }

        // Checks if the NavMeshAgent has arrived at its destination.
        private bool HasArrived()
        {
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        IEnumerator DoRandomAction()
        {
            int randomIndex = Random.Range(0, actionTriggers.Length);
            string randomAction = actionTriggers[randomIndex];
            animator.SetTrigger(randomAction); // Trigger the random animation.

            yield return new WaitForSeconds(0.3f); // Wait for the animation transition.
            yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length); // Wait for the animation to finish.

            DecideNextAction();
        }

        public void DecideNextAction()
        {
            int nextAction = Random.Range(0, 2);

            if (nextAction == 0) StartCoroutine(Wander()); // 50% chance to wander again.
            else StartCoroutine(DoRandomAction()); // 50% chance to perform a random action.
        }

        public void InteruptMovement()
        {
            StopAllCoroutines(); // Stop any current wandering or actions.
            animator.SetBool("IsMoving", false);
            agent.SetDestination(transform.position); // Halt the NavMeshAgent's movement.
        }

        public void SetAnimatorBool(string parameter, bool value)
        {
            animator.SetBool(parameter, value);
        }
    }
}
