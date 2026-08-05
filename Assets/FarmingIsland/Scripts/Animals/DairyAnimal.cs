using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cinemachine;

namespace CryingSnow.FarmingIsland
{
    public class DairyAnimal : Animal
    {
        // Virtual cameras for different viewing angles during the milking minigame.
        [SerializeField] private CinemachineVirtualCamera leftVirtualCamera;
        [SerializeField] private CinemachineVirtualCamera rightVirtualCamera;

        // Skinned mesh renderer representing the milk bucket that visually fills up during the minigame.
        [SerializeField] private SkinnedMeshRenderer milkBucket;

        // Sound effect played when milking.
        [SerializeField] private AudioClip milkingSound;

        private List<Teat> teats = new List<Teat>(); // List of teats involved in the minigame.
        private float minTeatSwipeDistance; // Minimum swipe distance required to increase milking progress.

        protected override void Awake()
        {
            base.Awake();

            // Get all teats and initially deactivate them.
            teats = GetComponentsInChildren<Teat>(true).ToList();
            teats.ForEach(t => t.gameObject.SetActive(false));

            // Deactivate the milk bucket at the start.
            milkBucket.gameObject.SetActive(false);

            // Set the minimum swipe distance based on screen height (30%).
            minTeatSwipeDistance = Screen.height * 0.3f;
        }

        protected override IEnumerator MiniGame()
        {
            // Activate teats and reset their blend shapes.
            teats.ForEach(t =>
            {
                t.gameObject.SetActive(true);
                t.SetBlendShapes(0.1f);
            });

            // Activate and reset the milk bucket blend shape.
            milkBucket.gameObject.SetActive(true);
            milkBucket.SetBlendShapeWeight(0, 0f);

            controller.InteruptMovement(); // Stop any animal movement during the minigame.
            player.SetVisible(false); // Hide the player during the minigame.

            // Activate the appropriate virtual camera based on the position.
            var vCam = leftVirtualCamera.gameObject;
            if (rightVirtualCamera.transform.position.z < leftVirtualCamera.transform.position.z)
                vCam = rightVirtualCamera.gameObject;

            vCam.SetActive(true);

            // Display the UI prompt and hide the HUD during the minigame.
            UIManager.Instance.ProgressBar.Toggle($"Milk the {gameObject.name.ToLower()}!");
            UIManager.Instance.ToggleHUD(false);

            yield return new WaitForSeconds(1f);

            int milkedCount = 0;

            // Loop through each teat until all are milked.
            while (milkedCount < teats.Count)
            {
                float blend = 0.1f;
                Vector2 startTouchPosition = Vector2.zero;
                Vector2 currentTouchPosition = Vector2.zero;
                bool isSwiping = false;

                // Handle the milking input and adjust the blend shape for the current teat.
                while (blend < 0.9f)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        startTouchPosition = Input.mousePosition;
                        isSwiping = true;
                    }

                    if (Input.GetMouseButton(0) && isSwiping)
                    {
                        currentTouchPosition = Input.mousePosition;
                        float swipeDistance = startTouchPosition.y - currentTouchPosition.y;

                        if (swipeDistance > 0)
                        {
                            float blendIncrease = swipeDistance / minTeatSwipeDistance;
                            blend = Mathf.Clamp(0.1f + blendIncrease, 0.1f, 0.9f);
                        }
                    }

                    if (!isSwiping || (Input.GetMouseButtonUp(0) && isSwiping))
                    {
                        blend = Mathf.MoveTowards(blend, 0.1f, Time.deltaTime);
                        if (Input.GetMouseButtonUp(0))
                        {
                            isSwiping = false;
                        }
                    }

                    teats[milkedCount].SetBlendShapes(blend);
                    yield return null;
                }

                // Play the milking particle effect and update the milk bucket.
                var milkParticle = teats[milkedCount].MilkParticle;
                if (milkParticle != null)
                {
                    milkParticle.Play();
                    AudioManager.Instance.PlaySFX(milkingSound);
                    float duration = milkParticle.main.duration;
                    float elapsedTime = 0f;
                    float targetWeight = (milkedCount + 1f) / teats.Count * 100;

                    while (elapsedTime < duration)
                    {
                        elapsedTime += Time.deltaTime;
                        float currentWeight = milkBucket.GetBlendShapeWeight(0);
                        float newWeight = Mathf.Lerp(currentWeight, targetWeight, elapsedTime / duration);
                        milkBucket.SetBlendShapeWeight(0, newWeight);
                        yield return null;
                    }
                }

                milkedCount++;

                // Update the progress bar based on the number of teats milked.
                float milkingProgress = (float)milkedCount / teats.Count;
                UIManager.Instance.ProgressBar.UpdateDisplay(milkingProgress);
                yield return null;
            }

            yield return new WaitForSeconds(0.3f);

            // Reset the UI and camera after the minigame ends.
            UIManager.Instance.ProgressBar.Toggle("");
            UIManager.Instance.ToggleHUD(true);

            player.SetVisible(true); // Make the player visible again.
            vCam.SetActive(false); // Deactivate the virtual camera.
            teats.ForEach(t => t.gameObject.SetActive(false)); // Deactivate teats.
            milkBucket.gameObject.SetActive(false); // Deactivate the milk bucket.

            yield return new WaitForSeconds(1f);

            // Decide the next action for the animal after the minigame.
            controller.DecideNextAction();
        }
    }
}
