using Singletons.Core;
using Singletons.Policy;

namespace Singletons
{
    /// <summary>
    /// Base class for scene-local singletons that are destroyed on scene unload.
    /// </summary>
    /// <typeparam name="T">The concrete singleton type (must be sealed).</typeparam>
    /// <remarks>
    /// <para><b>Behavior:</b> No auto-creation (must be pre-placed), no <c>DontDestroyOnLoad</c>. Missing instance throws (DEV) or returns null (Release).</para>
    /// <para><b>Access:</b> Prefer <c>TryGetInstance(out T)</c> for safe access.</para>
    /// <para><b>Lifecycle:</b> Use <c>OnSingletonAwake()</c>/<c>OnSingletonDestroy()</c>; base Awake/OnDestroy calls required if overriding.</para>
    /// <para><b>vs Persistent:</b> Use <see cref="PersistentSingletonBehaviour{T}"/> for global managers that must persist.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class LevelManager : SceneSingletonBehaviour&lt;LevelManager&gt;
    /// {
    ///     protected override void OnSingletonAwake() { /* init */ }
    /// }
    /// // Safe access:
    /// if (LevelManager.TryGetInstance(out var mgr)) { mgr.DoSomething(); }
    /// </code>
    /// </example>
    public abstract class SceneSingletonBehaviour<T> : SingletonBehaviour<T, SceneScopedPolicy> where T : SceneSingletonBehaviour<T>
    {
    }
}
