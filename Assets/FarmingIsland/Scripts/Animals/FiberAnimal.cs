using System.Collections;
using UnityEngine;
using Cinemachine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class FiberAnimal : Animal
    {
        [SerializeField] private CinemachineVirtualCamera virtualCamera; // Camera used during the shearing minigame.

        private Wool wool; // Reference to the Wool component responsible for growing and shearing wool.

        protected override void Awake()
        {
            base.Awake();
            wool = GetComponentInChildren<Wool>();
        }

        protected override IEnumerator Produce()
        {
            yield return base.Produce(); // Wait for the base production process to complete.
            wool.GrowWool(); // Trigger wool growth after production.
        }

        protected override IEnumerator MiniGame()
        {
            controller.InteruptMovement(); // Stop the animal from moving during the minigame.
            controller.SetAnimatorBool("IsPosing", true); // Trigger posing animation for the shearing scene.
            player.SetVisible(false); // Hide the player character during the minigame.
            UIManager.Instance.SetActiveJoystick(false); // Deactivate the joystick to prevent movement.
            virtualCamera.gameObject.SetActive(true); // Activate the camera focused on the minigame.

            yield return new WaitForSeconds(1f); // Short delay before starting the camera rotation.

            // Rotate the camera smoothly around the animal during the shearing process.
            virtualCamera.transform.parent.DORotate(Vector3.up, 20f)
                .SetSpeedBased()
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);

            yield return wool.Shear(); // Perform the shearing coroutine and wait for it to complete.
            DOTween.Kill(virtualCamera.transform.parent); // Stop the camera rotation after shearing.

            player.SetVisible(true); // Make the player visible again after the minigame.
            virtualCamera.gameObject.SetActive(false); // Deactivate the virtual camera.

            controller.SetAnimatorBool("IsPosing", false); // End the posing animation.
            yield return new WaitForSeconds(1f); // Small delay before resuming normal behavior.

            UIManager.Instance.SetActiveJoystick(true); // Reactivate the joystick controls.
            controller.DecideNextAction(); // Determine what the animal should do next.
        }
    }
}
