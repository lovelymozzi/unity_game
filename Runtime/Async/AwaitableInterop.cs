using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hwi.Foundation.Async
{
    /// <summary>Unity 6 Awaitable ↔ UniTask 상호운용 헬퍼.</summary>
    public static class AwaitableInterop
    {
        public static UniTask AsUniTask(this Awaitable awaitable)
        {
            if (awaitable == null) throw new ArgumentNullException(nameof(awaitable));
            return AwaitInner(awaitable);
        }

        public static UniTask<T> AsUniTask<T>(this Awaitable<T> awaitable)
        {
            if (awaitable == null) throw new ArgumentNullException(nameof(awaitable));
            return AwaitInner(awaitable);
        }

        private static async UniTask AwaitInner(Awaitable a) { await a; }
        private static async UniTask<T> AwaitInner<T>(Awaitable<T> a) => await a;
    }
}
