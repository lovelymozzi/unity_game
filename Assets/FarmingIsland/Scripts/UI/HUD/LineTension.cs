using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.FarmingIsland
{
    public class LineTension : MonoBehaviour
    {
        [SerializeField] private Image tensionImage;
        [SerializeField] private Gradient tensionGradient;

        void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Toggle(bool active)
        {
            gameObject.SetActive(active);
        }

        public void UpdateDisplay(float amount)
        {
            Vector3 scale = new Vector3(amount, 1f, 1f);
            tensionImage.transform.localScale = scale;
            tensionImage.color = tensionGradient.Evaluate(amount);
        }
    }
}
