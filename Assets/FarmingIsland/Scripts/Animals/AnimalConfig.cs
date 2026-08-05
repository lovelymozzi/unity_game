using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public enum AnimalType
    {
        Poultry,
        Dairy,
        Fiber
    }

    [CreateAssetMenu(fileName = "AnimalConfig_", menuName = "Animal Config")]
    public class AnimalConfig : ScriptableObject
    {
        [Header("Identity")]
        public AnimalType Type;
        public string DisplayName;
        public Sprite Icon;

        [Header("Production Stats")]
        public float BaseProductionTime = 60f;
        public float TimeReductionPerLevel = 0.05f;

        [Header("Upgrade Economics")]
        public int BaseUpgradePrice = 1500;
        public float PriceMultiplier = 1.1f;
        public int MaxLevel = 10;
    }
}
