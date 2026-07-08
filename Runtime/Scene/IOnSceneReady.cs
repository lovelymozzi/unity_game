using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hwi.Foundation.Scene
{
    public interface IOnSceneReady
    {
        UniTask OnSceneReadyAsync(CancellationToken ct);
    }
}
