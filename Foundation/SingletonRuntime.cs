using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// Central runtime lifecycle for singleton infrastructure.
    /// Owns play-session initialization and quitting state.
    /// </summary>
    /// <remarks>
    /// Initialization runs at <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>,
    /// i.e., before the first scene is loaded.
    /// </remarks>
    public static class SingletonRuntime
    {
        /// <summary>
        /// Increments on each runtime startup (including entering Play Mode).
        /// Used to invalidate cached singleton statics when Domain Reload is disabled.
        /// </summary>
        public static int PlaySessionId { get; private set; }

        /// <summary>
        /// True while the application is quitting (or exiting Play Mode in the Editor).
        /// </summary>
        public static bool IsQuitting { get; private set; }

        /// <summary>
        /// Initializes singleton runtime state at <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>.
        /// Increments <see cref="PlaySessionId"/>, clears <see cref="IsQuitting"/>, and (re)subscribes to
        /// <see cref="Application.quitting"/> to avoid duplicate handlers when Domain Reload is disabled.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            PlaySessionId++;
            IsQuitting = false;

            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        /// <summary>
        /// Marks the runtime as quitting to prevent late singleton creation during shutdown.
        /// </summary>
        private static void OnQuitting() => IsQuitting = true;
    }
}
