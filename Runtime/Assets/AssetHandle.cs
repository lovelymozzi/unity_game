using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Hwi.Foundation.Core;

namespace Hwi.Foundation.Assets
{
    public sealed class AssetHandle<T> : IDisposable, IGroupDisposable where T : UnityEngine.Object
    {
        public T Asset { get; private set; }
        public string Key { get; private set; }
        public bool IsDisposed { get; private set; }

        internal bool OwnedByGroup { get; set; }

        private AsyncOperationHandle<T> _op;
        private bool _hasOp;

        private AssetHandle() { }

        public static UniTask<Result<AssetHandle<T>>> LoadAsync(string key, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(key))
                return UniTask.FromResult(Result<AssetHandle<T>>.Failure("key must not be null or empty"));
            return LoadInner(key, ct);
        }

        private static async UniTask<Result<AssetHandle<T>>> LoadInner(string key, CancellationToken ct)
        {
            var op = Addressables.LoadAssetAsync<T>(key);
            try
            {
                await op.WithCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                if (op.IsValid()) Addressables.Release(op);
                throw;
            }

            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                var err = op.OperationException?.Message ?? $"Addressables load failed for '{key}'";
                if (op.IsValid()) Addressables.Release(op);
                return Result<AssetHandle<T>>.Failure(err);
            }

            var h = new AssetHandle<T> { Asset = op.Result, Key = key, _op = op, _hasOp = true };
            return Result<AssetHandle<T>>.Success(h);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            if (OwnedByGroup) return;
            DisposeInternal();
        }

        void IGroupDisposable.DisposeInternal() => DisposeInternal();

        internal void DisposeInternal()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            if (_hasOp && _op.IsValid()) Addressables.Release(_op);
            Asset = null;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal static AssetHandle<T> CreateForTest(T asset, string key, AsyncOperationHandle<T> op)
        {
            return new AssetHandle<T> { Asset = asset, Key = key, _op = op, _hasOp = false };
        }
#endif
    }
}
