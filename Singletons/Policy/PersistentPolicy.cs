namespace Singletons.Policy
{
    /// <summary>
    /// Policy for application-lifetime singletons: persists across scenes, auto-creates if missing.
    /// </summary>
    /// <seealso cref="Singletons.PersistentSingletonBehaviour{T}"/>
    public readonly struct PersistentPolicy : ISingletonPolicy
    {
        /// <summary>
        /// Always <c>true</c>. Instance survives scene loads via <c>DontDestroyOnLoad</c>.
        /// </summary>
        public bool PersistAcrossScenes => true;

        /// <summary>
        /// Always <c>true</c>. Missing instance is auto-created on first access.
        /// </summary>
        public bool AutoCreateIfMissing => true;
    }
}
