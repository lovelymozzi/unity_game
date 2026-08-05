using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CryingSnow.FarmingIsland
{
    public class GraphicsManager : MonoBehaviour
    {
        public static GraphicsManager Instance { get; private set; }

        [Header("Pipeline Settings")]
        [SerializeField, Tooltip("Reference to the URP Asset to control render scale.")]
        private UniversalRenderPipelineAsset _URPAsset;

        public bool IsShadowsOn { get; private set; }
        public float CurrentRenderScale { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            InitializeGraphics();
        }

        private void InitializeGraphics()
        {
            // 1. Frame Rate Settings
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            // 2. Load Shadow Settings
            IsShadowsOn = PlayerPrefs.GetInt("Shadow ON", 1) > 0;
            ApplyShadows(IsShadowsOn);

            // 3. Load Render Scale Settings
            CurrentRenderScale = PlayerPrefs.GetFloat("Render Scale", 0.75f);
            ApplyRenderScale(CurrentRenderScale);
        }

        public void SetShadows(bool isOn)
        {
            IsShadowsOn = isOn;
            PlayerPrefs.SetInt("Shadow ON", isOn ? 1 : 0);
            ApplyShadows(isOn);
        }

        public void SetRenderScale(float sliderValue)
        {
            // Map slider (0–1) to render scale (0.5–1.0)
            float renderScale = Mathf.Lerp(0.5f, 1.0f, sliderValue);

            CurrentRenderScale = renderScale;
            PlayerPrefs.SetFloat("Render Scale", renderScale);
            ApplyRenderScale(renderScale);
        }

        private void ApplyShadows(bool isOn)
        {
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.shadows = isOn ? LightShadows.Hard : LightShadows.None;
            }
        }

        private void ApplyRenderScale(float scale)
        {
            if (_URPAsset != null)
            {
                _URPAsset.renderScale = scale;
            }
        }
    }
}
