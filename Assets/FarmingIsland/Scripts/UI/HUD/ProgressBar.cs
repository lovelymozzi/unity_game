using UnityEngine;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class ProgressBar : MonoBehaviour
    {
        private TMP_Text progressTitle;
        private SlicedFilledImage progressFill;

        void Awake()
        {
            progressTitle = GetComponentInChildren<TMP_Text>();
            progressFill = GetComponentInChildren<SlicedFilledImage>();

            gameObject.SetActive(false);
        }

        public void Toggle(string message)
        {
            progressTitle.text = message;
            UpdateDisplay(0f);
            gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        public void UpdateDisplay(float progress)
        {
            progressFill.fillAmount = progress;
        }
    }
}
