using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Hwi.Foundation.Core;

namespace Hwi.Foundation.Save
{
    public sealed class JsonFileSaveStore : ISaveStore
    {
        private readonly string _baseDir;

        public JsonFileSaveStore(string subdirectory = "saves")
        {
            _baseDir = Path.Combine(Application.persistentDataPath, subdirectory);
        }

        private string PathFor(string key)
        {
            var sanitized = key;
            foreach (var c in Path.GetInvalidFileNameChars()) sanitized = sanitized.Replace(c, '_');
            sanitized = sanitized.Replace('/', '_').Replace('\\', '_');
            return Path.Combine(_baseDir, sanitized + ".json");
        }

        public bool Has(string key) => File.Exists(PathFor(key));

        public async UniTask<Result<T>> LoadAsync<T>(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var path = PathFor(key);
            if (!File.Exists(path)) return Result<T>.Failure($"key not found: {key}");
            try
            {
                var json = await File.ReadAllTextAsync(path, ct);
                return Result<T>.Success(JsonUtility.FromJson<T>(json));
            }
            catch (System.OperationCanceledException) { throw; }
            catch (System.Exception ex)
            {
                return Result<T>.Failure(ex.Message);
            }
        }

        public async UniTask<Result<bool>> SaveAsync<T>(string key, T value, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!Directory.Exists(_baseDir)) Directory.CreateDirectory(_baseDir);
                var path = PathFor(key);
                var tmp = path + ".tmp";
                var json = JsonUtility.ToJson(value);
                await File.WriteAllTextAsync(tmp, json, ct);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return Result<bool>.Success(true);
            }
            catch (System.OperationCanceledException) { throw; }
            catch (System.Exception ex)
            {
                return Result<bool>.Failure(ex.Message);
            }
        }

        public UniTask<Result<bool>> DeleteAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var path = PathFor(key);
            if (!File.Exists(path)) return UniTask.FromResult(Result<bool>.Success(false));
            try
            {
                File.Delete(path);
                return UniTask.FromResult(Result<bool>.Success(true));
            }
            catch (System.Exception ex)
            {
                return UniTask.FromResult(Result<bool>.Failure(ex.Message));
            }
        }
    }
}
