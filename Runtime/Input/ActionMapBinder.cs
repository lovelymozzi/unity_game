using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Hwi.Foundation.Input
{
    public sealed class ActionMapBinder : IDisposable
    {
        private readonly InputActionMap _map;
        private readonly List<(InputAction action, Action<InputAction.CallbackContext> cb)> _binds
            = new List<(InputAction, Action<InputAction.CallbackContext>)>();

        public ActionMapBinder(InputActionAsset asset, string actionMapName)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            _map = asset.FindActionMap(actionMapName);
            if (_map == null) throw new ArgumentException($"ActionMap '{actionMapName}' not found", nameof(actionMapName));
        }

        public void Bind(string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var action = _map.FindAction(actionName);
            if (action == null) throw new ArgumentException($"Action '{actionName}' not found in map '{_map.name}'", nameof(actionName));
            action.performed += callback;
            _binds.Add((action, callback));
        }

        public void Enable() => _map.Enable();
        public void Disable() => _map.Disable();

        public void Dispose()
        {
            foreach (var (action, cb) in _binds) action.performed -= cb;
            _binds.Clear();
            _map.Disable();
        }
    }
}
