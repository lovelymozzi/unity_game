using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public class SiloInfo : MonoBehaviour
    {
        [Tooltip("Offset for the display position relative to the silo.")]
        [SerializeField] private Vector3 displayOffset = new Vector3(0f, 7f, 0f);

        [Tooltip("Prefab for displaying crop information.")]
        [SerializeField] private CropInfo cropInfoPrefab;

        private List<CropInfo> cropInfos = new List<CropInfo>(); // List to track instantiated CropInfo components.
        private Camera mainCamera; // Reference to the main camera for screen-space conversion.
        private Vector3 displayPosition; // Position in the world where the UI should be displayed.

        void Awake()
        {
            // Cache the main camera reference and initially hide the UI.
            mainCamera = Camera.main;
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            // Continuously update the UI's position to follow the designated world position.
            transform.position = mainCamera.WorldToScreenPoint(displayPosition);
        }

        /// <summary>
        /// Displays the crop information UI near the specified silo position.
        /// </summary>
        /// <param name="siloPosition">The world position of the silo.</param>
        /// <param name="crops">List of crops to display.</param>
        /// <param name="capacity">Maximum capacity for the crops.</param>
        public void Show(Vector3 siloPosition, List<Item> crops, int capacity)
        {
            displayPosition = siloPosition + displayOffset; // Adjust the position based on the offset.

            // Instantiate crop info UI elements for each crop in the list.
            foreach (var crop in crops)
            {
                var cropInfo = Instantiate(cropInfoPrefab, transform);
                cropInfo.UpdateInfo(crop, capacity);
                cropInfos.Add(cropInfo);
            }

            // Enable the UI after setting up all crop information.
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the crop information UI and clears all child elements.
        /// </summary>
        public void Hide()
        {
            // Destroy all child UI elements and clear the crop info list.
            foreach (Transform child in transform) Destroy(child.gameObject);
            cropInfos.Clear();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates the displayed crop information based on the current crop list and capacity.
        /// </summary>
        /// <param name="crops">Updated list of crops.</param>
        /// <param name="capacity">Updated capacity for the crops.</param>
        public void UpdateCropInfos(List<Item> crops, int capacity)
        {
            // Update each crop info UI element with the new data.
            for (int i = 0; i < cropInfos.Count; i++)
            {
                cropInfos[i].UpdateInfo(crops[i], capacity);
            }
        }
    }
}
