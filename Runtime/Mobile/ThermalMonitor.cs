using UnityEngine;

namespace Hwi.Foundation.Mobile
{
    /// <summary>배터리·열 상태를 노출. 과열 시 frame rate를 자동 다운하는 옵션 헬퍼 포함.</summary>
    public sealed class ThermalMonitor : MonoBehaviour
    {
        [SerializeField] private bool autoDownscaleOnHot = true;
        [SerializeField] private int hotFrameRate = 30;
        [SerializeField] private float pollInterval = 5f;

        public float BatteryLevel => SystemInfo.batteryLevel; // -1 if unsupported
        public BatteryStatus BatteryStatus => SystemInfo.batteryStatus;

        private float _nextPollTime;
        private bool _downscaled;

        private void Update()
        {
            if (!autoDownscaleOnHot) return;
            if (Time.unscaledTime < _nextPollTime) return;
            _nextPollTime = Time.unscaledTime + pollInterval;

#if UNITY_IOS && !UNITY_EDITOR
            var thermal = UnityEngine.iOS.Device.thermalState;
            bool hot = thermal == UnityEngine.iOS.Device.ThermalState.Serious
                    || thermal == UnityEngine.iOS.Device.ThermalState.Critical;
#else
            bool hot = false;
#endif

            if (hot && !_downscaled)
            {
                Application.targetFrameRate = hotFrameRate;
                _downscaled = true;
            }
            else if (!hot && _downscaled)
            {
                Application.targetFrameRate = MobileBootstrap.TargetFrameRate;
                _downscaled = false;
            }
        }
    }
}
