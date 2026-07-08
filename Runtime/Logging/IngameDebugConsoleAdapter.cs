#if HWI_INGAME_CONSOLE
using System;
using UnityEngine;
using IngameDebugConsole;
using Hwi.Foundation.Core;

namespace Hwi.Foundation.Logging
{
    /// <summary>
    /// IFoundationLogger → yasirkula/IngameDebugConsole 라우터.
    /// Foundation.Logging.asmdef 의 versionDefines 가 com.yasirkula.ingamedebugconsole 1.0.0+ 감지 시
    /// HWI_INGAME_CONSOLE 심볼 자동 정의 → 본 클래스 컴파일.
    /// 실제 노출은 IngameDebugConsole 의 Debug.* 후크 — 본 adapter 는 tag prefix 일관성만 책임.
    /// </summary>
    public sealed class IngameDebugConsoleAdapter : IFoundationLogger
    {
        public void Log(string tag, string message)
            => Debug.Log($"[{tag}] {message}");

        public void LogWarning(string tag, string message)
            => Debug.LogWarning($"[{tag}] {message}");

        public void LogError(string tag, string message, Exception ex = null)
            => Debug.LogError(ex != null ? $"[{tag}] {message}\n{ex}" : $"[{tag}] {message}");
    }
}
#endif
