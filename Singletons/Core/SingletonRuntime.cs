using System.Threading;
using UnityEngine;

namespace Singletons.Core
{
    /// <summary>
    /// Manages Play session state for singleton infrastructure.
    /// </summary>
    /// <remarks>
    /// <para><b>PlaySessionId:</b> Increments each Play Mode enter. Invalidates stale static references surviving Domain Reload skip.</para>
    /// <para><b>IsQuitting:</b> True during quit (runtime + Editor exit). Singleton access returns null to prevent resurrection.</para>
    /// </remarks>
    internal static class SingletonRuntime
    {
        private const int InvalidFrameCount = -1;
        private const int UninitializedMainThreadId = -1;

        private static int _lastBeginFrame = InvalidFrameCount;
        private static int _mainThreadId = UninitializedMainThreadId;

        /// <summary>
        /// Current Play session ID. Increments each Play Mode enter.
        /// </summary>
        public static int PlaySessionId { get; private set; }

        /// <summary>
        /// True when application is quitting. Singleton access returns null during quit.
        /// </summary>
        public static bool IsQuitting { get; private set; }

        private static bool IsMainThread => _mainThreadId != UninitializedMainThreadId && _mainThreadId == Thread.CurrentThread.ManagedThreadId;

        internal static void EnsureInitializedForCurrentPlaySession()
        {
            if (!Application.isPlaying) return;

            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;

            if (_mainThreadId == UninitializedMainThreadId)
            {
                TryLazyCaptureMainThreadId(callerContext: "EnsureInitializedForCurrentPlaySession");
            }

#if UNITY_EDITOR
            EnsureEditorHooks();
#endif
        }

        internal static bool ValidateMainThread(string callerContext)
        {
            if (!Application.isPlaying) return true;

            if (_mainThreadId == UninitializedMainThreadId)
            {
                EnsureInitializedForCurrentPlaySession();

                if (_mainThreadId == UninitializedMainThreadId && !TryLazyCaptureMainThreadId(callerContext: callerContext))
                {
                    SingletonLogger.LogError(message: $"{callerContext} must be called from the main thread, but the main thread ID is not initialized yet.\nCurrent thread: {Thread.CurrentThread.ManagedThreadId}.");
                    return false;
                }
            }

            if (IsMainThread)
            {
                return true;
            }

            SingletonLogger.LogError(message: $"{callerContext} must be called from the main thread.\nCurrent thread: {Thread.CurrentThread.ManagedThreadId}, Main thread: {_mainThreadId}.");
            return false;
        }

        /// <summary>
        /// First reliable point in Play Mode where main thread ID can be captured.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SubsystemRegistration()
        {
            if (!Application.isPlaying) return;

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            BeginNewPlaySession();
        }

        private static void BeginNewPlaySession()
        {
            if (Time.frameCount == _lastBeginFrame) return;

            _lastBeginFrame = Time.frameCount;
            EnsureInitializedForCurrentPlaySession();

            unchecked
            {
                PlaySessionId++;
            }

            IsQuitting = false;
        }

        private static void OnQuitting()
        {
            IsQuitting = true;
        }

        private static bool TryLazyCaptureMainThreadId(string callerContext)
        {
            if (_mainThreadId != UninitializedMainThreadId) return true;
            if (!Application.isPlaying) return false;
            if (SynchronizationContext.Current == null) return false;

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            SingletonLogger.LogWarning(message: $"Main thread ID lazily captured as {_mainThreadId}.\nContext: '{callerContext}'.");
            return true;
        }

#if UNITY_EDITOR
        private static bool _editorHooksInstalled;

        private static void EnsureEditorHooks()
        {
            if (_editorHooksInstalled) return;

            _editorHooksInstalled = true;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                IsQuitting = true;
            }
        }
#endif
    }
}
