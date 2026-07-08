using UnityEngine;

namespace Hwi.Foundation.Mobile
{
    /// <summary>
    /// 디바이스 메모리 클래스 분기 헬퍼. spec §2.2 (v0.4).
    /// Low: ram&lt;2048MB / Mid: 2048≤ram&lt;4096MB / High: ram≥4096MB (strict less-than).
    /// </summary>
    public static class DeviceTier
    {
        public enum Level { Low, Mid, High }

        public const int LowMaxMB = 2048;
        public const int MidMaxMB = 4096;

        private static Level? _cache;

        public static Level? Override { get; set; }

        public static Level Current
        {
            get
            {
                if (Override.HasValue) return Override.Value;
                if (_cache.HasValue) return _cache.Value;
                _cache = Compute(SystemInfo.systemMemorySize);
                return _cache.Value;
            }
        }

        /// <summary>테스트 노출. spec §2.2.2 경계 규약.</summary>
        internal static Level Compute(int memoryMB)
        {
            if (memoryMB < LowMaxMB) return Level.Low;
            if (memoryMB < MidMaxMB) return Level.Mid;
            return Level.High;
        }
    }
}
