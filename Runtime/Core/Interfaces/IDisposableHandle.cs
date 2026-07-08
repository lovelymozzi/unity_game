using System;

namespace Hwi.Foundation.Core
{
    /// <summary>Disposable that also reports whether it has been released.</summary>
    public interface IDisposableHandle : IDisposable
    {
        bool IsDisposed { get; }
    }
}
