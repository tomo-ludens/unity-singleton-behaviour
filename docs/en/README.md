# Unity SingletonBehaviour

[Japanese](./README.md) | [English](../en/README.md)

A **type-per-singleton base class** for MonoBehaviour.

Intended for use with Unity 6.3 (6000.3 series) or later.

## Overview ✨

`SingletonBehaviour<T>` provides:

- 🧩 **Type-per-singleton guarantee** (e.g., `GameManager` and `AudioManager` are separate instances)
- 🛡️ **Type-safe inheritance** (CRTP-style constraint + runtime guard to catch misuse)
- 🕰️ **Lazy creation** (auto-creates when `Instance` is accessed and no instance exists)
- 🔁 **Scene persistence** (`DontDestroyOnLoad`)
- 🧯 **Shutdown safety** (prevents re-creation via `Application.quitting`)
- ⚙️ **Domain Reload disabled support** (invalidates per-type static cache using Play session identifier)
- 🧱 **Practical tolerance for misplacement** (reparents to root and persists even if placed as a child object)
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

- Add `SingletonBehaviour<T>` and `SingletonRuntime` to your project (e.g., `Assets/Foundation/Singletons/`).
- Adjust the namespace according to your project conventions.

### Namespace Import
```csharp
using Foundation.Singletons;
```

## Design Intent 🧠

### Why use the CRTP constraint?

`SingletonBehaviour<T>` has the following type constraint:
```csharp
public abstract class SingletonBehaviour<T> : MonoBehaviour
    where T : SingletonBehaviour<T>
```

This catches incorrect inheritance patterns at compile time:
```csharp
// ✅ Correct implementation
public sealed class GameManager : SingletonBehaviour<GameManager> { }

// ❌ Compile error (CS0311)
public sealed class A : SingletonBehaviour<B> { }
```

However, C# constraints alone cannot catch 100% of misuse cases (e.g., accidentally specifying a different type).
Therefore, a **runtime guard** (`this as T` validation) is also used to detect issues early in production.

### Why is `SingletonRuntime` necessary?

When Domain Reload is disabled, **static fields and static event handlers can persist across Play sessions**.

Given this "persistence," the per-type static cache must be invalidated at the start of each Play session.

To achieve this:

* `PlaySessionId` is updated in a non-generic location that reliably runs at Play start (`SubsystemRegistration`)
* `SingletonBehaviour<T>` references `PlaySessionId` to **invalidate its cache**

This separation of responsibilities is maintained.

> Note: Unity has known cases where `[RuntimeInitializeOnLoadMethod]` inside generic types is not invoked as expected.
> Centralizing initialization in a non-generic type is a practical workaround (see Issue Tracker).

### How Play Session Detection Works

* A non-generic `SingletonRuntime.SubsystemRegistration` (`RuntimeInitializeLoadType.SubsystemRegistration`) runs before the first scene load and increments `PlaySessionId`
* `Time.frameCount` guards against double-increment when the callback fires more than once in the same frame
* `SingletonBehaviour<T>` reads `PlaySessionId` to invalidate its static cache per Play session
* If initialization is delayed, `EnsureInitializedForCurrentPlaySession` re-hooks `Application.quitting` and lazily captures the main thread ID (only when `SynchronizationContext` is present) as a fallback

### DontDestroyOnLoad Call Management

While calling `DontDestroyOnLoad` multiple times on the same object is harmless,
this implementation uses the `_isPersistent` flag to limit the call to once, avoiding unnecessary processing.

## Dependencies 🔍

| API                                                          | Default Behavior                                                                                        |
| ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| `Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` | **Does not return assets / inactive objects / `HideFlags.DontSave`** (return value not guaranteed across calls) |
| `Object.DontDestroyOnLoad()`                                 | **Only works on root GameObjects (or components on root GameObjects)**                                  |
| `Application.quitting`                                       | **Invoked when exiting Play Mode in Editor**. May not be detected during pause on Android               |
| `RuntimeInitializeLoadType.SubsystemRegistration`            | **Called before the first scene is loaded** (execution order is undefined)                              |
| `Time.frameCount`                                            | **Resets to 0 when entering Play Mode**. Used to guard duplicate initialization                         |
| `Application.isPlaying`                                      | **`true` in Play Mode, `false` in Edit Mode**                                                           |
| Domain Reload disabled                                       | **Static field values / static event handlers persist across Play sessions**                            |
| Scene Reload disabled                                        | **`OnEnable` / `OnDisable` / `OnDestroy` etc. are called as if newly loaded**                           |

## Public API 📌

### `static T Instance { get; }`

For mandatory dependencies. Returns the singleton instance. If missing, **searches → auto-creates if not found**. Returns `null` while quitting or from non-main threads. In DEV/EDITOR, auto-create is blocked if an inactive instance exists (throws) and only the exact type `T` is accepted (derived types are rejected).
```csharp
GameManager.Instance.AddScore(10);
```

| State           | Result                            |
| --------------- | --------------------------------- |
| Instance exists | Cached instance                   |
| Missing         | Search → create if not found      |
| Quitting        | `null`                            |
| Edit Mode       | Search only (no create, no cache) |
| Inactive exists (DEV/EDITOR) | Throws to avoid hidden duplicate |
| Derived type found | Rejected (exact type only) |

### `static bool TryGetInstance(out T instance)`

For optional dependencies. Returns the instance if it exists. **Never creates one**. Returns `false` while quitting or from non-main threads. Only the exact type `T` is accepted; derived types are rejected.
```csharp
if (AudioManager.TryGetInstance(out var am))
{
    am.PlaySe("click");
}
```

| State           | Return        | `instance`             |
| --------------- | ------------- | ---------------------- |
| Instance exists | `true`        | Valid reference        |
| Missing         | `false`       | `null`                 |
| Quitting        | `false`       | `null`                 |
| Edit Mode       | Search result | Search only (no cache) |
| Derived type found | `false`    | `null` (rejected)      |

**Typical use case: prevent "accidental creation" during teardown 🧹**
```csharp
private void OnDisable()
{
    if (AudioManager.TryGetInstance(out var am))
    {
        am.Unregister(this);
    }
}
```

## Usage 🚀

### 1) Defining a derived singleton
```csharp
using Foundation.Singletons;

public sealed class GameManager : SingletonBehaviour<GameManager>
{
    public int Score { get; private set; }

    protected override void OnSingletonAwake()
    {
        // Initialization that should run every Play session
        this.Score = 0;
    }

    public void AddScore(int value) => this.Score += value;

    protected override void OnSingletonDestroy()
    {
        // Cleanup when actually destroyed (resource release, event unsubscription, etc.)
    }
}
```

| Item           | Recommendation                                           |
| -------------- | -------------------------------------------------------- |
| Class modifier | `sealed` (prevents unintended inheritance)               |
| Initialization | Put in `OnSingletonAwake()` (re-initialization per Play) |
| Cleanup        | Put in `OnSingletonDestroy()` (destruction only)         |

---

### 2) Choosing between `Instance` and `TryGetInstance`

* ✅ **Instance**: the dependency is required (create if missing)
  Example: managers essential to game flow such as `GameManager` / `InputManager`

* ✅ **TryGetInstance**: use only if present / do nothing if missing / avoid creation during shutdown or teardown
  Example: cleanup paths such as `OnDisable` / `OnDestroy` / `OnApplicationPause`, unregistering optional features

> In DEV/EDITOR, `Instance` will throw if an inactive instance exists to prevent hidden duplicates. Prefer `TryGetInstance` in teardown paths to avoid accidental creation.

---

### 3) Access pattern (cache it) 🧠

❌ Calling `Instance` every frame is not recommended. Because a Find operation may occur, the standard practice is to get it once, cache the reference, and reuse it.

✅ Recommended: acquire once and cache
```csharp
using Foundation.Singletons;
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

## Soft Reset (Safe Re-initialization Per Play Session) 🧼

This implementation is designed with "reusing the same instance while running initialization each Play session" in mind.

With Domain Reload disabled, static state and static event subscriptions can persist, so `OnSingletonAwake()` should be written to be **idempotent (safe to re-run)**.

> Practical tip: For static event subscriptions, use the "unsubscribe → subscribe" pattern to prevent duplicate subscriptions when Domain Reload is disabled.

## Constraints ⚠️

### ❌ Do not define `Awake()` / `OnEnable()` / `OnDestroy()` in derived classes

The base class Unity message methods are responsible for:

* Establishing `_instance` and rejecting duplicates
* Detecting Play session and invalidating static cache
* Reparenting to root (to satisfy `DontDestroyOnLoad` requirements)
* Applying `DontDestroyOnLoad`
* Controlling `OnSingletonAwake` / `OnSingletonDestroy` invocation (soft reset per Play session)

If a derived class defines `Awake()` / `OnEnable()` / `OnDestroy()`, **the base logic will be skipped and the singleton will break**.
Use `OnSingletonAwake()` for initialization and `OnSingletonDestroy()` for cleanup.

> Unity message methods are invoked by **name**, not via C# `virtual/override`, so this cannot be fully enforced by the language. Use team conventions and IDE inspections.

### ❌ Type parameter must be the class itself

The CRTP constraint causes the following incorrect inheritance to produce a compile error:
```csharp
// ❌ Compile error
public sealed class A : SingletonBehaviour<B> { }

// ✅ Correct implementation
public sealed class A : SingletonBehaviour<A> { }
```

## Scene Placement Notes 🧱

| Constraint                                         | Reason                                                   |
| -------------------------------------------------- | -------------------------------------------------------- |
| Do not place the same singleton in multiple scenes | The later-initialized one will be destroyed (first wins) |
| Root GameObject is preferable                      | `DontDestroyOnLoad` only works on root                   |

This implementation automatically reparents a child-placed singleton to root and persists it.
Since unintended moves can be confusing, emitting a warning only in **Editor/Development builds** is a reasonable approach (and matches this implementation).

## Edit Mode Behavior 🖥️

In Edit Mode (`Application.isPlaying == false`), the following behavior applies:

* `Instance` / `TryGetInstance` perform **search only** (`FindAnyObjectByType`)
* **No auto-creation**
* **No static cache updates** (zero side effects)
* **No impact on Play session state**

This allows safe singleton access from editor scripts and custom inspectors.

## Threading / Main Thread 🧵

`Instance` / `TryGetInstance` call UnityEngine APIs internally (Find / GameObject creation, etc.).
Call them from the **main thread**.

## Initialization Order ⏱️

If dependency order becomes complex, you can pin it with a Bootstrap pattern:
```csharp
using Foundation.Singletons;
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

### Android

`Application.quitting` may not be detected during pause.

If needed, also consider `OnApplicationFocus` / `OnApplicationPause` for lifecycle handling.

## FAQ ❓

### Q. Why not use `[RuntimeInitializeOnLoadMethod]` in the generic class?

`RuntimeInitializeOnLoadMethod` inside generic classes is known to not be invoked in some cases.
Initialization is centralized in `SingletonRuntime` (non-generic) instead (see Issue Tracker).

### Q. Can I call `Instance` every frame?

It works, but it's not recommended. Cache the reference in `Start` / `Awake` instead.

### Q. What happens if I define `Awake` in a derived class?

The base `Awake` won't be called, skipping `_instance` assignment, reparenting to root, `DontDestroyOnLoad`, and `OnSingletonAwake` invocation.
Remove `Awake` and use `OnSingletonAwake()` instead (same for `OnEnable` / `OnDestroy`).

### Q. What happens if I write `class A : SingletonBehaviour<B>`?

The CRTP constraint causes a compile error (CS0311). Always specify the class itself as the type parameter.
Additionally, runtime guards detect misuse early even if it somehow compiles.

### Q. Is it safe to call `Instance` in Edit Mode?

Yes. In Edit Mode, only search is performed with no static cache updates or auto-creation.

### Q. Why does it work even though `RuntimeInitializeOnLoadMethod` execution order is undefined?

A non-generic `SubsystemRegistration` runs before the first scene load and uses `Time.frameCount` to avoid double execution in the same frame. Additionally, `SingletonBehaviour<T>` calls `EnsureInitializedForCurrentPlaySession` as needed, re-hooking `Application.quitting` and capturing the main thread ID if initialization was delayed.

## References 📚

### Unity Scripting API / Manual

* Domain Reload disabled behavior (static field/event persistence)
  [https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html)
* Scene Reload disabled behavior (OnEnable/OnDisable/OnDestroy invocation)
  [https://docs.unity3d.com/6000.2/Documentation/Manual/scene-reloading.html](https://docs.unity3d.com/6000.2/Documentation/Manual/scene-reloading.html)
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
* MonoBehaviour.StopAllCoroutines
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.StopAllCoroutines.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.StopAllCoroutines.html)
* MonoBehaviour.CancelInvoke
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.CancelInvoke.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.CancelInvoke.html)
* SceneManager.sceneLoaded
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager-sceneLoaded.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager-sceneLoaded.html)
* DefaultExecutionOrder
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html)
* RuntimeInitializeOnLoadMethodAttribute not invoked if class is generic (1019360)
  [https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic](https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic)
