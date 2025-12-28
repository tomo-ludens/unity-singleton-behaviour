using System.Threading;
using UnityEngine;

namespace Singletons.Core
{
    /// <summary>
    /// Manages Play session state for singleton infrastructure.
    /// </summary>
    /// <remarks>
    /// <para><b>PlaySessionId:</b> Increments each Play Mode enter. Used by SingletonBehaviour
    /// to invalidate stale static references that survive Domain Reload skip in Editor.</para>
    /// <para><b>IsQuitting:</b> Set true on BOTH <c>Application.quitting</c> AND
    /// <c>EditorApplication.playModeStateChanged(ExitingPlayMode)</c>.
    /// Singleton access returns null during quit to prevent resurrection.</para>
    /// </remarks>
    internal static class SingletonRuntime
    {
        private const int InvalidFrameCount = -1;
        private const int UninitializedMainThreadId = -1;

        private static int _lastBeginFrame = InvalidFrameCount;
        private static int _mainThreadId = UninitializedMainThreadId;

        public static int PlaySessionId { get; private set; }
        public static bool IsQuitting { get; private set; }

        private static string LogCategoryName => nameof(SingletonRuntime);
        private static bool IsMainThread =>
            _mainThreadId != UninitializedMainThreadId &&
            _mainThreadId == Thread.CurrentThread.ManagedThreadId;

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
                    SingletonLogger.LogError(
                        message: $"{callerContext} must be called from the main thread, but the main thread ID is not initialized yet.\nCurrent thread: {Thread.CurrentThread.ManagedThreadId}.",
                        typeTag: LogCategoryName
                    );

                    return false;
                }
            }

            if (IsMainThread)
            {
                return true;
            }

            SingletonLogger.LogError(
                message: $"{callerContext} must be called from the main thread.\nCurrent thread: {Thread.CurrentThread.ManagedThreadId}, Main thread: {_mainThreadId}.",
                typeTag: LogCategoryName
            );

            return false;
        }

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

            SingletonLogger.LogWarning(
                message: $"Main thread ID lazily captured as {_mainThreadId}.\nContext: '{callerContext}'.",
                typeTag: LogCategoryName
            );
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
