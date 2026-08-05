using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public class Merchant : MonoBehaviour
    {
        [SerializeField] private Vector3 displayOffset = new Vector3(0f, 4f, 0f);

        public Vector3 DisplayOffset => displayOffset;
        public List<ItemData> Items => stall.AcceptedItems;

        private Stall stall;
        private MerchantInfo info;

        void Awake()
        {
            stall = GetComponentInParent<Stall>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                info = UIManager.Instance.DisplayMerchantInfo(this);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (info != null) Destroy(info.gameObject);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var displayPos = transform.position + displayOffset;
            Gizmos.DrawWireCube(displayPos, new Vector3(4f, 1.5f, 0.1f));
        }
#endif
    }
}
