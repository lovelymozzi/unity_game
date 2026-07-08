using UnityEngine;
using UnityEngine.UI;

namespace Hwi.Foundation.UI
{
    /// <summary>
    /// CanvasScaler 자동 세팅 ScriptableObject. 세로/가로 프리셋 두 종을 ResetVertical / ResetHorizontal 메뉴에서 생성한다.
    /// </summary>
    [CreateAssetMenu(menuName = "HWI Foundation/UI/Canvas Scaler Preset", fileName = "CanvasScalerPreset")]
    public sealed class CanvasScalerPreset : ScriptableObject
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 1920);
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
        [SerializeField] private CanvasScaler.ScreenMatchMode matchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        [SerializeField] private CanvasScaler.ScaleMode scaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        public void ApplyTo(CanvasScaler scaler)
        {
            if (scaler == null) return;
            scaler.uiScaleMode = scaleMode;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = matchMode;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        [ContextMenu("Reset to Vertical (1080x1920)")]
        private void ResetVertical()
        {
            referenceResolution = new Vector2(1080, 1920);
            matchWidthOrHeight = 0.5f;
        }

        [ContextMenu("Reset to Horizontal (1920x1080)")]
        private void ResetHorizontal()
        {
            referenceResolution = new Vector2(1920, 1080);
            matchWidthOrHeight = 0.5f;
        }
    }
}
