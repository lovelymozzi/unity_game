using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cinemachine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class Barn : MonoBehaviour, IProp
    {
        public static Barn Instance { get; private set; }

        [Header("Database")]
        [SerializeField, Tooltip("List of animal config used to calculate production time and upgrades.")]
        private List<AnimalConfig> animalConfigs;

        [Header("Reference")]
        [SerializeField, Tooltip("Virtual camera used to focus on the barn when the player is near.")]
        private CinemachineVirtualCamera virtualCamera;

        public Location Location => Location.Barn;

        private void Awake()
        {
            Instance = this;
            transform.localScale = Vector3.zero;
        }

        void IProp.Animate(bool instant, System.Action onFinished)
        {
            if (instant)
            {
                transform.localScale = Vector3.one;
                onFinished?.Invoke();
                return;
            }

            transform.DOScale(Vector3.one, 1f)
                .SetEase(Ease.OutBounce)
                .OnComplete(() => onFinished?.Invoke());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                UIManager.Instance.AnimalUpgrade.Show(this);
                virtualCamera.gameObject.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                UIManager.Instance.AnimalUpgrade.Hide();
                virtualCamera.gameObject.SetActive(false);
            }
        }

        public AnimalConfig GetConfig(AnimalType type)
        {
            return animalConfigs.FirstOrDefault(c => c.Type == type);
        }

        public float CalculateProductionTime(AnimalType type)
        {
            var config = GetConfig(type);
            if (config == null) return 60f;

            int level = IslandManager.Instance.AnimalLevels[(int)type];
            float reduction = config.BaseProductionTime * level * config.TimeReductionPerLevel;
            return Mathf.Max(1f, config.BaseProductionTime - reduction);
        }

        public int CalculateUpgradeCost(AnimalType type)
        {
            var config = GetConfig(type);
            int level = IslandManager.Instance.AnimalLevels[(int)type];
            return Mathf.RoundToInt(Mathf.Round(config.BaseUpgradePrice * Mathf.Pow(config.PriceMultiplier, level)) / 5f) * 5;
        }

        public void TryUpgradeAnimal(AnimalType type)
        {
            var config = GetConfig(type);
            int index = (int)type;
            int currentLevel = IslandManager.Instance.AnimalLevels[index];

            if (currentLevel >= config.MaxLevel) return;

            int cost = CalculateUpgradeCost(type);
            if (IslandManager.Instance.Coin >= cost)
            {
                IslandManager.Instance.Coin -= cost;
                IslandManager.Instance.AnimalLevels[index]++;
                AudioManager.Instance.PlaySFX(AudioID.Cash);
                UIManager.Instance.AnimalUpgrade.RefreshUI();
            }
        }
    }
}
