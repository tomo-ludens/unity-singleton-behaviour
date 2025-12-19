using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// Type-per-singleton base class for <see cref="MonoBehaviour"/>.
    /// Provides <see cref="Instance"/> (auto-create) and <see cref="TryGetInstance(out T)"/> (no-create),
    /// persists the instance across scene loads via <see cref="Object.DontDestroyOnLoad(Object)"/>,
    /// and cooperates with <see cref="SingletonRuntime"/> to remain correct when Domain Reload is disabled.
    /// </summary>
    /// <remarks>
    /// Use <see cref="OnSingletonAwake"/> / <see cref="OnSingletonDestroy"/> for customization.
    /// Do not implement Awake/OnDestroy in derived classes (Unity message methods are name-invoked).
    /// Lookup uses <see cref="Object.FindAnyObjectByType{T}()"/> and therefore does not return assets,
    /// inactive objects, or objects with <see cref="HideFlags.DontSave"/> set.
    /// </remarks>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// Cached singleton instance for this closed generic type (one per T).
        /// Cleared when a new Play session starts (Domain Reload disabled-safe).
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        private static T _instance;

        /// <summary>
        /// Cached Play session id for this closed generic type.
        /// Used to detect a new Play session and invalidate the cached instance.
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        private static int _cachedPlaySessionId = -1;

        /// <summary>
        /// Mandatory access point: returns the singleton instance.
        /// If no instance exists in the loaded scene, it auto-creates one.
        /// Returns null while the application is quitting.
        /// </summary>
        public static T Instance
        {
            get
            {
                EnsurePlaySession();

                if (SingletonRuntime.IsQuitting) return null;
                if (_instance != null) return _instance;

                _instance = Object.FindAnyObjectByType<T>();
                if (_instance != null) return _instance;

                var go = new GameObject(typeof(T).Name);
                _instance = go.AddComponent<T>();
                return _instance;
            }
        }

        /// <summary>
        /// Optional access point: does not create an instance.
        /// If an instance exists in the loaded scene, it caches it into <paramref name="instance"/> and returns true.
        /// Returns false while the application is quitting, or when no instance exists.
        /// </summary>
        public static bool TryGetInstance(out T instance)
        {
            EnsurePlaySession();

            if (SingletonRuntime.IsQuitting)
            {
                instance = null;
                return false;
            }

            if (_instance != null)
            {
                instance = _instance;
                return true;
            }

            _instance = Object.FindAnyObjectByType<T>();
            instance = _instance;
            return instance != null;
        }

        protected void Awake()
        {
            // Do not (re)bind or persist during shutdown / Play Mode exit.
            if (SingletonRuntime.IsQuitting)
            {
                Destroy(this.gameObject);
                return;
            }

            // Prefer using the previously established instance for duplicate rejection,
            // even if the play-session id might be updated later in startup.
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            EnsurePlaySession();

            // Re-check after possible invalidation (rare, but safe).
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            _instance = this as T;

            if (this.transform.parent != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    message: $"[{typeof(T).Name}] Reparented to root for DontDestroyOnLoad.",
                    context: this
                );
#endif
                this.transform.SetParent(parent: null, worldPositionStays: true);
            }

            DontDestroyOnLoad(this.gameObject);
            OnSingletonAwake();
        }

        /// <summary>
        /// Customization hook called after the singleton instance is established and made persistent.
        /// </summary>
        protected virtual void OnSingletonAwake()
        {
        }

        protected void OnDestroy()
        {
            if (_instance != this) return;

            _instance = null;
            OnSingletonDestroy();
        }

        /// <summary>
        /// Customization hook called only when the established singleton instance is being destroyed.
        /// </summary>
        protected virtual void OnSingletonDestroy()
        {
        }

        /// <summary>
        /// Syncs the cached play-session id with <see cref="SingletonRuntime.PlaySessionId"/>.
        /// If the play session changed (e.g., Domain Reload disabled), invalidates the cached instance.
        /// </summary>
        private static void EnsurePlaySession()
        {
            var current = SingletonRuntime.PlaySessionId;
            if (_cachedPlaySessionId == current) return;

            _cachedPlaySessionId = current;
            _instance = null;
        }
    }
}
