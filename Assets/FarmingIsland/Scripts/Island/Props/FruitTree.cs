using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(SphereCollider), typeof(CapsuleCollider))]
    public class FruitTree : MonoBehaviour, IProp
    {
        [Tooltip("The type of fruit item this tree produces.")]
        [SerializeField] private ItemData fruitItem;

        [Tooltip("The minimum shake intensity required to start shaking the tree.")]
        [SerializeField] private float shakeThreshold = 0.3f;

        [Tooltip("The total amount of shaking required to drop the fruits.")]
        [SerializeField] private float shakeRequired = 50f;

        [Tooltip("The time it takes for the fruits to regrow after being dropped.")]
        [SerializeField] private float regrowTime = 10f;

        [Tooltip("The target position for the camera when focusing on this tree.")]
        [SerializeField] private Transform cameraTarget;

        [Tooltip("The transforms representing each fruit on the tree.")]
        [SerializeField] private Transform[] fruits;

        [Header("Animation Settings")]
        [Tooltip("Duration of the tree shaking animation.")]
        [SerializeField] private float duration = 0.3f;

        [Tooltip("Strength of the shake during the tree shaking animation.")]
        [SerializeField] private float strength = 10f;

        [Tooltip("Vibrato factor for the shake animation.")]
        [SerializeField] private int vibrato = 15;

        [Tooltip("Randomness factor for the shake animation.")]
        [SerializeField] private float randomness = 45f;

        [Tooltip("Whether the shake animation should fade out gradually.")]
        [SerializeField] private bool fadeOut = true;

        [Header("Special Effects")]
        [Tooltip("Particle effect to simulate leaves falling when the tree shakes.")]
        [SerializeField] private ParticleSystem leavesParticle;

        [Tooltip("Sound effect played when the tree rustles.")]
        [SerializeField] private AudioClip rustleSound;

        [Tooltip("Sound effect played when fruits bounce after falling.")]
        [SerializeField] private AudioClip bouncingSound;

        [Header("IK Targets")]
        [Tooltip("Parent transform for hand targets, used for player IK when shaking the tree.")]
        [SerializeField] private Transform handTargetsParent;

        [Tooltip("Target transform for the left hand IK.")]
        [SerializeField] private Transform leftHandTarget;

        [Tooltip("Target transform for the right hand IK.")]
        [SerializeField] private Transform rightHandTarget;

        public Location Location => Location.Fruit;

        private SphereCollider treeTrigger;
        private CapsuleCollider treeCollider;
        private CinemachineVirtualCamera virtualCamera;
        private float shakeAmount;

        void Awake()
        {
            // Cache the tree's colliders and set up the trigger.
            treeTrigger = GetComponent<SphereCollider>();
            treeCollider = GetComponent<CapsuleCollider>();
            treeTrigger.isTrigger = true;

            // Create the virtual camera that focuses on this tree.
            CreateVirtualCamera();

            // Set the tree's initial scale to zero for animation.
            transform.localScale = Vector3.zero;

            // Set the tree's layer to obstacle for navigation system.
            gameObject.layer = LayerMask.NameToLayer("Obstacle");
        }

        void OnTriggerEnter(Collider other)
        {
            // Check if the player has entered the trigger area.
            if (other.CompareTag("Player"))
            {
                var player = other.GetComponent<PlayerController>();
                // Show an action button to start shaking the tree.
                UIManager.Instance.ToggleActivityButton(fruitItem.Icon, () =>
                {
                    virtualCamera.gameObject.SetActive(true);
                    StartCoroutine(ShakeTree(player));
                });
            }
        }

        void OnTriggerExit(Collider other)
        {
            // Reset shake amount and hide the action button when the player leaves the trigger area.
            if (other.CompareTag("Player"))
            {
                shakeAmount = 0f;
                UIManager.Instance.ToggleActivityButton(null, null);
            }
        }

        void IProp.Animate(bool instant, System.Action onFinished)
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

        void CreateVirtualCamera()
        {
            var vCamObj = new GameObject("Fruit Tree vCam");
            vCamObj.transform.position = cameraTarget.position;
            vCamObj.transform.rotation = cameraTarget.rotation;
            vCamObj.transform.SetParent(transform.parent);

            virtualCamera = vCamObj.AddComponent<CinemachineVirtualCamera>();
            virtualCamera.gameObject.SetActive(false);
            virtualCamera.m_Lens.FieldOfView = 60;
            virtualCamera.m_Lens.FarClipPlane = 500;
        }

        IEnumerator ShakeTree(PlayerController player)
        {
            // Disable the tree's colliders to prevent interactions during the shaking process.
            treeTrigger.enabled = false;
            treeCollider.enabled = false;

            // Disable the activity button and release the virtual joystick controls.
            UIManager.Instance.ToggleActivityButton(null, null);
            UIManager.Instance.ReleaseJoystick();
            UIManager.Instance.ToggleHUD(false);

            // Temporarily disable player controls and initiate the shaking animation.
            player.IsControllable = false;
            player.SetAnimatorBool("IsShaking", true);

            // Calculate the direction for the player to face the tree and smoothly rotate the player.
            Vector3 direction = (transform.position - player.transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(direction);

            // Position and orient the hand targets for proper IK alignment during the animation.
            handTargetsParent.position = player.transform.position;
            handTargetsParent.rotation = lookRotation;
            player.SetIK(leftHandTarget, rightHandTarget, 1f);

            // Smoothly rotate the player towards the tree.
            yield return player.transform.DORotateQuaternion(lookRotation, 0.3f).WaitForCompletion();

            // Display the progress bar UI for shaking the tree.
            UIManager.Instance.ProgressBar.Toggle("Shake the tree!");

            // Continue shaking until the required shake amount is reached.
            while (shakeAmount < shakeRequired)
            {
                // Get the intensity of the player's joystick shaking.
                float shakeIntensity = UIManager.Instance.GetJoystickShakeIntensity();

                // If the shake intensity exceeds the threshold, add it to the total shake amount.
                if (shakeIntensity > shakeThreshold)
                {
                    shakeAmount += shakeIntensity;

                    // Update the progress bar display based on the current shake progress.
                    UIManager.Instance.ProgressBar.UpdateDisplay(shakeAmount / shakeRequired);

                    // Ensure the tree is not already in the process of shaking before triggering another shake animation.
                    if (!DOTween.IsTweening(transform))
                    {
                        // Perform the shake animation, spawn leaf particles, and play a rustling sound.
                        transform.DOShakeRotation(duration, strength, vibrato, randomness, fadeOut);
                        Instantiate(leavesParticle, transform.position, Quaternion.identity);
                        AudioManager.Instance.PlaySFX(rustleSound);
                        Vibration.Vibrate();
                    }
                }

                // Wait until the next frame before repeating the loop.
                yield return null;
            }

            // Reset shake amount and hide the progress bar once shaking is complete.
            shakeAmount = 0f;
            UIManager.Instance.ProgressBar.Toggle("");

            // Re-enable the HUD and quit the shaking animation.
            UIManager.Instance.ToggleHUD(true);
            player.SetAnimatorBool("IsShaking", false);
            player.SetIK(null, null, 0f);

            // Drop fruits for the player to collect.
            yield return DropFruits(player);

            // Re-enable player control and deactivate the virtual camera.
            player.IsControllable = true;
            virtualCamera.gameObject.SetActive(false);

            // Re-enable the tree's colliders.
            treeCollider.enabled = true;

            // Start the regrow coroutine to regenerate the fruits over time.
            StartCoroutine(Regrow());
        }

        IEnumerator DropFruits(PlayerController player)
        {
            // Set the point where fruits will be collected by the player.
            Vector3 collectPoint = player.transform.TransformPoint(Vector3.up * 2f);
            float dropDuration = 1f;  // Duration for the fruit drop animation.
            float collectDuration = 0.3f;  // Duration for the collection animation.
            List<Transform> fruitDrops = new List<Transform>();  // List to store dropped fruit objects.

            // Loop through each fruit in the tree and perform the dropping animation.
            foreach (Transform fruit in fruits)
            {
                // Deactivate the original fruit objects on the tree.
                fruit.gameObject.SetActive(false);

                // Calculate the position and direction for dropping the fruit.
                var dropPosition = fruit.position.Flatten();  // Flatten to remove height difference.
                var dropDirection = dropPosition - transform.position;
                dropPosition += dropDirection.normalized * 1.1f;  // Adjust the position for a natural drop.

                // Spawn a fruit object from the pool for the drop animation.
                var fruitDrop = PoolManager.Instance.SpawnObject(fruitItem.Drop.name).transform;
                fruitDrop.position = fruit.position;  // Set the initial position of the dropped fruit.
                fruitDrop.rotation = fruit.rotation;  // Maintain the original rotation of the fruit.
                fruitDrops.Add(fruitDrop);

                // Perform the drop animation with a jump effect and a bounce.
                fruitDrop.DOJump(dropPosition, Random.Range(1f, 3f), 1, dropDuration).SetEase(Ease.OutBounce);
                fruitDrop.DORotate(Vector3.zero, dropDuration);  // Reset the rotation during the drop.
            }

            // Wait for half of the drop duration before playing the bouncing sound effect.
            // The purpose is to time the sound approximately when the fruits land the first time.
            // Since the OutBounce easing curve creates a few bounces, this rough estimation aligns
            // the sound effect (which contains multiple thuds) with the first impactful landing.
            yield return new WaitForSeconds(dropDuration / 2);
            AudioManager.Instance.PlaySFX(bouncingSound, waitForCooldown: false);

            // Wait for the remaining half of the drop duration to complete the bounce animation.
            yield return new WaitForSeconds(dropDuration / 2);

            // Animate the collection of fruits by moving them to the player's collection point (above head).
            foreach (var fruitDrop in fruitDrops)
            {
                fruitDrop.DOMove(collectPoint, collectDuration)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        // Return the fruit object to the pool after the collection animation.
                        PoolManager.Instance.ReturnObject(fruitDrop.gameObject);
                        // Add the collected fruit to the player's inventory.
                        player.Inventory.AddItem(fruitItem.ItemId);
                    });
            }

            // Wait for the collection duration before ending the coroutine.
            yield return new WaitForSeconds(collectDuration);
        }

        IEnumerator Regrow()
        {
            foreach (Transform fruit in fruits)
            {
                yield return new WaitForSeconds(regrowTime / fruits.Length);
                fruit.gameObject.SetActive(true);
            }

            treeTrigger.enabled = true;
        }

        // Utility method to update the fruit meshes in the editor.
        [ContextMenu("Update Fruit Mesh")]
        void UpdateFruitMesh()
        {
            foreach (Transform fruit in fruits)
            {
                var fruitMeshFilter = fruit.GetComponent<MeshFilter>();
                fruitMeshFilter.mesh = fruitItem.Drop.GetComponent<MeshFilter>().sharedMesh;
            }
        }
    }
}
