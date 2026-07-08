using UnityEngine;

namespace Hwi.Foundation.Mobile
{
    /// <summary>
    /// 게임 시작 시 1회 호출되는 모바일 기본 세팅. RuntimeInitializeOnLoadMethod로 자동 적용.
    /// 인스펙터 노출이 필요한 경우 ScriptableObject 설정을 도입할 수 있지만, v0.1은 컴파일타임 상수.
    /// </summary>
    public static class MobileBootstrap
    {
        public static int TargetFrameRate { get; set; } = 60;
        public static bool DisableScreenSleep { get; set; } = true;
        public static bool DisableVSync { get; set; } = true;

        public static LowMemoryDispatcher LowMemory { get; } = new LowMemoryDispatcher();

        private static bool _applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoApply() => Apply();

        public static void Apply()
        {
            if (_applied) return;
            _applied = true;

            Application.targetFrameRate = TargetFrameRate;
            if (DisableScreenSleep) Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (DisableVSync) QualitySettings.vSyncCount = 0;

            Application.lowMemory += () => LowMemory.Dispatch();
        }

        public static bool HasNotch =>
            Screen.safeArea.width < Screen.width || Screen.safeArea.height < Screen.height ||
            Screen.safeArea.x > 0 || Screen.safeArea.y > 0;
    }
}
