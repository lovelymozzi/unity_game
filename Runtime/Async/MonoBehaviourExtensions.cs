using System.Threading;
using UnityEngine;

namespace Hwi.Foundation.Async
{
    public static class MonoBehaviourExtensions
    {
        /// <summary>Unity 6 destroyCancellationToken 의 명시적 래퍼.</summary>
        public static CancellationToken GetCancellationTokenOnDestroy(this MonoBehaviour mb)
        {
            return mb.destroyCancellationToken;
        }
    }
}
