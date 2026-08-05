using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public class FullMessage : MonoBehaviour
    {
        private Camera mainCamera; // Reference to the main camera for screen-space conversion.
        private Vector3 displayPosition; // Position in the world where the message should be displayed.

        void Start()
        {
            // Cache the main camera reference for performance and convenience.
            mainCamera = Camera.main;
        }

        /// <summary>
        /// Initializes the message with the world position it should track.
        /// </summary>
        /// <param name="displayPosition">The world position where the message should be displayed.</param>
        public void Initialize(Vector3 displayPosition)
        {
            this.displayPosition = displayPosition;
        }

        void LateUpdate()
        {
            // Converts the world position to screen space and updates the UI element's position.
            transform.position = mainCamera.WorldToScreenPoint(displayPosition);
        }
    }
}
