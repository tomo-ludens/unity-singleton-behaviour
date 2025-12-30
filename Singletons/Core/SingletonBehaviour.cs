using Singletons.Policy;
using UnityEngine;

namespace Singletons.Core
{
    /// <summary>
    /// Policy-driven singleton base class for <see cref="MonoBehaviour"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Sealed only:</b> Concrete types MUST be <c>sealed</c>; subclassing is rejected at runtime.</para>
    /// <para><b>Release builds:</b> Validations are stripped via <c>[Conditional]</c>; API returns <c>null</c>/<c>false</c> on failure.</para>
    /// <para><b>Lifecycle:</b> Override <c>Awake</c>/<c>OnEnable</c>/<c>OnDestroy</c> MUST call base (checked at runtime via OnEnable).</para>
    /// <para><b>Constraints:</b> Component must stay active+enabled. Main-thread only.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public abstract class SingletonBehaviour<T, TPolicy> : MonoBehaviour
        where T : SingletonBehaviour<T, TPolicy>
        where TPolicy : struct, ISingletonPolicy
    {
        private const int UninitializedPlaySessionId = -1;
        private const FindObjectsInactive FindInactivePolicy = FindObjectsInactive.Exclude;

        private static readonly TPolicy Policy = default;

        // ReSharper disable once StaticMemberInGenericType
        private static T _instance;

        // ReSharper disable once StaticMemberInGenericType
        private static int _cachedPlaySessionId = UninitializedPlaySessionId;

        private int _initializedPlaySessionId = UninitializedPlaySessionId;
        private bool _isPersistent;
        private bool _baseAwakeCalled;

        /// <summary>
        /// Returns singleton instance. In PlayMode, returns null during quit, from background thread, or if validation fails in release.
        /// </summary>
        /// <returns>The singleton instance, or <c>null</c> if unavailable.</returns>
        public static T Instance
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return AsExactType(candidate: FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy), callerContext: "Instance[EditMode]");
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
                        // In release: ThrowInvalidOperation call stripped, returns null below.
                        SingletonLogger.ThrowInvalidOperation<T>(message: $"Inactive/disabled instance detected.\nFound: '{candidate.name}' (type: '{candidate.GetType().Name}').\nEnable/activate it or remove it from the scene.");
                    return null;
                    }

                    candidate.InitializeForCurrentPlaySessionIfNeeded();
                    if (HasCachedInstance) return _instance;
                }

                if (!Policy.AutoCreateIfMissing)
                {
                    SingletonLogger.ThrowInvalidOperation<T>(message: "No instance found and auto-creation is disabled by policy.\nPlace an active instance in the scene.");
                    return null;
                }

                ThrowIfInactiveInstanceExists();
                _instance = CreateInstance();
                return _instance;
            }
        }

        private static bool HasCachedInstance => _instance != null;

        /// <summary>
        /// Non-creating lookup. Does NOT trigger auto-create even if policy allows.
        /// </summary>
        /// <returns><c>true</c> if instance exists and is valid; otherwise <c>false</c>.</returns>
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
                    SingletonLogger.ThrowInvalidOperation<T>(message: $"Inactive/disabled instance detected.\nFound: '{candidate.name}' (type: '{candidate.GetType().Name}').\nEnable/activate it or remove it from the scene.");
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

            _baseAwakeCalled = true;
            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        /// <summary>
        /// Override requires base.OnEnable() call.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (!Application.isPlaying) return;

            if (!_baseAwakeCalled)
            {
                SingletonLogger.LogError<T>(message: $"base.Awake() was not called in {this.GetType().Name}.\nThis will prevent proper singleton initialization.\nMake sure to call base.Awake() at the beginning of your Awake() method.", context: this);
            }

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        /// <summary>
        /// Override requires base.OnDestroy() call.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (!ReferenceEquals(objA: _instance, objB: this)) return;

            _instance = null;
        }

        /// <summary>
        /// Override for per-session reinitialization when Domain Reload is disabled.
        /// </summary>
        protected virtual void OnPlaySessionStart()
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
            instance.InitializeForCurrentPlaySessionIfNeeded();
            SingletonLogger.LogWarning<T>(message: "Auto-created.", context: instance);

            return instance;
        }

        private static T AsExactType(T candidate, string callerContext)
        {
            if (candidate == null) return null;
            if (candidate.GetType() == typeof(T)) return candidate;

            SingletonLogger.LogError<T>(message: $"Type mismatch found via '{callerContext}'.\nExpected EXACT type '{typeof(T).Name}', but found '{candidate.GetType().Name}'.", context: candidate);

            if (Application.isPlaying)
            {
                Destroy(obj: candidate.gameObject);
            }

            return null;
        }

        [System.Diagnostics.Conditional(conditionString: "UNITY_EDITOR"), System.Diagnostics.Conditional(conditionString: "DEVELOPMENT_BUILD")]
        private static void ThrowIfInactiveInstanceExists()
        {
            var allInstances = FindObjectsByType<T>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);

            foreach (var instance in allInstances)
            {
                if (!instance.isActiveAndEnabled)
                {
                    SingletonLogger.ThrowInvalidOperation<T>(message: $"Auto-create BLOCKED: inactive instance exists ('{instance.name}', type: '{instance.GetType().Name}').\nEnable it or remove from scene.");
                }
            }
        }

        private static void InvalidateInstanceCacheIfPlaySessionChanged()
        {
            if (!Application.isPlaying) return;

            SingletonRuntime.EnsureInitializedForCurrentPlaySession();

            int current = SingletonRuntime.PlaySessionId;
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

            int currentPlaySessionId = SingletonRuntime.PlaySessionId;
            if (this._initializedPlaySessionId == currentPlaySessionId) return;

            this.EnsurePersistent();
            this._initializedPlaySessionId = currentPlaySessionId;
            this.OnPlaySessionStart();
        }

        private bool TryEstablishAsInstance()
        {
            if (HasCachedInstance)
            {
                if (ReferenceEquals(objA: _instance, objB: this)) return true;

                SingletonLogger.LogWarning<T>(message: $"Duplicate detected. Existing='{_instance.name}', destroying '{this.name}'.", context: this);
                Destroy(obj: this.gameObject);
                return false;
            }

            if (this.GetType() != typeof(T))
            {
                SingletonLogger.LogError<T>(message: $"Type mismatch. Expected='{typeof(T).Name}', Actual='{this.GetType().Name}', destroying '{this.name}'.", context: this);
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
                SingletonLogger.LogWarning<T>(message: "Reparented to root for DontDestroyOnLoad.", context: this);
                this.transform.SetParent(parent: null, worldPositionStays: true);
            }

            DontDestroyOnLoad(target: this.gameObject);
            this._isPersistent = true;
        }
    }
}
