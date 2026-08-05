using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public class MerchantInfo : MonoBehaviour
    {
        [SerializeField] private ItemPriceInfo itemPriceInfoPrefab;
        [SerializeField] private Transform itemsParent;

        private Camera mainCamera;
        private Merchant merchant;

        public void Initialize(Merchant merchant)
        {
            foreach (var item in merchant.Items)
            {
                var info = Instantiate(itemPriceInfoPrefab, itemsParent);
                info.Initialize(item);
            }

            mainCamera = Camera.main;
            this.merchant = merchant;
        }

        private void LateUpdate()
        {
            if (mainCamera == null || merchant == null) return;

            var targetPos = merchant.transform.TransformPoint(merchant.DisplayOffset);
            transform.position = mainCamera.WorldToScreenPoint(targetPos);
        }
    }
}
