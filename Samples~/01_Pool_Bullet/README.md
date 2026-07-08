# 01 Pool — 2D Bullet

Demonstrates `PrefabPool` with a 2D shooter.

## Run
1. Import the sample from Package Manager.
2. Open `PoolBulletScene.unity`.
3. Press Play. Bullets fire from the origin every 0.1s, expire after 2s.

## What to verify
- After ~1s, no GameObject is destroyed — same instances cycle (visible in the Hierarchy: same set of `Bullet2D(Clone)` entries toggling active/inactive).
- Profiler shows steady, low allocation in steady state. (The sample uses `StartCoroutine(ReleaseAfter(...))` for simplicity, which boxes an IEnumerator each fire — true 0-GC release will come once `Foundation.Async` lands in v0.2 with `UniTask.Delay`.)

## Adapt for 3D
Swap `Bullet2D.prefab` for a 3D prefab (Sphere + Rigidbody + SphereCollider), and replace `Rigidbody2D` references in `Shooter2D.cs` with `Rigidbody`. No other change.
