using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class DockButton : MonoBehaviour
    {
        private Button button;
        private Image image;

        void Awake()
        {
            // Dynamically adds a Button component to this GameObject and gets its Image component.
            button = gameObject.AddComponent<Button>();
            image = button.image;
        }

        /// <summary>
        /// Initializes the button with a specified click action.
        /// </summary>
        /// <param name="onClick">The action to invoke when the button is clicked.</param>
        public void Initialize(System.Action onClick)
        {
            // Adds the given action as a listener to the button's onClick event.
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        void OnEnable()
        {
            // When the button is enabled, start a color transition animation that loops indefinitely.
            image.DOColor(Color.yellow, 0.25f).SetLoops(-1, LoopType.Yoyo);

            // Ensure the button is active and can be interacted with.
            button.enabled = true;
        }

        void OnDisable()
        {
            // Stop any running color animations when the button is disabled.
            DOTween.Kill(image);

            // Reset the button color to white.
            image.color = Color.white;

            // Disable the button to prevent interactions.
            button.enabled = false;
        }
    }
}
