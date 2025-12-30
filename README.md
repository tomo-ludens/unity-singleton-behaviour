# Policy-Driven Unity Singleton (v3.0.0)

[Japanese README](./README.ja.md)

A **policy-driven singleton base class** for MonoBehaviour.

## Table of Contents

- [Requirements](#requirements)
- [Performance Considerations](#performance-considerations)
- [Overview](#overview)
  - [Provided Classes](#provided-classes)
  - [Key Features](#key-features)
- [Directory Structure](#directory-structure)
- [Dependencies](#dependencies-assumed-unity-api-behavior)
- [Installation](#installation)
- [Usage](#usage)
  - [GlobalSingleton](#1-globalsingleton)
  - [SceneSingleton](#2-scenesingleton)
  - [Choosing Between Instance and TryGetInstance](#3-choosing-between-instance-and-trygetinstance-typical-patterns)
  - [Caching is Recommended](#4-caching-is-recommended-important)
- [Public API Details](#public-api-details)
- [Design Intent](#design-intent-notes)
- [Constraints & Best Practices](#constraints--best-practices)
- [Advanced Topics](#advanced-topics)
- [Edit Mode Behavior](#edit-mode-behavior-details)
- [IDE Configuration](#ide-configuration-rider--resharper)
- [Testing](#testing)
- [Known Limitations](#known-limitations)
- [Troubleshooting](#troubleshooting)
- [References](#references)
- [License](#license)

## Requirements

* **Unity 2022.3** or later (tested with Unity 6.3)
* Supports both enabled and disabled **Reload Domain** in **Enter Play Mode Options**
* No external dependencies

## Performance Considerations

* **Policy resolution**: Zero allocation (readonly struct)
* **Instance access**: Minimal allocation only during auto-creation
* **Search operations**: Uses Unity's optimized FindAnyObjectByType
* **Caching**: Caching references is recommended for frequent access

## Overview

This library provides two singleton base classes for different use cases.

They share the same core logic, while a **policy** controls the lifecycle behavior (persistence across scenes and whether auto-creation is allowed).

### Provided Classes

| Class | Persist Across Scenes | Auto-Create | Intended Use |
| --- | --- | --- | --- |
| **`GlobalSingleton<T>`** | ✅ Yes | ✅ Yes | Managers that should always exist for the entire game (e.g., GameManager) |
| **`SceneSingleton<T>`** | ❌ No | ❌ No | Controllers that only operate within a specific scene (e.g., LevelController) |

### Key Features

* **Policy-driven**: Separates persistence (`DontDestroyOnLoad`) and auto-creation behavior via policies.
* **Domain Reload disabled support**: Reliably resets caches per Play session using a Play session ID, even when static fields survive between sessions.
* **Safe lifecycle**:
  * **Quitting**: Considers `Application.quitting` and prevents creation/access during shutdown.
  * **Edit Mode**: Performs *lookup only* in the editor, and does not create instances or mutate static caches (no side effects).
  * **Reinitialization (Soft Reset)**: Performs state reset at the **Play-session boundary** and reinitializes every Play session (aligned with the `PlaySessionId` strategy).
* **Strict type checks**: Rejects references where the generic type `T` does not exactly match the concrete runtime type, preventing misuse.
* **Development safety (DEV/EDITOR)**:
  * `FindAnyObjectByType(...Exclude)` does **not** consider inactive objects, so an inactive singleton can be treated as "missing" → auto-created → silently duplicated. To prevent this, DEV/EDITOR uses **fail-fast** (throws) when an inactive singleton is detected.
  * Accessing a SceneSingleton that was not placed in the scene also uses **fail-fast** (throws) in DEV/EDITOR.
* **Release build optimization**: Logs and validation checks are stripped; on error the API returns `null` / `false` (callers must handle this).

## Directory Structure

```text
Singletons/
├── Singletons.asmdef                 # Assembly Definition
├── AssemblyInfo.cs                   # InternalsVisibleTo for tests
├── GlobalSingleton.cs                # Public API (persistent + auto-create)
├── SceneSingleton.cs                 # Public API (scene-scoped + no auto-create)
├── Core/
│   ├── SingletonBehaviour.cs         # Core implementation
│   ├── SingletonRuntime.cs           # Internal runtime (Domain Reload handling)
│   └── SingletonLogger.cs            # Conditional logger (stripped in release)
├── Policy/
│   ├── ISingletonPolicy.cs           # Policy interface
│   ├── PersistentPolicy.cs           # Persistent policy implementation
│   └── SceneScopedPolicy.cs          # Scene-scoped policy implementation
└── Tests/                            # PlayMode & EditMode tests
    ├── Runtime/
    │   ├── Singletons.Tests.asmdef
    │   └── SingletonTests.cs
    └── Editor/
        ├── Singletons.Editor.Tests.asmdef
        └── EditModeTests.cs
```

## Dependencies (Assumed Unity API Behavior)

This implementation assumes the following Unity behaviors. If Unity changes these behaviors, the design assumptions may need revisiting.

| API / Feature | Assumed Behavior |
| --- | --- |
| Domain Reload disabled | **Static variables** and **static event subscriptions** persist across Play sessions. The library invalidates caches using `PlaySessionId`. |
| Scene Reload disabled | When Scene Reload is disabled, scenes are not reloaded. The library **does not** assume the same callback order as a fresh app launch (new-load semantics). State reset is aligned to the Play-session boundary via `PlaySessionId`. |
| `Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` | By default, **inactive objects are excluded**. Therefore an inactive singleton can be treated as "not found," which drives the DEV/EDITOR fail-fast policy. |
| `Object.DontDestroyOnLoad` | Must be applied to a **root GameObject** (therefore Persistent singletons may reparent to root when needed). |

## Installation

1. Place the `Singletons` folder anywhere in your project (e.g., `Assets/Plugins/Singletons/`).
2. Adjust namespaces and assembly definitions as needed.

## Usage

### 1. GlobalSingleton

Persists across scenes, and auto-creates when accessed if not found.

```csharp
using Singletons;

// Sealing is recommended to prevent accidental inheritance.
public sealed class GameManager : GlobalSingleton<GameManager>
{
    public int Score { get; private set; }
    public int CurrentLevel { get; private set; }

    protected override void Awake()
    {
        base.Awake(); // Required - initializes singleton
        Score = 0;
        CurrentLevel = 1;
    }

    // Per-play-session reinitialization (especially with Domain Reload disabled)
    protected override void OnPlaySessionStart()
    {
        // Called at the start of each play session (Play Mode start or restart with Domain Reload disabled)
        // Awake is called only on first run, but OnPlaySessionStart is called every play session
        Debug.Log($"New play session started. Current level: {CurrentLevel}");
        
        // Reset session-specific state
        // Example: temporary data, event subscriptions, caches, etc.
        ResetTemporaryData();
        RebindEvents();
    }

    private void ResetTemporaryData()
    {
        // Clear temporary data that shouldn't persist between play sessions
        // Example: UI state, unsaved work-in-progress data, etc.
    }

    private void RebindEvents()
    {
        // Re-subscribe to events (in case subscriptions are lost with Domain Reload disabled)
        // Example: GameManager.OnGameStateChanged += HandleGameStateChanged;
    }

    public void AddScore(int value) => Score += value;
    public void NextLevel() => CurrentLevel++;
}

// Example:
// GameManager.Instance.AddScore(10);
// GameManager.Instance.NextLevel();
```

#### Importance of OnPlaySessionStart

`OnPlaySessionStart` is especially important when **Domain Reload is disabled**:

| Method | Called When | Purpose |
|--------|-------------|---------|
| `Awake()` | Only on first Play Mode start | Persistent initialization (resource loading, static settings) |
| `OnPlaySessionStart()` | **Every play session** | Session-specific initialization (temporary data, event subscriptions) |

**Why is it needed?**
- When Domain Reload is disabled, static fields persist between play sessions
- Event subscriptions and temporary data might remain from the previous session
- `OnPlaySessionStart` ensures a clean state for each session

### 2. SceneSingleton

Must be placed in the scene. No auto-creation. Destroyed when the scene unloads.

```csharp
using Singletons;

public sealed class LevelController : SceneSingleton<LevelController>
{
    protected override void Awake()
    {
        base.Awake(); // Required - initializes singleton
        // Per-scene initialization
    }
}

// ⚠️ Must be placed in the scene.
// If you forget to place it: DEV/EDITOR throws; release builds return null.
// LevelController.Instance.DoSomething();
```

### 3. Choosing Between `Instance` and `TryGetInstance` (Typical Patterns)

In release builds, DEV/EDITOR-only validations are stripped and the API returns `null` / `false` on errors. The key for effective usage is making clear **when to use `Instance` and when to use `TryGetInstance`**.

| Choice | Rule of Thumb | Examples |
| --- | --- | --- |
| **`Instance`** | Use when the feature is **required**, and a missing instance is not acceptable. | Essential managers during **startup/initialization** (`GameManager`, `AudioManager`, etc.) |
| **`TryGetInstance`** | Use when "if it exists, use it; otherwise do nothing" is correct.<br>Avoid unintended creation/resurrection and ordering coupling. | **Cleanup / unregister / pause** flows (`OnDisable` / `OnDestroy` / `OnApplicationPause`, etc.) |

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

#### Typical: Use Instance at startup to reliably establish (with caching)

```csharp
private GameManager _gm;

private void Awake()
{
    _gm = GameManager.Instance; // required → Instance
}

private void Update()
{
    if (_gm == null) return; // defensive guard since release builds may return null
    // ...
}
```

### 4. Caching is Recommended (Important)

`Instance` performs internal lookup and validation, so **avoid calling it every frame (e.g., inside `Update`)**. Fetch once in `Start` / `Awake`, cache the reference, and reuse it.

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

| State | Behavior |
| --- | --- |
| **Playing (normal)** | Returns the cached instance if established. Otherwise searches, and Persistent may auto-create if needed. |
| **During quitting** | Always returns `null`. |
| **Edit Mode** | Lookup only (no creation, and no static cache mutation). |
| **Inactive detected** | Throws in DEV/EDITOR; returns `null` in Player builds. |
| **Type mismatch** | Rejects and returns `null` for references with mismatched types such as derived types (destroys it during Play Mode). |
| **Scene missing** | If a SceneSingleton is not found: throws in DEV/EDITOR; returns `null` in Player builds. |

### `static bool TryGetInstance(out T instance)`

Returns the instance if present. **Does not auto-create**.

| State | Behavior |
| --- | --- |
| **Present** | Returns `true` and a valid reference. |
| **Not present** | Returns `false` and `null`. |
| **During quitting** | Always returns `false`. |
| **Edit Mode** | Lookup only (does not cache). |

## Design Intent (Notes)

### Why split behavior via policies?

To separate "behavior" such as persistence and auto-creation into policies (`ISingletonPolicy`) while keeping the core logic shared.

### Why is `SingletonRuntime` required?

With Domain Reload disabled, static fields and static event subscriptions can persist across Play sessions. Therefore the library must invalidate per-type static caches at each Play start.

1. Update `PlaySessionId` at Play start from a reliably invoked point (`SubsystemRegistration`).
2. The singleton side checks `PlaySessionId` and invalidates stale caches, forcing re-lookup.

### Why centralize initialization in `SingletonRuntime`?

With Domain Reload disabled, there is no guarantee that static state resets to defaults at each Play. Unity's documentation explicitly states that static variables and static event subscriptions can persist when Domain Reload is disabled.

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

Even if you forget, there is a safety net that initializes on the first `Instance` / `TryGetInstance` access. However, that obscures ordering and is not recommended. Always call `base.Awake()` at the beginning of your overridden `Awake()` method.

### 3. Placement guidelines

* **Do not place duplicates**: Do not place the same singleton in multiple scenes (the later-loaded one will be destroyed).
* **Persistent expects root placement**: If attached under a child, it will reparent to root and persist; DEV/EDITOR emits a warning.
* **Do not keep it disabled**: Avoid leaving singleton components Disabled; they can be treated as "missing" and lead to hidden duplication.

## Advanced Topics

### Soft Reset (per-Play reinitialization)

With Domain Reload disabled, static state can persist. This library invalidates caches at the Play-session boundary (`PlaySessionId`) and reinitializes every Play session to reset state.

Because Unity calls `Awake()` only once per GameObject lifetime, do **per-Play reinitialization** by overriding `OnPlaySessionStart()` (called once per Play session when the singleton is established).

Write your `OnPlaySessionStart()` logic to be **idempotent** (e.g., "unsubscribe → subscribe" for event hookups).

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

> Note: `FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` excludes inactive objects by default. Because an inactive singleton can be treated as "not found," DEV/EDITOR chooses fail-fast.

## IDE Configuration (Rider / ReSharper)

### `StaticMemberInGenericType` warning

`static` fields in `SingletonBehaviour<T, TPolicy>` (such as `_instance`) are isolated per generic instantiation.

This is **intended behavior** for this singleton design, so align your team on one approach:

* Use suppression comments in code, or
* Adjust severity via `.DotSettings`, etc.

## Testing

### Included Tests

This package includes comprehensive PlayMode and EditMode tests with **53 total tests** (41 PlayMode + 12 EditMode), all passing.

#### PlayMode Tests (41 tests)

| Category | Tests | Coverage |
|----------|-------|----------|
| GlobalSingleton | 7 | Auto-creation, caching, duplicates |
| SceneSingleton | 5 | Placement, no auto-create, duplicates |
| InactiveInstance | 3 | Inactive GO detection, disabled component |
| TypeMismatch | 2 | Derived class rejection |
| ThreadSafety | 7 | Background thread protection, main thread validation |
| Lifecycle | 2 | Destruction, recreation |
| SoftReset | 1 | Per-Play reinitialization on PlaySessionId boundary |
| SceneSingletonEdgeCase | 2 | Not placed, no auto-create |
| PracticalUsage | 6 | GameManager, LevelController, state management |
| PolicyBehavior | 3 | Policy-driven behavior validation |
| ResourceManagement | 3 | Instance lifecycle and cleanup |

#### EditMode Tests (12 tests)

| Category | Tests | Coverage |
|----------|-------|----------|
| SingletonRuntimeEditMode | 2 | PlaySessionId, IsQuitting validation |
| Policy | 5 | Policy struct validation, immutability, interface compliance |
| SingletonBehaviourEditMode | 5 | EditMode behavior, caching isolation |

### Running Tests

1. Open **Window → General → Test Runner**
2. Select **PlayMode** or **EditMode** tab
3. Click **Run All**

### Writing Your Own Tests

Test-only APIs are available via `TestExtensions`:

```csharp
// Reset static instance cache (uses reflection)
default(MyManager).ResetStaticCacheForTesting();
```

**Example Test:**

```csharp
[UnityTest]
public IEnumerator MyManager_AutoCreates()
{
    var instance = MyManager.Instance;
    yield return null;

    Assert.IsNotNull(instance);
}

[TearDown]
public void TearDown()
{
    if (MyManager.TryGetInstance(out var instance))
    {
        Object.DestroyImmediate(instance.gameObject);
    }
    default(MyManager).ResetStaticCacheForTesting();
}
```

### PlayMode Test Considerations

* `RuntimeInitializeOnLoadMethod` runs in PlayMode tests.
* `PlaySessionId` advances between tests, providing static cache isolation.
* Always clean up in `TearDown` to avoid test pollution.

## Known Limitations

### Static Constructor Timing
If a singleton class has a static constructor, it may execute before `PlaySessionId` is initialized. This can rarely cause unexpected behavior.

### Thread Safety
All singleton operations must be called from the main thread. Access from background threads throws `UnityException` (because `Application.isPlaying` is a main-thread-only API).

### Scene Loading Order
If multiple scenes contain the same singleton type, the destruction order depends on Unity's scene loading sequence.

### Memory Leaks
If static event subscriptions are not properly cleaned up in `OnDestroy`, memory leaks can occur when Domain Reload is disabled.

## Troubleshooting

### FAQ

**Q. Singleton returns null in Play Mode**
Check that the component is active and enabled, and that you're calling from the main thread. If you override Awake, verify that `base.Awake()` is called.

**Q. Getting duplicate singleton warnings**
The same singleton may be placed in multiple scenes. Check scenes and prefabs, and remove duplicate instances.

**Q. Exceptions only occur in Editor**
This is due to DEV/EDITOR fail-fast behavior. Verify that SceneSingleton is placed in the scene. Use `TryGetInstance()` for conditional access.

**Q. Can I call `Instance` every frame?**
It works, but it is not recommended. Cache it in `Start` / `Awake`.

**Q. What happens if I override `Awake` and forget `base.Awake()`?**
Initialization is deferred and occurs on the first `Instance` / `TryGetInstance` access. It still runs, but the timing becomes unexpectedly late, so always call the base method.

**Q. What happens if I forget to place a SceneSingleton in the scene?**
DEV/EDITOR throws an exception; Player builds return `null` / `false`. GlobalSingleton auto-creates if not found.

### Debugging Tips

```csharp
// Enable detailed logging (DEV/EDITOR only)
#define DEVELOPMENT_BUILD
#define UNITY_EDITOR

// Check singleton state
if (MySingleton.TryGetInstance(out var instance))
{
    Debug.Log($"Singleton found: {instance.name}");
}
else
{
    Debug.LogWarning("Singleton not available");
}
```

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
