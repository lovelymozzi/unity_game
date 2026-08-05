using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public class AnimalUpgrade : MonoBehaviour
    {
        [SerializeField] private Transform slotContainer;
        [SerializeField] private AnimalUpgradeSlot slotPrefab;

        private List<AnimalUpgradeSlot> activeSlots = new List<AnimalUpgradeSlot>();

        private void Awake()
        {
            // Adjust the UI panel's position to be horizontally centered.
            var rect = GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);

            Hide();
        }

        public void RefreshUI()
        {
            foreach (var slot in activeSlots) Destroy(slot.gameObject);
            activeSlots.Clear();

            var allTypes = (AnimalType[])System.Enum.GetValues(typeof(AnimalType));

            foreach (var type in allTypes)
            {
                var config = Barn.Instance.GetConfig(type);
                if (config == null) continue;

                int level = IslandManager.Instance.AnimalLevels[(int)type];

                var newSlot = Instantiate(slotPrefab, slotContainer);
                newSlot.Setup(config, level, config.MaxLevel);
                activeSlots.Add(newSlot);
            }
        }

        public void Show(Barn barn)
        {
            gameObject.SetActive(true);
            RefreshUI();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
