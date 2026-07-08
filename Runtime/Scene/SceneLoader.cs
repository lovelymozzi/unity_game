using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hwi.Foundation.Scene
{
    public static class SceneLoader
    {
        public static UniTask LoadAsync(
            string sceneName,
            ITransition transition = null,
            IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("sceneName must not be null or empty", nameof(sceneName));
            return LoadInner(sceneName, transition, progress, ct);
        }

        public static UniTask LoadAdditiveAsync(
            string sceneName,
            IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("sceneName must not be null or empty", nameof(sceneName));
            return LoadAdditiveInner(sceneName, progress, ct);
        }

        public static UniTask UnloadAsync(string sceneName, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("sceneName must not be null or empty", nameof(sceneName));
            return UnloadInner(sceneName, ct);
        }

        private static async UniTask LoadInner(string sceneName, ITransition transition, IProgress<float> progress, CancellationToken ct)
        {
            if (transition != null)
                await transition.PlayOutAsync(ct);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
                throw new InvalidOperationException($"Scene '{sceneName}' not in Build Settings");
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                progress?.Report(Mathf.Clamp01(op.progress / 0.9f));
                await UniTask.Yield(ct);
            }
            progress?.Report(1f);

            op.allowSceneActivation = true;
            await UniTask.WaitUntil(() => op.isDone, cancellationToken: ct);

            var loaded = SceneManager.GetSceneByName(sceneName);
            await InvokeHooksAsync<IOnSceneReady>(loaded, h => h.OnSceneReadyAsync(ct));
            await InvokeHooksAsync<IAfterSceneLoaded>(loaded, h => h.OnAfterSceneLoadedAsync(ct));

            if (transition != null)
                await transition.PlayInAsync(ct);
        }

        private static async UniTask LoadAdditiveInner(string sceneName, IProgress<float> progress, CancellationToken ct)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
                throw new InvalidOperationException($"Scene '{sceneName}' not in Build Settings");
            while (!op.isDone)
            {
                progress?.Report(Mathf.Clamp01(op.progress));
                await UniTask.Yield(ct);
            }
            progress?.Report(1f);

            var loaded = SceneManager.GetSceneByName(sceneName);
            await InvokeHooksAsync<IAfterSceneLoaded>(loaded, h => h.OnAfterSceneLoadedAsync(ct));
        }

        private static async UniTask UnloadInner(string sceneName, CancellationToken ct)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return; // idempotent
            var op = SceneManager.UnloadSceneAsync(sceneName);
            if (op == null) return;
            while (!op.isDone) await UniTask.Yield(ct);
        }

        private static async UniTask InvokeHooksAsync<T>(UnityEngine.SceneManagement.Scene scene, Func<T, UniTask> invoke) where T : class
        {
            if (!scene.IsValid()) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var hooks = root.GetComponentsInChildren<T>(includeInactive: true);
                foreach (var h in hooks)
                {
                    try { await invoke(h); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        Hwi.Foundation.Core.FoundationContext.Logger?.LogError(
                            "Scene/Hook",
                            $"{typeof(T).Name} on {((MonoBehaviour)(object)h)?.name} threw",
                            ex);
                    }
                }
            }
        }
    }
}
