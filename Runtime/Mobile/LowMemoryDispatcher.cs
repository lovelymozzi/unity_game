using System;
using System.Collections.Generic;
using Hwi.Foundation.Core;

namespace Hwi.Foundation.Mobile
{
    /// <summary>저메모리 신호의 listener fan-out. MobileBootstrap이 Application.lowMemory를 이곳에 전달.</summary>
    public sealed class LowMemoryDispatcher
    {
        private readonly List<Action> _listeners = new List<Action>();

        public void Subscribe(Action listener)
        {
            if (listener == null) return;
            _listeners.Add(listener);
        }

        public void Unsubscribe(Action listener)
        {
            if (listener == null) return;
            _listeners.Remove(listener);
        }

        public void Dispatch()
        {
            // ToArray로 enumeration 중 변경 안전
            foreach (var l in _listeners.ToArray())
            {
                try { l(); }
                catch (Exception ex) { FoundationContext.Logger.LogError("Mobile.LowMemory", "listener threw", ex); }
            }
        }

        public int Count => _listeners.Count;
    }
}
