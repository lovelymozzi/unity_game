using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(Image))]
    public class SettingsWindow : MonoBehaviour
    {
        [SerializeField, Tooltip("Main panel of the settings window.")]
        private RectTransform mainPanel;

        [Header("Audios")]
        [SerializeField, Tooltip("Toggle to enable or disable sound effects (SFX).")]
        private Toggle sfxToggle;

        [SerializeField, Tooltip("Toggle to enable or disable background music (BGM).")]
        private Toggle bgmToggle;

        [Header("Haptics")]
        [SerializeField, Tooltip("Toggle to enable or disable vibration.")]
        private Toggle vibrationToggle;

        [Header("Graphics")]
        [SerializeField, Tooltip("Toggle to enable or disable shadow.")]
        private Toggle shadowToggle;

        [SerializeField, Tooltip("Slider to set the game's render scale.")]
        private Slider renderScaleSlider;

        private void Start()
        {
            InitializeWindowVisuals();
            InitializeUIValues();
            RegisterEvents();

            gameObject.SetActive(false);
        }

        private void InitializeWindowVisuals()
        {
            GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);
            mainPanel.anchoredPosition = Vector2.zero;
        }

        private void InitializeUIValues()
        {
            if (AudioManager.Instance != null)
            {
                sfxToggle.isOn = AudioManager.Instance.IsSFXOn;
                bgmToggle.isOn = AudioManager.Instance.IsBGMOn;
            }

            if (GraphicsManager.Instance != null)
            {
                shadowToggle.isOn = GraphicsManager.Instance.IsShadowsOn;
                renderScaleSlider.value = Mathf.InverseLerp(0.5f, 1.0f, GraphicsManager.Instance.CurrentRenderScale);
            }

            vibrationToggle.isOn = PlayerPrefs.GetInt("Vibration ON", 1) > 0;
        }

        private void RegisterEvents()
        {
            // Audio Events
            sfxToggle.onValueChanged.AddListener((isOn) => AudioManager.Instance?.SetSFX(isOn));
            bgmToggle.onValueChanged.AddListener((isOn) => AudioManager.Instance?.SetBGM(isOn));

            // Graphics Events
            shadowToggle.onValueChanged.AddListener((isOn) => GraphicsManager.Instance?.SetShadows(isOn));
            renderScaleSlider.onValueChanged.AddListener((val) => GraphicsManager.Instance?.SetRenderScale(val));

            // Haptic Event
            vibrationToggle.onValueChanged.AddListener((isOn) =>
            {
                PlayerPrefs.SetInt("Vibration ON", isOn ? 1 : 0);
            });
        }
    }
}
