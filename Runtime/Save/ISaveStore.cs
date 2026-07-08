using System.Threading;
using Cysharp.Threading.Tasks;
using Hwi.Foundation.Core;

namespace Hwi.Foundation.Save
{
    public interface ISaveStore
    {
        UniTask<Result<T>> LoadAsync<T>(string key, CancellationToken ct = default);
        UniTask<Result<bool>> SaveAsync<T>(string key, T value, CancellationToken ct = default);
        UniTask<Result<bool>> DeleteAsync(string key, CancellationToken ct = default);
        bool Has(string key);
    }
}
