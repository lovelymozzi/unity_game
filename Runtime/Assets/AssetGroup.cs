using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hwi.Foundation.Core;
using Hwi.Foundation.Mobile;

namespace Hwi.Foundation.Assets
{
    public sealed class AssetGroup : IDisposable
    {
        private readonly List<IGroupDisposable> _entries = new List<IGroupDisposable>();
        private Action _lowMemoryDispose;

        public int Count => _entries.Count;
        public bool IsDisposed { get; private set; }
        public bool ReleaseOnLowMemory { get; }

        public AssetGroup(bool releaseOnLowMemory = false)
        {
            ReleaseOnLowMemory = releaseOnLowMemory;
            if (releaseOnLowMemory)
            {
                _lowMemoryDispose = () => Dispose();
                MobileBootstrap.LowMemory.Subscribe(_lowMemoryDispose);
            }
        }

        public async UniTask<Result<T>> LoadAsync<T>(string key, CancellationToken ct = default)
            where T : UnityEngine.Object
        {
            if (IsDisposed) return Result<T>.Failure("AssetGroup is disposed");
            var hRes = await AssetHandle<T>.LoadAsync(key, ct);
            if (!hRes.IsOk) return Result<T>.Failure(hRes.Error);
            Adopt(hRes.Value);
            return Result<T>.Success(hRes.Value.Asset);
        }

        public void Adopt<T>(AssetHandle<T> handle) where T : UnityEngine.Object
        {
            if (IsDisposed) throw new InvalidOperationException("AssetGroup is disposed");
            if (handle == null) return;
            if (handle.OwnedByGroup) return;
            handle.OwnedByGroup = true;
            _entries.Add(handle);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            if (_lowMemoryDispose != null)
            {
                MobileBootstrap.LowMemory.Unsubscribe(_lowMemoryDispose);
                _lowMemoryDispose = null;
            }
            foreach (var e in _entries) e.DisposeInternal();
            _entries.Clear();
        }
    }
}
