using System;
using UnityEngine;
using Hwi.Foundation.Core;

namespace Hwi.Foundation.Logging
{
    /// <summary>UnityEngine.Debug 로 라우팅하는 기본 Logger. tag 는 메시지 prefix.</summary>
    public sealed class UnityDebugFoundationLogger : IFoundationLogger
    {
        public void Log(string tag, string message)
            => Debug.Log($"[{tag}] {message}");

        public void LogWarning(string tag, string message)
            => Debug.LogWarning($"[{tag}] {message}");

        public void LogError(string tag, string message, Exception ex = null)
            => Debug.LogError(ex != null ? $"[{tag}] {message}\n{ex}" : $"[{tag}] {message}");
    }
}
