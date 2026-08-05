using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ProductInfo : MonoBehaviour
    {
        [SerializeField] private Image iconImage;

        private CanvasGroup canvasGroup;
        private Camera mainCamera;
        private Transform animal;
        private Vector3 offset;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            mainCamera = Camera.main;
        }

        public void Initialize(Transform animal, Vector3 offset, Sprite icon)
        {
            this.animal = animal;
            this.offset = offset;
            iconImage.sprite = icon;
        }

        private void LateUpdate()
        {
            if (animal == null || mainCamera == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 worldPos = animal.TransformPoint(offset);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            // Behind camera
            if (screenPos.z <= 0f)
            {
                SetVisible(false);
                return;
            }

            // Outside screen bounds
            bool offScreen =
                screenPos.x < 0f || screenPos.x > Screen.width ||
                screenPos.y < 0f || screenPos.y > Screen.height;

            SetVisible(!offScreen);
            transform.position = screenPos;
        }

        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
}
