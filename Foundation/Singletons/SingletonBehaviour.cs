using System;
using UnityEngine;

namespace Foundation.Singletons
{
    /// <summary>
    /// Type-per-singleton base class for MonoBehaviour with soft reset per Play session.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>All public API must be called from the main thread.</item>
    ///   <item>In DEV/EDITOR, inactive instances block auto-creation (fail-fast).</item>
    ///   <item>FindAnyObjectByType may return derived types; AsExactType enforces T == actual type.</item>
    /// </list>
    /// </remarks>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        private const int UninitializedPlaySessionId = -1;
        private const FindObjectsInactive FindInactivePolicy = FindObjectsInactive.Exclude;

        // ReSharper disable once StaticMemberInGenericType
        private static T _instance;

        // ReSharper disable once StaticMemberInGenericType
        private static int _cachedPlaySessionId = UninitializedPlaySessionId;

        private int _initializedPlaySessionId = UninitializedPlaySessionId;
        private bool _isPersistent;

        /// <summary>
        /// Returns the singleton instance. Auto-creates if missing (Play Mode only).
        /// </summary>
        /// <returns>The instance, or null while quitting / off main thread (Edit Mode: null if missing).</returns>
        /// <exception cref="InvalidOperationException">
        /// (DEV/EDITOR) Thrown when an inactive/disabled instance exists (auto-creation is blocked).
        /// </exception>
        public static T Instance
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return AsExactType(
                        candidate: FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy),
                        callerContext: $"{typeof(T).Name}.Instance[EditMode]"
                    );
                }

                if (!SingletonRuntime.AssertMainThread(callerContext: $"{typeof(T).Name}.Instance"))
                {
                    return null;
                }

                InvalidateInstanceCacheIfPlaySessionChanged();

                if (SingletonRuntime.IsQuitting) return null;
                if (_instance != null) return _instance;

                var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                _instance = AsExactType(candidate: candidate, callerContext: $"{typeof(T).Name}.Instance[PlayMode]");
                if (_instance != null) return _instance;

                AssertNoInactiveInstanceExists();

                _instance = CreateInstance();
                return _instance;
            }
        }

        /// <summary>
        /// Gets the singleton instance without creating one.
        /// </summary>
        /// <returns>False if: quitting, no instance, or background thread.</returns>
        public static bool TryGetInstance(out T instance)
        {
            if (!Application.isPlaying)
            {
                instance = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                instance = AsExactType(candidate: instance, callerContext: $"{typeof(T).Name}.TryGetInstance[EditMode]");
                return instance != null;
            }

            if (!SingletonRuntime.AssertMainThread(callerContext: $"{typeof(T).Name}.TryGetInstance"))
            {
                instance = null;
                return false;
            }

            InvalidateInstanceCacheIfPlaySessionChanged();

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

            var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
            candidate = AsExactType(candidate: candidate, callerContext: $"{typeof(T).Name}.TryGetInstance[PlayMode]");
            if (candidate != null)
            {
                _instance = candidate;
                instance = _instance;
                return true;
            }

            instance = null;
            return false;
        }

        protected void Awake()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        protected void OnEnable()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        protected void OnDestroy()
        {
            if (!ReferenceEquals(objA: _instance, objB: this)) return;

            _instance = null;
            this.OnSingletonDestroy();
        }

        /// <summary>
        /// Called once per Play session after singleton is established.
        /// </summary>
        protected virtual void OnSingletonAwake()
        {
        }

        /// <summary>
        /// Called when singleton instance is destroyed.
        /// </summary>
        protected virtual void OnSingletonDestroy()
        {
        }

        private static T CreateInstance()
        {
            var go = new GameObject(name: typeof(T).Name);
            DontDestroyOnLoad(target: go);

            var instance = go.AddComponent<T>();
            instance._isPersistent = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                message: $"[{typeof(T).Name}] Auto-created (no instance found).",
                context: instance
            );
#endif
            return instance;
        }

        /// <summary>
        /// Enforces type-per-singleton: candidate must be exactly T, not a derived type.
        /// </summary>
        private static T AsExactType(T candidate, string callerContext)
        {
            if (candidate == null) return null;

            if (candidate.GetType() == typeof(T))
            {
                return candidate;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                message: $"[{typeof(T).Name}] Type mismatch found via '{callerContext}'. " +
                         $"Expected EXACT type '{typeof(T).Name}', but found '{candidate.GetType().Name}'.",
                context: candidate
            );
#endif

            // Play Mode: destroy to enforce invariant.
            // Edit Mode: log only to avoid Undo/Inspector side effects.
            if (Application.isPlaying)
            {
                Destroy(obj: candidate.gameObject);
            }

            return null;
        }

        /// <summary>
        /// DEV/EDITOR only: throws if an inactive instance exists to prevent silent auto-create conflicts.
        /// </summary>
        private static void AssertNoInactiveInstanceExists()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindObjectsInactive.Include);

            if (candidate != null && !candidate.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    message: $"[{typeof(T).Name}] Auto-create BLOCKED: an inactive/disabled instance exists ('{candidate.name}', actual type: '{candidate.GetType().Name}'). " +
                             "Enable/activate the existing instance, or remove it from the scene."
                );
            }
#endif
        }

        private static void InvalidateInstanceCacheIfPlaySessionChanged()
        {
            if (!Application.isPlaying) return;

            SingletonRuntime.EnsureInitializedForCurrentPlaySession();

            var current = SingletonRuntime.PlaySessionId;
            if (_cachedPlaySessionId == current) return;

            _cachedPlaySessionId = current;
            _instance = null;
        }

        private void InitializeForCurrentPlaySessionIfNeeded()
        {
            InvalidateInstanceCacheIfPlaySessionChanged();

            if (SingletonRuntime.IsQuitting)
            {
                Destroy(obj: this.gameObject);
                return;
            }

            if (!this.TryEstablishAsInstance()) return;

            var currentPlaySessionId = SingletonRuntime.PlaySessionId;
            if (this._initializedPlaySessionId == currentPlaySessionId) return;

            this.EnsurePersistent();

            this._initializedPlaySessionId = currentPlaySessionId;
            this.OnSingletonAwake();
        }

        private bool TryEstablishAsInstance()
        {
            if (_instance != null)
            {
                if (ReferenceEquals(objA: _instance, objB: this)) return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    message: $"[{typeof(T).Name}] Duplicate detected. Existing='{_instance.name}', destroying '{this.name}'.",
                    context: this
                );
#endif
                Destroy(obj: this.gameObject);
                return false;
            }

            // CRTP violation check: GetType() must equal typeof(T).
            if (this.GetType() != typeof(T))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    message: $"[{typeof(T).Name}] Type mismatch detected. Expected='{typeof(T).Name}', Actual='{this.GetType().Name}', destroying '{this.name}'.",
                    context: this
                );
#endif
                Destroy(obj: this.gameObject);
                return false;
            }

            var typedThis = this as T;
            if (typedThis == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    message: $"[{typeof(T).Name}] Internal cast failure. Expected='{typeof(T).Name}', destroying '{this.name}'.",
                    context: this
                );
#endif
                Destroy(obj: this.gameObject);
                return false;
            }

            _instance = typedThis;
            return true;
        }

        private void EnsurePersistent()
        {
            if (this._isPersistent) return;

            // DontDestroyOnLoad requires root GameObject.
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

            DontDestroyOnLoad(target: this.gameObject);
            this._isPersistent = true;
        }
    }
}
