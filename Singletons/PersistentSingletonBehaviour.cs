using Singletons.Core;
using Singletons.Policy;

namespace Singletons
{
    /// <summary>
    /// Base class for application-lifetime singletons that persist across all scene loads.
    /// </summary>
    /// <typeparam name="T">The concrete singleton type (must be sealed).</typeparam>
    /// <remarks>
    /// <para><b>Behavior:</b> Auto-creates on first access, applies <c>DontDestroyOnLoad</c>, destroys duplicates.</para>
    /// <para><b>Lifecycle:</b> Use <c>OnSingletonAwake()</c>/<c>OnSingletonDestroy()</c>; base Awake/OnDestroy calls required if overriding.</para>
    /// <para><b>vs Scene:</b> Use <see cref="SceneSingletonBehaviour{T}"/> for scene-local managers that reset on reload.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class AudioManager : PersistentSingletonBehaviour&lt;AudioManager&gt;
    /// {
    ///     protected override void OnSingletonAwake() { /* init */ }
    /// }
    /// </code>
    /// </example>
    public abstract class PersistentSingletonBehaviour<T>: SingletonBehaviour<T, PersistentPolicy> where T : PersistentSingletonBehaviour<T>
    {
    }
}
