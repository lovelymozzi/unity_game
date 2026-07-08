using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hwi.Foundation.Scene
{
    public interface IAfterSceneLoaded
    {
        UniTask OnAfterSceneLoadedAsync(CancellationToken ct);
    }
}
