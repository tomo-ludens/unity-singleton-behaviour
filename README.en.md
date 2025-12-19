# Unity SingletonBehaviour

[Japanese](./README.md) | [English](./README.en.md)

A singleton base class for MonoBehaviour. Intended for use with Unity 6.3 (6000.3 series) or later.

## Overview ✨

`SingletonBehaviour<T>` provides:

- 🧩 **Type-per-singleton guarantee** (e.g., `GameManager` and `AudioManager` are separate instances)
- 🕰️ **Lazy creation** (auto-creates when `Instance` is accessed and no instance exists)
- 🔁 **Scene persistence** (`DontDestroyOnLoad`)
- 🧯 **Shutdown safety** (prevents re-creation via `Application.quitting`)
- ⚙️ **Domain Reload disabled support** (invalidates cached statics using a Play session identifier)
- 🧱 **Practical tolerance for misplacement** (reparents to root and persists even if placed as a child object)

## Requirements ✅

- Unity 6.3 (6000.3.x) or later
- Designed to remain stable even when Domain Reload is disabled via Enter Play Mode Options

## Design Intent 🧠

### Why is `SingletonRuntime` necessary?

`SingletonBehaviour<T>` is a **generic type**. Unity has a limitation where placing `[RuntimeInitializeOnLoadMethod]` inside generic types is not allowed (or is not invoked as expected), and this is treated as a known limitation/issue in Unity.

To centralize initialization in a place that can reliably run at startup, this design introduces `SingletonRuntime` (a non-generic type):

- `SingletonRuntime.Initialize()` runs at `SubsystemRegistration`
- It updates `PlaySessionId`, and `SingletonBehaviour<T>` uses it to **invalidate the per-type static cache**

This separation preserves SRP while providing a practical mitigation for “static state persisting across Play sessions” when Domain Reload is disabled.

> Terminology note: this README uses **PlaySessionId** consistently (no “Epoch”) to match the code.

## Dependencies 🔍

| API | Default behavior |
|-----|------------------|
| `Object.FindAnyObjectByType<T>()` | Does not return assets / inactive objects / objects with `HideFlags.DontSave` |
| `Object.DontDestroyOnLoad()` | Only works on root GameObjects (or components on root GameObjects) |
| `Application.quitting` | Invoked when exiting Play Mode in the Editor; on Android it may not be detected during pause |
| `RuntimeInitializeLoadType.SubsystemRegistration` | Called before the first scene is loaded |

## Public API 📌

### `static T Instance { get; }`

For mandatory dependencies. Returns the singleton instance and **auto-creates** one if missing. Returns `null` while quitting.

```csharp
GameManager.Instance.AddScore(10);
````

| State           | Result                     |
| --------------- | -------------------------- |
| Instance exists | Cached instance            |
| Missing         | Find → create if not found |
| Quitting        | `null`                     |

---

### `static bool TryGetInstance(out T instance)`

For optional dependencies. Returns the instance if it exists, and **never creates** one.

```csharp
if (AudioManager.TryGetInstance(out var am))
{
    am.PlaySe("click");
}
```

| State           | Return  | `instance`      |
| --------------- | ------- | --------------- |
| Instance exists | `true`  | Valid reference |
| Missing         | `false` | `null`          |
| Quitting        | `false` | `null`          |

**Typical use case: prevent “accidental creation” during teardown 🧹**

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
public sealed class GameManager : SingletonBehaviour<GameManager>
{
    public int Score { get; private set; }

    protected override void OnSingletonAwake()
    {
        Score = 0;
    }

    public void AddScore(int value) => Score += value;

    protected override void OnSingletonDestroy()
    {
        // Resource cleanup, event unsubscription, etc.
    }
}
```

| Item           | Recommendation                             |
| -------------- | ------------------------------------------ |
| Class modifier | `sealed` (prevents unintended inheritance) |
| Initialization | Put in `OnSingletonAwake()`                |
| Cleanup        | Put in `OnSingletonDestroy()`              |

---

### 2) Choosing between `Instance` and `TryGetInstance`

* ✅ **Instance**: the dependency is required (create if missing)
  Example: managers essential to game flow such as `GameManager` / `InputManager`

* ✅ **TryGetInstance**: use only if present / do nothing if missing / avoid creation during shutdown or teardown
  Example: cleanup paths such as `OnDisable` / `OnDestroy` / `OnApplicationPause`, unregistering optional features

---

### 3) Access pattern (cache it) 🧠

❌ Calling `Instance` every frame is not recommended.
Because a Find operation may occur, the standard practice is to get it once, cache the reference, and reuse it.

✅ Recommended: acquire once and cache

```csharp
public sealed class ScoreHUD : MonoBehaviour
{
    private GameManager _gm;

    private void Start()
    {
        _gm = GameManager.Instance; // cache
    }

    private void Update()
    {
        if (_gm == null) return;
        // use _gm.Score ...
    }
}
```

## Constraints ⚠️

### ❌ Do not define `Awake()` / `OnDestroy()` in derived classes

The base `Awake` / `OnDestroy` are responsible for:

* Establishing `_instance` and rejecting duplicates
* Reparenting to root (to satisfy `DontDestroyOnLoad` requirements)
* Applying `DontDestroyOnLoad`
* Invoking `OnSingletonAwake` / `OnSingletonDestroy`

If a derived class defines `Awake()` / `OnDestroy()`, the base logic may be skipped and the singleton can break.

💡 Unity message methods are invoked by **name**, not via C# `virtual/override`, so this cannot be fully enforced by the language. Use team conventions and IDE inspections.

## Scene Placement Notes 🧱

| Constraint                                         | Reason                                 |
| -------------------------------------------------- | -------------------------------------- |
| Do not place the same singleton in multiple scenes | The later-loaded one will be destroyed |
| Root GameObject is preferable                      | `DontDestroyOnLoad` requirement        |

This implementation automatically reparents a child-placed singleton to root and persists it.

Since unintended moves can be confusing, emitting a warning only in **Editor/Development builds** is a reasonable approach (and matches this implementation).

## Threading / Main Thread 🧵

`Instance` / `TryGetInstance` call UnityEngine APIs internally (Find / GameObject creation, etc.).
Call them from the **main thread**.

## Initialization Order ⏱️

If dependency order becomes complex, you can pin it with a Bootstrap pattern:

```csharp
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
This warns that “static fields are separated per closed generic type,” which is **intentional** for type-per-singleton designs.

Depending on team policy, you can prefer adjusting inspection severity via shared `.DotSettings` rather than scattering suppression comments.

## Testing (PlayMode Tests) 🧪

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` also runs when starting PlayMode tests.

* ✅ `PlaySessionId` is updated between test runs, so cached statics are invalidated in the normal case
* ⚠️ If you need test-specific initialization, add a guard or dedicated setup for tests

## Platform Notes 📱

### Android

`Application.quitting` may not be detected during pause.
If needed, also consider `OnApplicationFocus` / `OnApplicationPause` for lifecycle handling.

## FAQ ❓

### Q. Why not use `[RuntimeInitializeOnLoadMethod]` in the generic class?

Due to a Unity limitation, it won't be invoked inside generic classes.
Instead, `SingletonRuntime` (non-generic) updates the `PlaySessionId`, and each `SingletonBehaviour<T>` checks it to invalidate its cached static.

### Q. Can I call `Instance` every frame?

It works, but it's not recommended. Cache the reference in `Start` / `Awake` instead.

### Q. What happens if I define `Awake` in a derived class?

The base `Awake` won't be called, skipping `_instance` assignment, reparenting to root, `DontDestroyOnLoad`, and the `OnSingletonAwake` hook. Remove `Awake` and use `OnSingletonAwake()` instead.

## References 📚

* RuntimeInitializeOnLoadMethodAttribute: [https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html](https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)
* RuntimeInitializeLoadType.SubsystemRegistration (Unity 6.3): [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html)
* Object.FindAnyObjectByType: [https://docs.unity3d.com/ScriptReference/Object.FindAnyObjectByType.html](https://docs.unity3d.com/ScriptReference/Object.FindAnyObjectByType.html)
* Object.DontDestroyOnLoad: [https://docs.unity3d.com/ScriptReference/Object.DontDestroyOnLoad.html](https://docs.unity3d.com/ScriptReference/Object.DontDestroyOnLoad.html)
* Application.quitting: [https://docs.unity3d.com/ScriptReference/Application-quitting.html](https://docs.unity3d.com/ScriptReference/Application-quitting.html)
* Application.logMessageReceivedThreaded (thread safety note): [https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Application-logMessageReceivedThreaded.html](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Application-logMessageReceivedThreaded.html)
* GameObject.Find (not recommended in Update): [https://docs.unity3d.com/ScriptReference/GameObject.Find.html](https://docs.unity3d.com/ScriptReference/GameObject.Find.html)
* Issue Tracker: RuntimeInitializeOnLoadMethodAttribute not invoked if class is generic: [https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic](https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic)
* Unity Discussions: “RuntimeInitializeOnLoad methods cannot be in generic classes” (error report): [https://discussions.unity.com/t/method-init-is-in-a-generic-class-but-runtimeinitializeonload-methods-cannot-be-in-generic-classes/1698250](https://discussions.unity.com/t/method-init-is-in-a-generic-class-but-runtimeinitializeonload-methods-cannot-be-in-generic-classes/1698250)

## License

MIT
