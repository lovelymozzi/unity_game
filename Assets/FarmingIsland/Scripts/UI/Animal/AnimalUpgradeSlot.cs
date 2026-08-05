using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class AnimalUpgradeSlot : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rateText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button upgradeButton;

        public void Setup(AnimalConfig config, int currentLevel, int maxLevel)
        {
            if (config.Icon != null) iconImage.sprite = config.Icon;
            nameText.text = $"{config.DisplayName} (Lv. {currentLevel})";

            float currentSpeed = Barn.Instance.CalculateProductionTime(config.Type);

            rateText.text = $"Rate: {currentSpeed:0.##}s";

            if (currentLevel >= maxLevel)
            {
                priceText.text = "MAX";
                upgradeButton.interactable = false;
            }
            else
            {
                int cost = Barn.Instance.CalculateUpgradeCost(config.Type);
                priceText.text = cost.ToAbbreviatedString();

                upgradeButton.interactable = IslandManager.Instance.Coin >= cost;

                upgradeButton.onClick.RemoveAllListeners();
                upgradeButton.onClick.AddListener(() => Barn.Instance.TryUpgradeAnimal(config.Type));
            }
        }
    }
}
