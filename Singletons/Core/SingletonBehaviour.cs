using Singletons.Policy;
using UnityEngine;

namespace Singletons.Core
{
    /// <summary>
    /// Policy-driven singleton MonoBehaviour.
    /// </summary>
    /// <remarks>
    /// <para><b>CRITICAL - EXACT TYPE MATCH:</b>
    /// Do NOT subclass derived singletons.
    /// <c>class Derived : MySingleton</c> accessing <c>MySingleton.Instance</c> will fail type check.
    /// Each concrete singleton must directly inherit <c>SingletonBehaviour&lt;T, TPolicy&gt;</c>.</para>
    /// <para><b>STATIC FIELD ISOLATION:</b>
    /// <c>_instance</c> and <c>_cachedPlaySessionId</c> are per generic instantiation (T, TPolicy).
    /// Different T (or different policy) yields independent singleton storage.</para>
    /// <para><b>RELEASE BUILD BEHAVIOR:</b>
    /// DEV/EDITOR-only validations use <c>[Conditional]</c>. In release builds,
    /// <see cref="SingletonLogger.ThrowInvalidOperation"/> calls are stripped and the API returns <c>null</c>
    /// (or <c>false</c>) on validation failures instead of throwing.</para>
    /// <para><b>LIFECYCLE OVERRIDES:</b>
    /// If you override <c>Awake</c>/<c>OnEnable</c>/<c>OnDestroy</c>, you must call base.
    /// If you forget, singleton establishment and <c>OnSingletonAwake()</c> may be deferred until the first
    /// <c>Instance</c>/<c>TryGetInstance</c> access (safety net), which can hide ordering/duplication issues.
    /// Prefer <c>OnSingletonAwake</c>/<c>OnSingletonDestroy</c> hooks.</para>
    /// <para><b>(Optional)</b> Do not keep singleton components disabled; keep them active and enabled.</para>
    /// </remarks>
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
                        callerContext: $"{typeof(T).Name}.Instance[EditMode]"
                    );
                }

                if (!SingletonRuntime.ValidateMainThread(callerContext: $"{typeof(T).Name}.Instance"))
                {
                    return null;
                }

                InvalidateInstanceCacheIfPlaySessionChanged();

                if (SingletonRuntime.IsQuitting) return null;
                if (_instance != null) return _instance;

                var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                candidate = AsExactType(candidate: candidate, callerContext: $"{typeof(T).Name}.Instance[PlayMode]");

                if (candidate != null)
                {
                    if (!candidate.isActiveAndEnabled)
                    {
                        // In release: call stripped, returns null below.
                        SingletonLogger.ThrowInvalidOperation(
                            message: $"[{typeof(T).Name}] Inactive/disabled instance detected.\n" +
                                     $"Found: '{candidate.name}' (type: '{candidate.GetType().Name}').\n" +
                                     "Enable/activate it or remove it from the scene."
                        );
                        return null;
                    }

                    candidate.InitializeForCurrentPlaySessionIfNeeded();
                    if (_instance != null) return _instance;
                }

                if (!Policy.AutoCreateIfMissing)
                {
                    // In release: call stripped, returns null below.
                    SingletonLogger.ThrowInvalidOperation(
                        message: $"[{typeof(T).Name}] No instance found and auto-creation is disabled by policy.\n" +
                                 "Place an active instance in the scene."
                    );
                    return null;
                }

                ThrowIfInactiveInstanceExists();

                _instance = CreateInstance();
                return _instance;
            }
        }

        /// <summary>
        /// Non-creating lookup. Does NOT trigger auto-create even if policy allows.
        /// </summary>
        public static bool TryGetInstance(out T instance)
        {
            if (!Application.isPlaying)
            {
                instance = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                instance = AsExactType(candidate: instance, callerContext: $"{typeof(T).Name}.TryGetInstance[EditMode]");
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

            if (_instance != null)
            {
                instance = _instance;
                return true;
            }

            var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
            candidate = AsExactType(candidate: candidate, callerContext: $"{typeof(T).Name}.TryGetInstance[PlayMode]");

            if (candidate != null)
            {
                if (!candidate.isActiveAndEnabled)
                {
                    // In release: call stripped, returns false below.
                    SingletonLogger.ThrowInvalidOperation(
                        message: $"[{typeof(T).Name}] Inactive/disabled instance detected.\n" +
                                 $"Found: '{candidate.name}' (type: '{candidate.GetType().Name}').\n" +
                                 "Enable/activate it or remove it from the scene."
                    );
                    instance = null;
                    return false;
                }

                candidate.InitializeForCurrentPlaySessionIfNeeded();

                if (_instance != null)
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
                message: $"[{typeof(T).Name}] Auto-created.",
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
                message: $"[{typeof(T).Name}] Type mismatch found via '{callerContext}'.\n" +
                         $"Expected EXACT type '{typeof(T).Name}', but found '{candidate.GetType().Name}'.",
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
                        message: $"[{typeof(T).Name}] Auto-create BLOCKED: inactive instance exists " +
                                 $"('{instance.name}', type: '{instance.GetType().Name}'). " +
                                 "Enable it or remove from scene."
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
            if (_instance != null)
            {
                if (ReferenceEquals(objA: _instance, objB: this)) return true;

                SingletonLogger.LogWarning(
                    message: $"[{typeof(T).Name}] Duplicate detected. Existing='{_instance.name}', destroying '{this.name}'.",
                    context: this
                );

                Destroy(obj: this.gameObject);
                return false;
            }

            if (this.GetType() != typeof(T))
            {
                SingletonLogger.LogError(
                    message: $"[{typeof(T).Name}] Type mismatch. Expected='{typeof(T).Name}', Actual='{this.GetType().Name}', destroying '{this.name}'.",
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
                    message: $"[{typeof(T).Name}] Reparented to root for DontDestroyOnLoad.",
                    context: this
                );

                this.transform.SetParent(parent: null, worldPositionStays: true);
            }

            DontDestroyOnLoad(target: this.gameObject);
            this._isPersistent = true;
        }
    }
}
