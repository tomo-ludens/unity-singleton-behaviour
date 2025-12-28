using Singletons.Policy;
using UnityEngine;

namespace Singletons.Core
{
    /// <summary>
    /// Policy-driven singleton base class for <see cref="MonoBehaviour"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Exact type only:</b>
    /// Concrete singletons MUST be <c>sealed</c>. Do NOT derive from a concrete singleton.
    /// <c>class Derived : MySingleton</c> is invalid and is rejected by the runtime exact-type check.</para>
    /// <para><b>Static isolation:</b>
    /// <c>_instance</c> and <c>_cachedPlaySessionId</c> are isolated per generic instantiation (<c>T</c>, <c>TPolicy</c>).
    /// Changing <c>T</c> or the policy yields an independent singleton storage.</para>
    /// <para><b>Release behavior:</b>
    /// DEV/EDITOR validations use <c>[Conditional]</c>. In release builds, validation call sites are stripped from IL.
    /// On validation failures this API returns <c>null</c>/<c>false</c> instead of throwing. Callers MUST handle that.</para>
    /// <para><b>Lifecycle overrides:</b>
    /// If you override <c>Awake</c>, <c>OnEnable</c>, or <c>OnDestroy</c>, you MUST call the base method.
    /// Missing a base call is a bug.
    /// A safety net may establish the singleton on the first <c>Instance</c>/<c>TryGetInstance</c> access, which can
    /// conceal ordering and duplication issues. Use <c>OnSingletonAwake</c>/<c>OnSingletonDestroy</c> as extension points.</para>
    /// <para><b>Active + enabled:</b>
    /// Singleton components MUST remain active and enabled.
    /// Disabled components are treated as invalid and block safe access.</para>
    /// <para><b>Main thread only:</b>
    /// All public API is main-thread only because it calls UnityEngine APIs (Find/Create/Destroy).</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public abstract class SingletonBehaviour<T, TPolicy> : MonoBehaviour
        where T : SingletonBehaviour<T, TPolicy>
        where TPolicy : struct, ISingletonPolicy
    {
        private const int UninitializedPlaySessionId = -1;
        private const FindObjectsInactive FindInactivePolicy = FindObjectsInactive.Exclude;

        private static readonly TPolicy Policy = default;

        // Per generic instantiation (T, TPolicy). Not shared across different T.
        // ReSharper disable once StaticMemberInGenericType
        private static T _instance;

        // ReSharper disable once StaticMemberInGenericType
        private static int _cachedPlaySessionId = UninitializedPlaySessionId;

        private int _initializedPlaySessionId = UninitializedPlaySessionId;
        private bool _isPersistent;

        /// <summary>
        /// Returns singleton instance. Returns null during quit, from background thread, or if validation fails in release.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return AsExactType(
                        candidate: FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy),
                        callerContext: "Instance[EditMode]"
                    );
                }

                if (!SingletonRuntime.ValidateMainThread(callerContext: $"{typeof(T).Name}.Instance"))
                {
                    return null;
                }

                InvalidateInstanceCacheIfPlaySessionChanged();

                if (SingletonRuntime.IsQuitting) return null;
                if (HasCachedInstance) return _instance;

                var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                candidate = AsExactType(candidate: candidate, callerContext: "Instance[PlayMode]");

                if (candidate != null)
                {
                    if (!candidate.isActiveAndEnabled)
                    {
                        // In release: call stripped, returns null below.
                        SingletonLogger.ThrowInvalidOperation(
                            message: $"Inactive/disabled instance detected.\nFound: '{candidate.name}' (type: '{candidate.GetType().Name}').\nEnable/activate it or remove it from the scene.",
                            typeTag: LogCategoryName
                        );

                        return null;
                    }

                    candidate.InitializeForCurrentPlaySessionIfNeeded();
                    if (HasCachedInstance) return _instance;
                }

                if (!Policy.AutoCreateIfMissing)
                {
                    // In release: call stripped, returns null below.
                    SingletonLogger.ThrowInvalidOperation(
                        message: "No instance found and auto-creation is disabled by policy.\nPlace an active instance in the scene.",
                        typeTag: LogCategoryName
                    );

                    return null;
                }

                ThrowIfInactiveInstanceExists();

                _instance = CreateInstance();
                return _instance;
            }
        }

        private static bool HasCachedInstance => _instance != null;
        private static string LogCategoryName => typeof(T).FullName;

        /// <summary>
        /// Non-creating lookup. Does NOT trigger auto-create even if policy allows.
        /// </summary>
        public static bool TryGetInstance(out T instance)
        {
            if (!Application.isPlaying)
            {
                instance = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                instance = AsExactType(candidate: instance, callerContext: "TryGetInstance[EditMode]");
                return instance != null;
            }

            if (!SingletonRuntime.ValidateMainThread(callerContext: $"{typeof(T).Name}.TryGetInstance"))
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

            if (HasCachedInstance)
            {
                instance = _instance;
                return true;
            }

            var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
            candidate = AsExactType(candidate: candidate, callerContext: "TryGetInstance[PlayMode]");

            if (candidate != null)
            {
                if (!candidate.isActiveAndEnabled)
                {
                    // In release: call stripped, returns false below.
                    SingletonLogger.ThrowInvalidOperation(
                        message: $"Inactive/disabled instance detected.\nFound: '{candidate.name}' (type: '{candidate.GetType().Name}').\nEnable/activate it or remove it from the scene.",
                        typeTag: LogCategoryName
                    );
                    instance = null;
                    return false;
                }

                candidate.InitializeForCurrentPlaySessionIfNeeded();

                if (HasCachedInstance)
                {
                    instance = _instance;
                    return true;
                }
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// Override requires base.Awake() call.
        /// </summary>
        protected virtual void Awake()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        /// <summary>
        /// Override requires base.OnEnable() call.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        /// <summary>
        /// Override requires base.OnDestroy() call.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (!ReferenceEquals(objA: _instance, objB: this)) return;

            _instance = null;
            this.OnSingletonDestroy();
        }

        /// <summary>
        /// Called once per Play session after singleton established.
        /// </summary>
        protected virtual void OnSingletonAwake()
        {
        }

        /// <summary>
        /// Called when THIS instance is destroyed as the singleton. NOT called for destroyed duplicates.
        /// </summary>
        protected virtual void OnSingletonDestroy()
        {
        }

        private static T CreateInstance()
        {
            var go = new GameObject(name: typeof(T).Name);

            if (Policy.PersistAcrossScenes)
            {
                DontDestroyOnLoad(target: go);
            }

            var instance = go.AddComponent<T>();
            instance._isPersistent = Policy.PersistAcrossScenes;

            // Ensure initialization even if derived Awake doesn't call base.
            instance.InitializeForCurrentPlaySessionIfNeeded();

            SingletonLogger.LogWarning(
                message: "Auto-created.",
                typeTag: LogCategoryName,
                context: instance
            );

            return instance;
        }

        private static T AsExactType(T candidate, string callerContext)
        {
            if (candidate == null) return null;

            if (candidate.GetType() == typeof(T))
            {
                return candidate;
            }

            SingletonLogger.LogError(
                message: $"Type mismatch found via '{callerContext}'.\nExpected EXACT type '{typeof(T).Name}', but found '{candidate.GetType().Name}'.",
                typeTag: LogCategoryName,
                context: candidate
            );

            if (Application.isPlaying)
            {
                Destroy(obj: candidate.gameObject);
            }

            return null;
        }

        // In release: entire method call stripped, FindObjectsByType not executed.
        [System.Diagnostics.Conditional(conditionString: "UNITY_EDITOR"), System.Diagnostics.Conditional(conditionString: "DEVELOPMENT_BUILD")]
        private static void ThrowIfInactiveInstanceExists()
        {
            var allInstances = FindObjectsByType<T>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);

            foreach (var instance in allInstances)
            {
                if (!instance.isActiveAndEnabled)
                {
                    SingletonLogger.ThrowInvalidOperation(
                        message: $"Auto-create BLOCKED: inactive instance exists ('{instance.name}', type: '{instance.GetType().Name}').\nEnable it or remove from scene.",
                        typeTag: LogCategoryName
                    );
                }
            }
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
            if (HasCachedInstance)
            {
                if (ReferenceEquals(objA: _instance, objB: this)) return true;

                SingletonLogger.LogWarning(
                    message: $"Duplicate detected. Existing='{_instance.name}', destroying '{this.name}'.",
                    typeTag: LogCategoryName,
                    context: this
                );

                Destroy(obj: this.gameObject);
                return false;
            }

            if (this.GetType() != typeof(T))
            {
                SingletonLogger.LogError(
                    message: $"Type mismatch. Expected='{typeof(T).Name}', Actual='{this.GetType().Name}', destroying '{this.name}'.",
                    typeTag: LogCategoryName,
                    context: this
                );

                Destroy(obj: this.gameObject);
                return false;
            }

            _instance = (T)this;
            return true;
        }

        private void EnsurePersistent()
        {
            if (!Policy.PersistAcrossScenes) return;
            if (this._isPersistent) return;

            if (this.transform.parent != null)
            {
                SingletonLogger.LogWarning(
                    message: "Reparented to root for DontDestroyOnLoad.",
                    typeTag: LogCategoryName,
                    context: this
                );

                this.transform.SetParent(parent: null, worldPositionStays: true);
            }

            DontDestroyOnLoad(target: this.gameObject);
            this._isPersistent = true;
        }
    }
}
