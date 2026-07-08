using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Hwi.Foundation.Core;

namespace Hwi.Foundation.Save
{
    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public bool Has(string key) => PlayerPrefs.HasKey(key);

        public UniTask<Result<T>> LoadAsync<T>(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (!PlayerPrefs.HasKey(key))
                return UniTask.FromResult(Result<T>.Failure($"key not found: {key}"));
            try
            {
                var json = PlayerPrefs.GetString(key);
                var value = JsonUtility.FromJson<T>(json);
                return UniTask.FromResult(Result<T>.Success(value));
            }
            catch (System.Exception ex)
            {
                return UniTask.FromResult(Result<T>.Failure(ex.Message));
            }
        }

        public UniTask<Result<bool>> SaveAsync<T>(string key, T value, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = JsonUtility.ToJson(value);
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
                return UniTask.FromResult(Result<bool>.Success(true));
            }
            catch (System.Exception ex)
            {
                return UniTask.FromResult(Result<bool>.Failure(ex.Message));
            }
        }

        public UniTask<Result<bool>> DeleteAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (!PlayerPrefs.HasKey(key)) return UniTask.FromResult(Result<bool>.Success(false));
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return UniTask.FromResult(Result<bool>.Success(true));
        }
    }
}
