namespace Singletons.Policy
{
    /// <summary>
    /// Policy for scene-local singletons: no persistence, no auto-creation.
    /// </summary>
    /// <seealso cref="Singletons.SceneSingletonBehaviour{T}"/>
    public readonly struct SceneScopedPolicy : ISingletonPolicy
    {
        /// <summary>
        /// Always <c>false</c>. Instance is destroyed when its scene unloads.
        /// </summary>
        public bool PersistAcrossScenes => false;

        /// <summary>
        /// Always <c>false</c>. Requires pre-placed instance; no auto-creation.
        /// </summary>
        public bool AutoCreateIfMissing => false;
    }
}
