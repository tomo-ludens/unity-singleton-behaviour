namespace Singletons.Policy
{
    /// <summary>
    /// Contract for singleton lifecycle policies. Used as a generic constraint in <c>SingletonBehaviour&lt;T, TPolicy&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Implementations MUST be <c>readonly struct</c> with constant property values (enables zero-allocation <c>default(TPolicy)</c>).
    /// </remarks>
    /// <seealso cref="PersistentPolicy"/>
    /// <seealso cref="SceneScopedPolicy"/>
    public interface ISingletonPolicy
    {
        /// <summary>
        /// If true, applies <c>DontDestroyOnLoad</c> to persist across scene loads.
        /// </summary>
        bool PersistAcrossScenes { get; }

        /// <summary>
        /// If true, auto-creates instance on first access when none exists.
        /// </summary>
        bool AutoCreateIfMissing { get; }
    }
}
