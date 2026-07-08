using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hwi.Foundation.Async
{
    public static class DelayAsync
    {
        /// <summary>지정 프레임 수만큼 대기. UniTask.DelayFrame 래퍼.</summary>
        public static UniTask DelayFramesAsync(int frames, CancellationToken ct)
        {
            return UniTask.DelayFrame(frames, cancellationToken: ct);
        }
    }
}
