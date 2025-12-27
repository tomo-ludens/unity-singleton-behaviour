# Unity SingletonBehaviour

[Japanese](../ja/README.md) | [English](./README.md)

A **policy-driven singleton base class** for MonoBehaviour.

Intended for use with Unity 6.3 (6000.3 series) or later.

## Overview ✨

This library provides two singleton base classes:

| Class | Persistence | Auto-create | Use Case |
| --- | --- | --- | --- |
| `PersistentSingletonBehaviour<T>` | ✅ `DontDestroyOnLoad` | ✅ Yes | Game-wide managers |
| `SceneSingletonBehaviour<T>` | ❌ No | ❌ No | Scene-specific controllers |

Common features:

- 🧩 **Type-per-singleton guarantee** (e.g., `GameManager` and `AudioManager` are separate instances)
- 🛡️ **Type-safe inheritance** (CRTP-style constraint + runtime guard to catch misuse)
- 🧯 **Shutdown safety** (prevents re-creation via `Application.quitting`)
- ⚙️ **Domain Reload disabled support** (invalidates per-type static cache using Play session identifier)
- 🧱 **Practical tolerance for misplacement** (reparents to root and persists even if placed as a child object — Persistent only)
- 🧼 **Soft reset oriented** (runs `OnSingletonAwake()` each Play session, enabling re-initialization even for the same surviving instance)
- 🖥️ **Edit Mode safe** (search only in Edit Mode, no side effects on static cache)
- 🎯 **Exact-type enforcement** (rejects derived types; enforces T is the concrete type)
- 🚦 **Auto-create blocked if inactive exists (DEV/EDITOR)** to prevent hidden duplicates
- 🧵 **Main-thread guard** (public API asserts main thread in Play Mode)

## Requirements ✅

- Unity 6.3 (6000.3.x) or later
- Designed to remain stable even when **Reload Domain is disabled** via Enter Play Mode Options
- (Optional) Supports re-initialization each Play session even when Reload Scene is disabled

## Installation 📦

- Add the `Singletons/` folder to your project (e.g., `Assets/Plugins/Singletons/`).
- Adjust the namespace according to your project conventions.

### Namespace Import
```csharp
using Singletons;
```

## Usage 🚀

### 1) Persistent Singleton (PersistentSingletonBehaviour)

Survives scene loads and auto-creates if missing.

```csharp
using Singletons;

public sealed class GameManager : PersistentSingletonBehaviour<GameManager>
{
    public int Score { get; private set; }

    protected override void OnSingletonAwake()
    {
        // Initialization that runs every Play session
        this.Score = 0;
    }

    public void AddScore(int value) => this.Score += value;

    protected override void OnSingletonDestroy()
    {
        // Cleanup when actually destroyed
    }
}
```

### 2) Scene-scoped Singleton (SceneSingletonBehaviour)

Must be placed in scene. Destroyed on scene unload, no auto-creation.

```csharp
using Singletons;

public sealed class LevelController : SceneSingletonBehaviour<LevelController>
{
    protected override void OnSingletonAwake()
    {
        // Per-scene initialization
    }
}
```

> ⚠️ **Accessing `Instance` without a placed instance throws in DEV/EDITOR**, returns `null` in Player.

### 3) Choosing between `Instance` and `TryGetInstance`

| Item | Recommendation |
| --- | --- |
| Class modifier | `sealed` (prevents unintended inheritance) |
| Initialization | Put in `OnSingletonAwake()` (re-initialization per Play) |
| Cleanup | Put in `OnSingletonDestroy()` (destruction only) |

* ✅ **Instance**: the dependency is required (Persistent will create if missing)
  Example: managers essential to game flow such as `GameManager` / `AudioManager`

* ✅ **TryGetInstance**: use only if present / do nothing if missing / avoid creation during shutdown or teardown
  Example: cleanup paths such as `OnDisable` / `OnDestroy` / `OnApplicationPause`, unregistering optional features

```csharp
// Safe teardown pattern
private void OnDisable()
{
    if (AudioManager.TryGetInstance(out var am))
    {
        am.Unregister(this);
    }
}
```

### 4) Access pattern (cache it) 🧠

❌ Calling `Instance` every frame is not recommended. Because a Find operation may occur, the standard practice is to get it once, cache the reference, and reuse it.

✅ Recommended: acquire once and cache
```csharp
using Singletons;
using UnityEngine;

public sealed class ScoreHUD : MonoBehaviour
{
    private GameManager _gm;

    private void Start()
    {
        this._gm = GameManager.Instance; // cache
    }

    private void Update()
    {
        if (this._gm == null) return;
        // use this._gm.Score
    }
}
```

## Public API 📌

### `static T Instance { get; }`

Returns the singleton instance.

| State | Persistent | Scene |
| --- | --- | --- |
| Instance exists | Cached instance | Cached instance |
| Missing | Search → **auto-create** if not found | Search → **throw (DEV/EDITOR)** or `null` (Player) |
| Quitting | `null` | `null` |
| Edit Mode | Search only (no create, no cache) | Search only (no create, no cache) |
| Inactive exists (DEV/EDITOR) | Throws to avoid hidden duplicate | Throws to avoid hidden duplicate |
| Derived type found | `null` (Play: destroy, Edit: log only) | `null` (Play: destroy, Edit: log only) |

### `static bool TryGetInstance(out T instance)`

Gets the singleton instance without creating one.

| State | Return | `instance` |
| --- | --- | --- |
| Instance exists | `true` | Valid reference |
| Missing | `false` | `null` |
| Quitting | `false` | `null` |
| Edit Mode | Search result | Search only (no cache) |
| Derived type found | `false` | `null` (Play: destroy, Edit: log only) |

## Design Intent 🧠

### Why the policy pattern?

By separating singleton behavior (persistence, auto-creation) into policies, we share the same core logic while providing purpose-specific classes.

```csharp
public interface ISingletonPolicy
{
    bool PersistAcrossScenes { get; }
    bool AutoCreateIfMissing { get; }
}
```

### Why is `SingletonRuntime` necessary?

When Domain Reload is disabled, **static fields and static event handlers can persist across Play sessions**.

Given this "persistence," the per-type static cache must be invalidated at the start of each Play session.

To achieve this:

* `PlaySessionId` is updated in a non-generic location that reliably runs at Play start (`SubsystemRegistration`)
* `SingletonBehaviour<T, TPolicy>` references `PlaySessionId` to **invalidate its cache**

This separation of responsibilities is maintained.

> Note: Unity has known cases where `[RuntimeInitializeOnLoadMethod]` inside generic types is not invoked as expected.
> Centralizing initialization in a non-generic type is a practical workaround (see Issue Tracker).

### DontDestroyOnLoad Call Management

While calling `DontDestroyOnLoad` multiple times on the same object is harmless,
this implementation uses the `_isPersistent` flag to limit the call to once, avoiding unnecessary processing.

## Constraints ⚠️

### ❌ Overriding `Awake()` / `OnEnable()` / `OnDestroy()` requires calling base

The base class Unity message methods are responsible for:

* Establishing `_instance` and rejecting duplicates
* Detecting Play session and invalidating static cache
* Reparenting to root (to satisfy `DontDestroyOnLoad` requirements — Persistent only)
* Applying `DontDestroyOnLoad` (Persistent only)
* Controlling `OnSingletonAwake` / `OnSingletonDestroy` invocation

If you override these in a derived class, **always call `base.Awake()` etc.**
The recommended approach is to use `OnSingletonAwake()` / `OnSingletonDestroy()`.

```csharp
// OK: calling base
protected override void Awake()
{
    base.Awake();
    // additional processing
}

// Recommended: use OnSingletonAwake
protected override void OnSingletonAwake()
{
    // initialization
}
```

### ❌ Type parameter must be the class itself

The CRTP constraint causes the following incorrect inheritance to produce a compile error:
```csharp
// ❌ Compile error
public sealed class A : PersistentSingletonBehaviour<B> { }

// ✅ Correct implementation
public sealed class A : PersistentSingletonBehaviour<A> { }
```

## Scene Placement Notes 🧱

| Constraint | Reason |
| --- | --- |
| Do not place the same singleton in multiple scenes | The later-initialized one will be destroyed (first wins) |
| Root GameObject is preferable (Persistent) | `DontDestroyOnLoad` only works on root |

This implementation automatically reparents a child-placed singleton to root and persists it (Persistent only).
Since unintended moves can be confusing, a warning is emitted only in **Editor/Development builds**.

## Edit Mode Behavior 🖥️

In Edit Mode (`Application.isPlaying == false`), the following behavior applies:

* `Instance` / `TryGetInstance` perform **search only** (`FindAnyObjectByType`)
* **No auto-creation**
* **No static cache updates** (zero side effects)
* **No impact on Play session state**
* **Derived types found are logged only, not destroyed** (avoids Undo/Inspector side effects)

This allows safe singleton access from editor scripts and custom inspectors.

## Soft Reset (Safe Re-initialization Per Play Session) 🧼

This implementation is designed with "reusing the same instance while running initialization each Play session" in mind.

With Domain Reload disabled, static state and static event subscriptions can persist, so `OnSingletonAwake()` should be written to be **idempotent (safe to re-run)**.

> Practical tip: For static event subscriptions, use the "unsubscribe → subscribe" pattern to prevent duplicate subscriptions when Domain Reload is disabled.

## Threading / Main Thread 🧵

`Instance` / `TryGetInstance` call UnityEngine APIs internally (Find / GameObject creation, etc.).
Call them from the **main thread**.

## Initialization Order ⏱️

If dependency order becomes complex, you can pin it with a Bootstrap pattern:
```csharp
using Singletons;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class Bootstrap : MonoBehaviour
{
    private void Awake()
    {
        _ = GameManager.Instance;
        _ = AudioManager.Instance;
        _ = InputManager.Instance;
    }
}
```

## Dependencies 🔍

| API | Default Behavior |
| --- | --- |
| `Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` | **Does not return assets / inactive objects / `HideFlags.DontSave`** |
| `Object.DontDestroyOnLoad()` | **Only works on root GameObjects (or components on root GameObjects)** |
| `Application.quitting` | **Invoked when exiting Play Mode in Editor**. May not be called on mobile due to OS behavior |
| `RuntimeInitializeLoadType.SubsystemRegistration` | **Called before the first scene is loaded** (execution order is undefined) |
| `Time.frameCount` | **Resets to 0 when entering Play Mode**. Used to guard duplicate initialization |
| `Application.isPlaying` | **`true` in Play Mode, `false` in Edit Mode** |
| Domain Reload disabled | **Static field values / static event handlers persist across Play sessions** |
| Scene Reload disabled | **`OnEnable` / `OnDisable` / `OnDestroy` etc. are called as if newly loaded** |

## IDE Configuration (Rider / ReSharper) 🧰

### `StaticMemberInGenericType` warning

Rider/ReSharper may warn about static members in generic types.
This warns that "static fields are separated per closed generic type," which is **intentional** for type-per-singleton designs.

Depending on team policy, you can prefer adjusting inspection severity via shared `.DotSettings` rather than scattering suppression comments.

## Testing (PlayMode Tests) 🧪

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` also runs when starting PlayMode tests.

* ✅ `PlaySessionId` is updated between test runs, so cached statics are invalidated in the normal case
* ⚠️ If you need test-specific initialization, add guards for the test environment

## Platform Notes 📱

### Mobile (Android / iOS)

`Application.quitting` may not be called due to OS behavior.
If needed, also consider `OnApplicationFocus` / `OnApplicationPause` for lifecycle handling.

## FAQ ❓

### Q. Why not use `[RuntimeInitializeOnLoadMethod]` in the generic class?

`RuntimeInitializeOnLoadMethod` inside generic classes is known to not be invoked in some cases.
Initialization is centralized in `SingletonRuntime` (non-generic) instead (see Issue Tracker).

### Q. Can I call `Instance` every frame?

It works, but it's not recommended. Cache the reference in `Start` / `Awake` instead.

### Q. What happens if I define `Awake` in a derived class?

If you don't call `base.Awake()`, `_instance` assignment, reparenting to root, `DontDestroyOnLoad`, and `OnSingletonAwake` invocation will be skipped.
Always call `base.Awake()` or use `OnSingletonAwake()` instead.

### Q. What happens if I write `class A : PersistentSingletonBehaviour<B>`?

The CRTP constraint causes a compile error (CS0311). Always specify the class itself as the type parameter.
Additionally, runtime guards detect misuse early even if it somehow compiles.

### Q. Is it safe to call `Instance` in Edit Mode?

Yes. In Edit Mode, only search is performed with no static cache updates or auto-creation.
If a derived type is found, it is logged only and not destroyed, avoiding Undo system and Inspector side effects.

### Q. What happens if SceneSingletonBehaviour is not placed in the scene?

In DEV/EDITOR, an `InvalidOperationException` is thrown. In Player, `null` is returned.
Always place the singleton in the scene.

## References 📚

### Unity Scripting API / Manual

* Domain Reload disabled behavior (static field/event persistence)
  [https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html)
* Scene Reload disabled behavior (OnEnable/OnDisable/OnDestroy invocation)
  [https://docs.unity3d.com/6000.3/Documentation/Manual/scene-reloading.html](https://docs.unity3d.com/6000.3/Documentation/Manual/scene-reloading.html)
* RuntimeInitializeOnLoadMethodAttribute
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)
* RuntimeInitializeLoadType.SubsystemRegistration
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html)
* Time.frameCount
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-frameCount.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-frameCount.html)
* Application.isPlaying
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-isPlaying.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-isPlaying.html)
* Object.FindAnyObjectByType
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.FindAnyObjectByType.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.FindAnyObjectByType.html)
* Object.DontDestroyOnLoad
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.DontDestroyOnLoad.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.DontDestroyOnLoad.html)
* Application.quitting
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-quitting.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-quitting.html)
* DefaultExecutionOrder
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html)
* RuntimeInitializeOnLoadMethodAttribute not invoked if class is generic (1019360)
  [https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic](https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic)
