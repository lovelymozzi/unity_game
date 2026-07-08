using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hwi.Foundation.Async
{
    /// <summary>fire-and-forget UniTask 의 silent swallow 방지.</summary>
    public static class UniTaskExtensions
    {
        public static async void ForgetWithLog(this UniTask task)
        {
            try { await task; }
            catch (Exception ex) { Debug.LogError($"[ForgetWithLog] {ex}"); }
        }

        public static async void ForgetWithLog<T>(this UniTask<T> task)
        {
            try { await task; }
            catch (Exception ex) { Debug.LogError($"[ForgetWithLog] {ex}"); }
        }
    }
}
