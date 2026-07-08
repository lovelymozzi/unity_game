using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hwi.Foundation.Scene
{
    public interface ITransition
    {
        UniTask PlayOutAsync(CancellationToken ct);
        UniTask PlayInAsync(CancellationToken ct);
    }
}
