# Policy-Driven Unity Singleton

[Japanese README](./README.ja.md)

A **policy-driven singleton base class** for MonoBehaviour.

This library targets Unity 6.3 (6000.3 series) and is designed to remain robust even when **Reload Domain is disabled** in Enter Play Mode Options.

## Requirements

* Unity **6.3** (6000.3.x) or newer
* Full support for environments where **Reload Domain** is disabled in **Enter Play Mode Options**
* (Optional) Assumes a workflow where even with **Reload Scene** disabled, initialization is performed per Play session

## Overview

This library provides two singleton base classes for different use cases.

They share the same core logic, while a **policy** controls the lifecycle behavior (persistence across scenes and whether auto-creation is allowed).

### Provided Classes

| Class | Persist Across Scenes | Auto-Create | Intended Use |
| --- | --- | --- | --- |
| **`PersistentSingletonBehaviour<T>`** | ✅ Yes | ✅ Yes | Managers that should always exist for the entire game (e.g., GameManager) |
| **`SceneSingletonBehaviour<T>`** | ❌ No | ❌ No | Controllers that only operate within a specific scene (e.g., LevelController) |

### Key Features

* **Policy-driven**: Separates persistence (`DontDestroyOnLoad`) and auto-creation behavior via policies.
* **Domain Reload disabled support**: Reliably resets caches per Play session using a Play session ID, even when static fields survive between sessions.
* **Safe lifecycle**:
  * **Quitting**: Considers `Application.quitting` and prevents creation/access during shutdown.
  * **Edit Mode**: Performs *lookup only* in the editor, and does not create instances or mutate static caches (no side effects).
  * **Reinitialization (Soft Reset)**: Performs state reset at the **Play-session boundary** and runs `OnSingletonAwake()` every Play session (aligned with the `PlaySessionId` strategy).
* **Strict type checks**: Rejects references where the generic type `T` does not exactly match the concrete runtime type, preventing misuse.
* **Development safety (DEV/EDITOR)**:
  * `FindAnyObjectByType(...Exclude)` does **not** consider inactive objects, so an inactive singleton can be treated as “missing” → auto-created → silently duplicated. To prevent this, DEV/EDITOR uses **fail-fast** (throws) when an inactive singleton is detected.
  * Accessing a SceneSingleton that was not placed in the scene also uses **fail-fast** (throws) in DEV/EDITOR.
* **Release build optimization**: Logs and validation checks are stripped; on error the API returns `null` / `false` (callers must handle this).

## Directory Structure

```text
Singletons/
├── PersistentSingletonBehaviour.cs   # Public API (persistent + auto-create)
├── SceneSingletonBehaviour.cs        # Public API (scene-scoped + no auto-create)
├── Core/
│   ├── SingletonBehaviour.cs         # Core implementation
│   ├── SingletonRuntime.cs           # Internal runtime (Domain Reload handling)
│   └── SingletonLogger.cs            # Conditional logger (stripped in release)
└── Policy/
    ├── ISingletonPolicy.cs           # Policy interface
    ├── PersistentPolicy.cs           # Persistent policy implementation
    └── SceneScopedPolicy.cs          # Scene-scoped policy implementation
````

## Dependencies (Assumed Unity API Behavior)

This implementation assumes the following Unity behaviors. If Unity changes these behaviors, the design assumptions may need revisiting.

| API / Feature                                                | Assumed Behavior                                                                                                                                                                                                                     |
| ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Domain Reload disabled                                       | **Static variables** and **static event subscriptions** persist across Play sessions. The library invalidates caches using `PlaySessionId`.                                                                                          |
| Scene Reload disabled                                        | When Scene Reload is disabled, scenes are not reloaded. The library **does not** assume the same callback order as a fresh app launch (new-load semantics). State reset is aligned to the Play-session boundary via `PlaySessionId`. |
| `Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` | By default, **inactive objects are excluded**. Therefore an inactive singleton can be treated as “not found,” which drives the DEV/EDITOR fail-fast policy.                                                                          |
| `Object.DontDestroyOnLoad`                                   | Must be applied to a **root GameObject** (therefore Persistent singletons may reparent to root when needed).                                                                                                                         |

## Installation

1. Place the `Singletons` folder anywhere in your project (e.g., `Assets/Plugins/Singletons/`).
2. Adjust namespaces and assembly definitions as needed.

## Usage

### 1. Persistent Singleton

Persists across scenes, and auto-creates when accessed if not found.

```csharp
using Singletons;

// Sealing is recommended to prevent accidental inheritance.
public sealed class GameManager : PersistentSingletonBehaviour<GameManager>
{
    public int Score { get; private set; }

    // Use OnSingletonAwake instead of Awake.
    protected override void OnSingletonAwake()
    {
        // Initialization that runs once per Play session
        Score = 0;
    }

    protected override void OnSingletonDestroy()
    {
        // Called only when the actual instance is destroyed
    }

    public void AddScore(int value) => Score += value;
}

// Example:
// GameManager.Instance.AddScore(10);
```

### 2. Scene-scoped Singleton

Must be placed in the scene. No auto-creation. Destroyed when the scene unloads.

```csharp
using Singletons;

public sealed class LevelController : SceneSingletonBehaviour<LevelController>
{
    protected override void OnSingletonAwake()
    {
        // Per-scene initialization
    }
}

// ⚠️ Must be placed in the scene.
// If you forget to place it: DEV/EDITOR throws; release builds return null.
// LevelController.Instance.DoSomething();
```

### 3. Choosing Between `Instance` and `TryGetInstance` (Typical Patterns)

In release builds, DEV/EDITOR-only validations are stripped and the API returns `null` / `false` on errors. The most effective guidance for users is making the **typical usage split** explicit.

| Choice               | Rule of Thumb                                                                                                                   | Examples                                                                                        |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| **`Instance`**       | Use when the feature is **required**, and a missing instance is not acceptable.                                                 | Essential managers during **startup/initialization** (`GameManager`, `AudioManager`, etc.)      |
| **`TryGetInstance`** | Use when “if it exists, use it; otherwise do nothing” is correct. Avoid unintended creation/resurrection and ordering coupling. | **Cleanup / unregister / pause** flows (`OnDisable` / `OnDestroy` / `OnApplicationPause`, etc.) |

#### Typical: Use TryGetInstance for cleanup/unregister paths

```csharp
private void OnDisable()
{
    if (AudioManager.TryGetInstance(out var am))
    {
        am.Unregister(this);
    }
}

private void OnDestroy()
{
    if (GameManager.TryGetInstance(out var gm))
    {
        gm.Unregister(this);
    }
}

private void OnApplicationPause(bool paused)
{
    if (paused && Telemetry.TryGetInstance(out var t))
    {
        t.Flush();
    }
}
```

#### Typical: Use Instance at startup, then cache

```csharp
private GameManager _gm;

private void Awake()
{
    _gm = GameManager.Instance; // required → Instance
}

private void Update()
{
    if (_gm == null) return; // defensive guard for release behavior
    // ...
}
```

### 4. Caching is recommended (Important)

`Instance` performs internal lookup and validation, so avoid calling it every frame (e.g., inside `Update`). Fetch once in `Start` / `Awake`, cache the reference, and reuse it.

```csharp
public class Player : MonoBehaviour
{
    private GameManager _gameManager;

    private void Start()
    {
        _gameManager = GameManager.Instance; // cache here
    }

    private void Update()
    {
        if (_gameManager == null) return;
        // _gameManager.DoSomething();
    }
}
```

## Public API Details

### `static T Instance { get; }`

| State                 | Behavior                                                                                                  |
| --------------------- | --------------------------------------------------------------------------------------------------------- |
| **Playing (normal)**  | Returns the cached instance if established. Otherwise searches, and Persistent may auto-create if needed. |
| **During quitting**   | Always returns `null`.                                                                                    |
| **Edit Mode**         | Lookup only (no creation, and no static cache mutation).                                                  |
| **Inactive detected** | Throws in DEV/EDITOR; returns `null` in Player builds.                                                    |
| **Type mismatch**     | Rejects and returns `null` (and destroys it in Play Mode).                                                |
| **Scene missing**     | If a SceneSingleton is not found: throws in DEV/EDITOR; returns `null` in Player builds.                  |

### `static bool TryGetInstance(out T instance)`

Returns the instance if present. **Does not auto-create**.

| State               | Behavior                              |
| ------------------- | ------------------------------------- |
| **Present**         | Returns `true` and a valid reference. |
| **Not present**     | Returns `false` and `null`.           |
| **During quitting** | Always returns `false`.               |
| **Edit Mode**       | Lookup only (does not cache).         |

## Design Intent (Notes)

### Why split behavior via policies?

To separate “behavior” such as persistence and auto-creation into policies (`ISingletonPolicy`) while keeping the core logic shared.

### Why is `SingletonRuntime` required?

With Domain Reload disabled, static fields and static event subscriptions can persist across Play sessions. Therefore the library must invalidate per-type static caches at each Play start.

1. Update `PlaySessionId` at Play start from a reliably invoked point (`SubsystemRegistration`).
2. The singleton side checks `PlaySessionId` and invalidates stale caches, forcing re-lookup.

### Why centralize initialization in `SingletonRuntime`?

With Domain Reload disabled, there is no guarantee that static state resets to defaults at each Play. Unity’s documentation explicitly states that static variables and static event subscriptions can persist when Domain Reload is disabled.

Therefore, this library updates `PlaySessionId` at each Play start and makes `SingletonBehaviour` invalidate stale static caches reliably.

Additionally, Unity has a known issue where `RuntimeInitializeOnLoadMethod` on a **generic** class may not be invoked as expected (Issue Tracker). For this reason, initialization is centralized in the non-generic `SingletonRuntime`.

## Constraints & Best Practices

### 1. Seal concrete classes

Further inheriting from a concrete singleton (e.g., `GameManager`) is not recommended.
Inheritance like `class Derived : GameManager` is rejected at runtime by the type-check mechanism.

### 2. If you override Unity messages, base calls are required

If you override `Awake`, `OnEnable`, or `OnDestroy`, you must call the base method.

```csharp
protected override void Awake()
{
    base.Awake(); // required
    // additional initialization
}
```

Even if you forget, there is a safety net that initializes on the first `Instance` / `TryGetInstance` access. However, that obscures ordering and is not recommended. Prefer `OnSingletonAwake` / `OnSingletonDestroy`.

### 3. Placement guidelines

* **Do not place duplicates**: Do not place the same singleton in multiple scenes (the later-loaded one will be destroyed).
* **Persistent expects root placement**: If attached under a child, it will reparent to root and persist; DEV/EDITOR emits a warning.
* **Do not keep it disabled**: Avoid leaving singleton components Disabled; they can be treated as “missing” and lead to hidden duplication.

## Advanced Topics

### Soft Reset (per-Play reinitialization)

With Domain Reload disabled, static state can persist. This library invalidates caches at the Play-session boundary (`PlaySessionId`) and runs `OnSingletonAwake()` every Play session to reset state.

Write `OnSingletonAwake()` to be **idempotent** (e.g., “unsubscribe → subscribe” for event hookups).

### Threading / Main Thread

`Instance` / `TryGetInstance` call UnityEngine APIs (Find / GameObject creation). Therefore, during Play Mode they must be called from the **main thread**.

### Initialization Order

If you need strict initialization order, fix it via a Bootstrap class with `DefaultExecutionOrder`.

```csharp
[DefaultExecutionOrder(-10000)]
public class Bootstrap : MonoBehaviour
{
    void Awake()
    {
        _ = GameManager.Instance;
        _ = AudioManager.Instance;
        _ = InputManager.Instance;
    }
}
```

## Edit Mode Behavior (Details)

In Edit Mode (`Application.isPlaying == false`), behavior is fixed:

* `Instance` / `TryGetInstance` perform **lookup only** (no auto-creation).
* **Static caches are not updated** (no side effects).
* Therefore, references from custom inspectors or editor tooling do not affect Play Mode state.

> Note: `FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` excludes inactive objects by default. Because an inactive singleton can be treated as “not found,” DEV/EDITOR chooses fail-fast.

## IDE Configuration (Rider / ReSharper)

### `StaticMemberInGenericType` warning

`static` fields in `SingletonBehaviour<T, TPolicy>` (such as `_instance`) are isolated per generic instantiation. This is **intended** for this singleton design. Align your team on one approach:

* Use suppression comments in code, or
* Adjust severity via `.DotSettings`, etc.

## Testing (PlayMode tests)

`RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)` also runs in PlayMode tests.

* Because `PlaySessionId` advances between tests, static caches are less likely to break tests.
* If your tests keep static subscriptions or caches, ensure teardown (`TearDown`) unsubscribes and resets them.

## FAQ

**Q. Can I call `Instance` every frame?**
It works, but it is not recommended. Cache it in `Start` / `Awake`.

**Q. What happens if I override `Awake` and forget `base.Awake()`?**
Establishment is deferred and initialization occurs on the first `Instance` / `TryGetInstance` access. It still runs, but the initialization timing becomes unexpectedly late, so always call the base method.

**Q. What happens if I forget to place it in the scene?**
SceneSingleton throws in DEV/EDITOR; returns `null` / `false` in Player builds. PersistentSingleton auto-creates if not found.

## References

Domain Reload (Manual)
[https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html)

Scene Reload (Manual)
[https://docs.unity3d.com/6000.2/Documentation/Manual/scene-reloading.html](https://docs.unity3d.com/6000.2/Documentation/Manual/scene-reloading.html)

RuntimeInitializeOnLoadMethodAttribute (Scripting API)
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)

RuntimeInitializeLoadType.SubsystemRegistration (Scripting API)
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html)

Object.DontDestroyOnLoad (Scripting API)
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.DontDestroyOnLoad.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.DontDestroyOnLoad.html)

Application.quitting (Scripting API)
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-quitting.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-quitting.html)

DefaultExecutionOrder (Scripting API)
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html)

Unity Issue Tracker: RuntimeInitializeOnLoadMethodAttribute not invoked if class is generic
[https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic](https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic)

## License

See [LICENSE](./LICENSE).
