using System;

namespace Hwi.Foundation.Assets
{
    internal interface IGroupDisposable : IDisposable
    {
        void DisposeInternal();
    }
}
