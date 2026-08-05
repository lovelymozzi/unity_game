# MatchDefense — Codex Project Instructions

## Project baseline

- Use Unity 6.5 (`6000.5.5f1`) and target a 2D project.
- Use `SpriteRenderer` for game objects. Base world framing and dynamic aspect-ratio behavior on the main camera's orthographic size; use `Foundation.UI.OrthographicCameraFitter` when applicable.
- Use uGUI (`Canvas`) and TextMeshPro (`TMP_Text`) for UI.
- When Unity Screen Navigator is installed, use its `Page` and `Modal` types for screen and modal flows; do not invent a parallel screen stack.

## Package and code conventions

- Check `Packages/manifest.json` before using a third-party package. Do not write code against packages that are not installed.
- Prefer `UniTask` for asynchronous game flows when the package is available. Do not add coroutines or `Task`-based wrappers solely to duplicate an existing UniTask flow.
- Load and release addressable assets through the project's established lifetime-binding pattern. Validate Addressables settings and keys before claiming an asset load is correct.
- Use `R3` only when it is installed. Keep state ownership singular and dispose subscriptions with the owning object's lifetime.
- Use DOTween for UI and gameplay motion when installed. For flows that must run while `timeScale` is zero, use unscaled updates deliberately.
- Use `UnityEngine.Pool.ObjectPool` for frequently created and destroyed objects. Cache component references in initialization; do not call `GetComponent` or `Find` every frame.

## Required project settings

- Keep `USN_USE_ASYNC_METHODS` enabled if Unity Screen Navigator async lifecycle methods are used.
- Enable `UNITASK_DOTWEEN_SUPPORT` only when DOTween is installed; otherwise it can break compilation.
- Preserve existing stripping and linker settings unless the requested change requires modifying them.

## C# style

- Use `PascalCase` for public types, methods, properties, and serialized fields when project conventions require it.
- Use `_camelCase` for private and protected fields.
- Keep gameplay data separate from UI presentation. Typical responsibilities are: data loading, game/core management, controller/object behavior, and UI/view behavior.
- Provide complete, compilable C# when implementing code; do not leave placeholder omissions in a change intended to build.

## Verification

- After C# or asset changes, check the Unity Console and confirm compilation succeeds.
- For scene, prefab, or serialized-field changes, inspect the resulting object state rather than relying only on source edits.
- Use the installed Codex skills when applicable:
  - `hwi-unity-cli` for Unity Editor inspection, changes, and validation.
  - `hwi-unity-playtest` for uGUI Play Mode interaction tests.
  - `hwi-unity-ai-image-gen` for Unity AI-generated assets; obtain approval before credit-consuming generation.

## Current project dependencies

- HWI Foundation: `com.hwi.foundation` (Git dependency)
- Unity AI Assistant: `com.unity.ai.assistant`
- UniTask: `com.cysharp.unitask`
- uGUI: `com.unity.ugui`

Keep `CLAUDE.md` as the historical source document. Apply this `AGENTS.md` for Codex work in this repository.
