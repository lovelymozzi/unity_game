using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class ATCTower : MonoBehaviour, IProp
    {
        [Tooltip("The helicopter prefab used in the animation sequence.")]
        [SerializeField] private Helicopter helicopterPrefab;

        [Tooltip("The position on the helipad where the helicopter spawns.")]
        [SerializeField] private Transform helipadTransform;

        [Tooltip("Offset for the player's entry position relative to the helipad.")]
        [SerializeField] private Vector3 entryOffset = new Vector3(1.5f, 0f, -0.5f);

        [Tooltip("Offset for the helicopter's final destination position during takeoff.")]
        [SerializeField] private Vector3 destinationOffset = new Vector3(80f, 20f, 0f);

        [Tooltip("Reference to the virtual camera that tracks the helicopter.")]
        [SerializeField] private CinemachineVirtualCamera virtualCamera;

        Location IProp.Location => Location.Tower;

        // Lazy-loaded reference to the PlayerController
        private PlayerController _player;
        private PlayerController player
        {
            get
            {
                if (_player == null)
                    _player = FindFirstObjectByType<PlayerController>();
                return _player;
            }
        }

        // Flag to ensure the flight sequence only runs once at a time
        private bool isFlying;

        private void Awake()
        {
            // Start with the tower scaled down to zero for animation purposes
            transform.localScale = Vector3.zero;
        }

        // Animate the tower appearance and trigger the helicopter sequence
        void IProp.Animate(bool instant, System.Action onFinished)
        {
            if (instant)
            {
                // Instantly set the tower's scale without animation
                transform.localScale = Vector3.one;
                onFinished?.Invoke();
            }
            else
            {
                // Animate the tower's appearance with a bounce effect
                transform.DOScale(Vector3.one, 1f)
                    .SetEase(Ease.OutBounce)
                    .OnComplete(() =>
                    {
                        onFinished?.Invoke();
                        StartCoroutine(Fly());
                    });
            }
        }

        // Trigger the flight sequence when the player enters the tower's collider
        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _player = other.GetComponent<PlayerController>();
                StartCoroutine(Fly());
            }
        }

        // Coroutine to handle the helicopter flight sequence and scene transition
        IEnumerator Fly()
        {
            if (isFlying) yield break; // Prevent multiple flight sequences from running concurrently

            isFlying = true;

            player.IsControllable = false; // Disable player control during the sequence
            UIManager.Instance.ToggleHUD(false); // Hide HUD elements
            UIManager.Instance.SetActiveJoystick(false); // Hide joystick controls

            // Calculate the helicopter's spawn point above the helipad
            float flyingHeight = destinationOffset.y;
            var spawnPoint = helipadTransform.position + Vector3.up * flyingHeight;
            var lookRotation = Quaternion.Euler(0f, 180f, 0f);
            var helicopter = Instantiate(helicopterPrefab, spawnPoint, lookRotation).transform;

            // Set the virtual camera to focus on the helicopter
            virtualCamera.m_LookAt = helicopter;
            virtualCamera.gameObject.SetActive(true);

            yield return new WaitForSeconds(1f); // Small delay before lowering the helicopter

            // Lower the helicopter to the helipad
            yield return helicopter.DOMoveY(0f, 5f).WaitForCompletion();

            // Move the player towards the helipad's entry point
            var entryPoint = helipadTransform.position + entryOffset;
            yield return player.SetDestination(entryPoint);

            // Hide the player after reaching the destination
            player.SetVisible(false);

            yield return new WaitForSeconds(1f); // Pause before lifting off

            // Raise the helicopter to its flying height
            yield return helicopter.DOMoveY(flyingHeight, 5f).WaitForCompletion();

            // Calculate the direction and rotation for the helicopter's destination
            Vector3 flyingDestination = helipadTransform.position + destinationOffset;
            Vector3 flyingDirection = (flyingDestination.Flatten() - helipadTransform.position).normalized;
            Quaternion flyingLookRotation = Quaternion.LookRotation(flyingDirection);

            // Rotate the helicopter towards its flying direction
            yield return helicopter.DORotate(flyingLookRotation.eulerAngles, 2f).WaitForCompletion();

            // Apply a slight tilt to simulate forward acceleration
            Quaternion tiltAngle = helicopter.rotation * Quaternion.Euler(15f, 0f, 0f);
            helicopter.DORotateQuaternion(tiltAngle, 1f);

            // Move the helicopter towards its destination at a consistent speed
            yield return helicopter.DOMove(flyingDestination, 5f).SetSpeedBased().WaitForCompletion();

            // Trigger the scene transition
            yield return GoToNextScene();
        }

        // Coroutine to handle the transition to the next scene
        IEnumerator GoToNextScene()
        {
            // Fade the screen out before loading the new scene
            yield return UIManager.Instance.FadeInScreen();

            int destinationSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(destinationSceneIndex);
            asyncOperation.allowSceneActivation = false;

            // Wait until the scene is fully loaded but not yet activated
            while (!asyncOperation.isDone)
            {
                if (asyncOperation.progress >= 0.9f)
                {
                    yield return new WaitForSeconds(1f); // Delay before activating the scene
                    asyncOperation.allowSceneActivation = true; // Activate the new scene
                }

                yield return null; // Continue waiting until the next frame
            }
        }
    }
}
