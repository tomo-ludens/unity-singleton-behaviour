using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Singletons.Tests.Runtime
{
    public sealed class TestPersistentSingleton : GlobalSingleton<TestPersistentSingleton>
    {
        public int AwakeCount { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AwakeCount++;
        }
    }

    public sealed class TestSceneSingleton : SceneSingleton<TestSceneSingleton>
    {
        public bool WasInitialized { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            WasInitialized = true;
        }
    }

    // For type mismatch tests - a base class that is NOT sealed
    public class TestBaseSingleton : GlobalSingleton<TestBaseSingleton>
    {
    }

    // Derived class that should be rejected
    public sealed class TestDerivedSingleton : TestBaseSingleton
    {
    }

    // For inactive instance tests
    public sealed class TestInactiveSingleton : GlobalSingleton<TestInactiveSingleton>
    {
    }

    public sealed class TestSoftResetSingleton : GlobalSingleton<TestSoftResetSingleton>
    {
        private int _awakeCalls;
        private int _playSessionStartCalls;

        public int AwakeCalls => _awakeCalls;
        public int PlaySessionStartCalls => _playSessionStartCalls;

        protected override void Awake()
        {
            base.Awake();
            _awakeCalls++;
        }

        protected override void OnPlaySessionStart()
        {
            _playSessionStartCalls++;
        }
    }

    [TestFixture]
    public class PersistentSingletonTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_AutoCreates_WhenNotExists()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Instance should be auto-created");
            Assert.AreEqual(expected: 1, actual: instance.AwakeCount, message: "Awake should be called once");
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsSameInstance_OnMultipleAccess()
        {
            var first = TestPersistentSingleton.Instance;
            yield return null;

            var second = TestPersistentSingleton.Instance;
            yield return null;

            Assert.AreSame(expected: first, actual: second, message: "Multiple accesses should return the same instance");
            Assert.AreEqual(expected: 1, actual: first.AwakeCount, message: "Awake should be called only once");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenNotExists()
        {
            bool result = TestPersistentSingleton.TryGetInstance(instance: out var instance);
            yield return null;

            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when no instance exists");
            Assert.IsNull(anObject: instance, message: "Out parameter should be null");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsTrue_WhenExists()
        {
            var created = TestPersistentSingleton.Instance;
            yield return null;

            bool result = TestPersistentSingleton.TryGetInstance(instance: out var instance);

            Assert.IsTrue(condition: result, message: "TryGetInstance should return true when instance exists");
            Assert.AreSame(expected: created, actual: instance, message: "Should return the same instance");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_DoesNotAutoCreate()
        {
            TestPersistentSingleton.TryGetInstance(instance: out _);
            yield return null;

            bool exists = TestPersistentSingleton.TryGetInstance(instance: out _);

            Assert.IsFalse(condition: exists, message: "TryGetInstance should not auto-create");
        }



        [UnityTest]
        public IEnumerator Duplicate_IsDestroyed()
        {
            var first = TestPersistentSingleton.Instance;
            yield return null;

            var duplicateGo = new GameObject(name: "Duplicate");
            duplicateGo.AddComponent<TestPersistentSingleton>();
            yield return null;

            Assert.AreSame(expected: first, actual: TestPersistentSingleton.Instance, message: "Original instance should remain");
            Assert.AreEqual(expected: 1, actual: Object.FindObjectsByType<TestPersistentSingleton>(sortMode: FindObjectsSortMode.None).Length, message: "Only one instance should exist");
        }


        [UnityTest]
        public IEnumerator Instance_HasDontDestroyOnLoad()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsTrue(
                condition: instance.gameObject.scene.name == "DontDestroyOnLoad" || instance.gameObject.scene.buildIndex == -1,
                message: "Persistent singleton should be in DontDestroyOnLoad scene"
                );
        }
    }

    public class SoftResetTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSoftResetSingleton.TryGetInstance(instance: out var instance))
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestSoftResetSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Reinitializes_PerPlaySession_WhenPlaySessionIdChanges()
        {
            var instance = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance);
            Assert.AreEqual(expected: 1, actual: instance.AwakeCalls, message: "Awake should run once per GameObject lifetime");
            Assert.AreEqual(expected: 1, actual: instance.PlaySessionStartCalls, message: "OnPlaySessionStart should run on first establishment");

            // Simulate new Play session boundary (Domain Reload disabled scenario)
            TestExtensions.AdvancePlaySessionIdForTesting();

            var sameInstance = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreSame(expected: instance, actual: sameInstance, message: "Instance should be re-used (not recreated) across play session boundary");
            Assert.AreEqual(expected: 1, actual: sameInstance.AwakeCalls, message: "Awake should not be re-run on play session boundary");
            Assert.AreEqual(expected: 2, actual: sameInstance.PlaySessionStartCalls, message: "OnPlaySessionStart should run once per Play session");
            Assert.AreEqual(
                expected: 1,
                actual: Object.FindObjectsByType<TestSoftResetSingleton>(sortMode: FindObjectsSortMode.None).Length,
                message: "Only one instance should exist"
            );
        }
    }

    [TestFixture]
    public class SceneSingletonTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSceneSingleton.TryGetInstance(instance: out var instance))
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestSceneSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsPlacedInstance()
        {
            var go = new GameObject(name: "TestSceneSingleton");
            var placed = go.AddComponent<TestSceneSingleton>();
            yield return null;

            var instance = TestSceneSingleton.Instance;

            Assert.AreSame(expected: placed, actual: instance, message: "Should return the placed instance");
            Assert.IsTrue(condition: instance.WasInitialized, message: "Awake should be called");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenNotPlaced()
        {
            bool result = TestSceneSingleton.TryGetInstance(instance: out var instance);
            yield return null;

            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when not placed");
            Assert.IsNull(anObject: instance, message: "Instance should be null");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsTrue_WhenPlaced()
        {
            var go = new GameObject(name: "TestSceneSingleton");
            go.AddComponent<TestSceneSingleton>();
            yield return null;

            bool result = TestSceneSingleton.TryGetInstance(instance: out var instance);

            Assert.IsTrue(condition: result, message: "TryGetInstance should return true when placed");
            Assert.IsNotNull(anObject: instance, message: "Instance should not be null");
        }

        [UnityTest]
        public IEnumerator SceneSingleton_DoesNotHaveDontDestroyOnLoad()
        {
            var go = new GameObject(name: "TestSceneSingleton");
            go.AddComponent<TestSceneSingleton>();
            yield return null;

            var instance = TestSceneSingleton.Instance;

            Assert.AreNotEqual(expected: "DontDestroyOnLoad", actual: instance.gameObject.scene.name, message: "Scene singleton should NOT be in DontDestroyOnLoad");
        }

        [UnityTest]
        public IEnumerator Duplicate_IsDestroyed()
        {
            var go1 = new GameObject(name: "First");
            var first = go1.AddComponent<TestSceneSingleton>();
            yield return null;

            var go2 = new GameObject(name: "Second");
            go2.AddComponent<TestSceneSingleton>();
            yield return null;

            Assert.AreSame(expected: first, actual: TestSceneSingleton.Instance, message: "First instance should remain");
            Assert.AreEqual(expected: 1, actual: Object.FindObjectsByType<TestSceneSingleton>(sortMode: FindObjectsSortMode.None).Length, message: "Only one instance should exist");
        }
    }


    [TestFixture]
    public class InactiveInstanceTests
    {
        [TearDown]
        public void TearDown()
        {
            var allInstances = Object.FindObjectsByType<TestInactiveSingleton>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);

            foreach (var instance in allInstances)
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestInactiveSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_ThrowsInEditor_WhenInactiveInstanceExists()
        {
            // Create GO, add component (Awake runs but doesn't cache yet), then set inactive
            var go = new GameObject(name: "InactiveSingleton");
            go.AddComponent<TestInactiveSingleton>();
            go.SetActive(value: false);

            // Reset cache to force fresh search
            default(TestInactiveSingleton).ResetStaticCacheForTesting();
            yield return null;

            // Singleton behavior: ThrowIfInactiveInstanceExists() checks with Include
            // and BLOCKS auto-creation if inactive instance exists (fail-fast for devs)
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<System.InvalidOperationException>(() => { _ = TestInactiveSingleton.Instance; }, message: "Should throw when inactive instance exists - auto-create blocked");
            #else
            // In release build, ThrowIfInactiveInstanceExists is stripped (Conditional attribute)
            var instance = TestInactiveSingleton.Instance;
            Assert.IsNotNull(instance, "Should auto-create in release build");
            #endif
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenOnlyInactiveInstanceExists()
        {
            var go = new GameObject(name: "InactiveSingleton");
            go.AddComponent<TestInactiveSingleton>();
            go.SetActive(value: false);

            // Reset cache to force fresh search
            default(TestInactiveSingleton).ResetStaticCacheForTesting();
            yield return null;

            // With Exclude policy, inactive GO is not found
            bool result = TestInactiveSingleton.TryGetInstance(instance: out var instance);

            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when only inactive exists");
            Assert.IsNull(anObject: instance, message: "Instance should be null");
        }

        [UnityTest]
        public IEnumerator DisabledComponent_ThrowsInEditor()
        {
            // Disabled component (enabled=false) but active GameObject IS found
            var go = new GameObject(name: "DisabledComponent");
            var comp = go.AddComponent<TestInactiveSingleton>();
            yield return null; // Let Awake run first

            // Disable the component after initialization
            comp.enabled = false;
            default(TestInactiveSingleton).ResetStaticCacheForTesting();
            yield return null;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<System.InvalidOperationException>(() => { _ = TestInactiveSingleton.Instance; }, message: "Should throw when component is disabled in DEV/EDITOR");
            #else
            var instance = TestInactiveSingleton.Instance;
            Assert.IsNull(instance, "Should return null in release build");
            #endif
        }
    }

    [TestFixture]
    public class TypeMismatchTests
    {
        [TearDown]
        public void TearDown()
        {
            var baseInstances = Object.FindObjectsByType<TestBaseSingleton>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);

            foreach (var instance in baseInstances)
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestBaseSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator DerivedClass_IsRejected_AndDestroyed()
        {
            // Expect the error log BEFORE the action that triggers it
            // Log format: "[TypeTag] Type mismatch..."
            LogAssert.Expect(type: LogType.Error, message: new System.Text.RegularExpressions.Regex(pattern: @".*Type mismatch.*"));

            var go = new GameObject(name: "DerivedSingleton");
            go.AddComponent<TestDerivedSingleton>(); // Error logged in Awake
            yield return null;

            // Access via base class - derived instance was rejected, so auto-create
            var instance = TestBaseSingleton.Instance;
            yield return null;

            // The derived instance should be destroyed, and a new base instance auto-created
            Assert.IsNotNull(anObject: instance, message: "Should auto-create correct type");
            Assert.AreEqual(expected: typeof(TestBaseSingleton), actual: instance.GetType(), message: "Instance should be exact type, not derived");
        }

        [UnityTest]
        public IEnumerator BaseClass_IsAccepted()
        {
            var go = new GameObject(name: "BaseSingleton");
            var placed = go.AddComponent<TestBaseSingleton>();
            yield return null;

            var instance = TestBaseSingleton.Instance;

            Assert.AreSame(expected: placed, actual: instance, message: "Base class instance should be accepted");
            Assert.AreEqual(expected: typeof(TestBaseSingleton), actual: instance.GetType());
        }
    }

    [TestFixture]
    public class ThreadSafetyTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator BackgroundThread_Instance_ReturnsNull()
        {
            // First, ensure main thread ID is captured by accessing from main thread
            _ = TestPersistentSingleton.Instance;
            yield return null;

            // Expect error log from ValidateMainThread
            LogAssert.Expect(type: LogType.Error, message: new System.Text.RegularExpressions.Regex(pattern: "must be called from the main thread"));

            // Background thread access should return null (not throw)
            TestPersistentSingleton backgroundResult = null;
            bool threadCompleted = false;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    backgroundResult = TestPersistentSingleton.Instance;
                }
                finally
                {
                    threadCompleted = true;
                }
            });

            thread.Start();

            while (!threadCompleted)
            {
                yield return null;
            }

            Assert.IsNull(anObject: backgroundResult, message: "Instance should return null from background thread");
        }

        [UnityTest]
        public IEnumerator BackgroundThread_TryGetInstance_ReturnsFalse()
        {
            _ = TestPersistentSingleton.Instance;
            yield return null;

            // Expect error log from ValidateMainThread
            LogAssert.Expect(type: LogType.Error, message: new System.Text.RegularExpressions.Regex(pattern: "must be called from the main thread"));

            bool tryGetResult = true;
            TestPersistentSingleton backgroundInstance = null;
            bool threadCompleted = false;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    tryGetResult = TestPersistentSingleton.TryGetInstance(instance: out backgroundInstance);
                }
                finally
                {
                    threadCompleted = true;
                }
            });

            thread.Start();

            while (!threadCompleted)
            {
                yield return null;
            }

            Assert.IsFalse(condition: tryGetResult, message: "TryGetInstance should return false from background thread");
            Assert.IsNull(anObject: backgroundInstance, message: "Instance should be null from background thread");
        }

        [UnityTest]
        public IEnumerator MainThread_Instance_AccessIsSafe()
        {
            // Verify that main thread access works correctly and safely
            System.Exception mainThreadException = null;
            TestPersistentSingleton instance = null;

            try
            {
                instance = TestPersistentSingleton.Instance;
            }
            catch (System.Exception ex)
            {
                mainThreadException = ex;
            }

            Assert.IsNull(anObject: mainThreadException, message: "No exception should be thrown on main thread");
            Assert.IsNotNull(anObject: instance, message: "Instance should be created successfully on main thread");
            Assert.IsInstanceOf<TestPersistentSingleton>(actual: instance, message: "Should return correct type");

            yield return null;
        }

        [UnityTest]
        public IEnumerator MainThread_TryGetInstance_AccessIsSafe()
        {
            // Verify TryGetInstance works safely on main thread

            // First create an instance
            var createdInstance = TestPersistentSingleton.Instance;
            yield return null;

            // Now TryGetInstance should find it safely on main thread
            bool result = TestPersistentSingleton.TryGetInstance(instance: out var instance);

            Assert.IsTrue(condition: result, message: "TryGetInstance should return true after creation");
            Assert.IsNotNull(anObject: instance, message: "Instance should be retrieved successfully");
            Assert.AreSame(expected: createdInstance, actual: instance, message: "Should return the same instance");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreadSafety_Isolation_BetweenOperations()
        {
            // Test that main thread operations work correctly and are isolated from thread safety issues
            var firstInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: firstInstance, message: "Should create instance on main thread");

            // Multiple Instance calls should be safe and return same instance
            for (int i = 0; i < 5; i++)
            {
                var instance = TestPersistentSingleton.Instance;
                Assert.AreSame(expected: firstInstance, actual: instance, message: $"Instance call {i} should return same instance on main thread");
                yield return null;
            }

            // TryGetInstance should also work safely
            bool tryResult = TestPersistentSingleton.TryGetInstance(instance: out var tryInstance);
            Assert.IsTrue(condition: tryResult, message: "TryGetInstance should succeed on main thread");
            Assert.AreSame(expected: firstInstance, actual: tryInstance, message: "TryGetInstance should return same instance");
        }

        [UnityTest]
        public IEnumerator ThreadSafety_ValidationLayer_PreventsBackgroundAccess()
        {
            // First, ensure main thread ID is captured
            _ = TestPersistentSingleton.Instance;
            yield return null;

            // Expect error log from ValidateMainThread
            LogAssert.Expect(type: LogType.Error, message: new System.Text.RegularExpressions.Regex(pattern: "must be called from the main thread"));

            // Test that the thread safety validation layer properly prevents background thread access
            TestPersistentSingleton backgroundResult = null;
            bool threadCompleted = false;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    // This should trigger the main thread validation and return null
                    backgroundResult = TestPersistentSingleton.Instance;
                }
                finally
                {
                    threadCompleted = true;
                }
            });

            thread.Start();

            while (!threadCompleted)
            {
                yield return null;
            }

            Assert.IsNull(anObject: backgroundResult, message: "Background thread access should return null");
        }

        [Test]
        public void ThreadSafety_MainThreadValidation_DoesNotInterfere()
        {
            // Test that main thread validation doesn't interfere with normal operations
            Assert.DoesNotThrow(() =>
            {
                var instance = TestPersistentSingleton.Instance;
                Assert.IsNotNull(anObject: instance, message: "Instance creation should work on main thread");
            }, message: "Instance access should not throw on main thread");

            Assert.DoesNotThrow(() =>
            {
                bool result = TestPersistentSingleton.TryGetInstance(instance: out var instance);
                Assert.IsTrue(condition: result, message: "TryGetInstance should succeed");
                Assert.IsNotNull(anObject: instance, message: "Instance should be retrieved");
            }, message: "TryGetInstance should not throw on main thread");
        }
    }

    [TestFixture]
    public class LifecycleTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator OnDestroy_CleansUpInstance()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Object.DestroyImmediate(obj: instance.gameObject);
            yield return null;

            // After destruction, TryGetInstance should return false
            bool exists = TestPersistentSingleton.TryGetInstance(instance: out _);
            Assert.IsFalse(condition: exists, message: "Instance should not exist after destruction");
        }

        [UnityTest]
        public IEnumerator Instance_CanBeRecreated_AfterDestruction()
        {
            var first = TestPersistentSingleton.Instance;
            yield return null;

            Object.DestroyImmediate(obj: first.gameObject);
            default(TestPersistentSingleton).ResetStaticCacheForTesting();
            yield return null;

            var second = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: second, message: "New instance should be created");
            Assert.AreNotSame(expected: first, actual: second, message: "Should be a different instance");
            Assert.AreEqual(expected: 1, actual: second.AwakeCount, message: "New instance should have fresh AwakeCount");
        }

    }

    [TestFixture]
    public class SceneSingletonEdgeCaseTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSceneSingleton.TryGetInstance(instance: out var instance))
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestSceneSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_ThrowsInEditor_WhenNotPlaced()
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<System.InvalidOperationException>(() => { _ = TestSceneSingleton.Instance; }, message: "Should throw when SceneSingleton is not placed");
            #else
            var instance = TestSceneSingleton.Instance;
            Assert.IsNull(instance, "Should return null in release build");
            #endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneSingleton_DoesNotAutoCreate()
        {
            TestSceneSingleton.TryGetInstance(instance: out var before);
            yield return null;

            // Even after accessing, it should not exist
            TestSceneSingleton.TryGetInstance(instance: out var after);

            Assert.IsNull(anObject: before, message: "Should not exist before");
            Assert.IsNull(anObject: after, message: "Should still not exist - no auto-creation");
        }
    }

    /// <summary>
    /// GameManager-like persistent singleton for practical usage testing
    /// </summary>
    public sealed class GameManager : GlobalSingleton<GameManager>
    {
        public int PlayerScore { get; private set; }
        public string CurrentLevel { get; private set; }
        public bool IsGamePaused { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            PlayerScore = 0;
            CurrentLevel = "MainMenu";
            IsGamePaused = false;
        }

        public void AddScore(int points)
        {
            PlayerScore += points;
        }

        public void SetLevel(string levelName)
        {
            CurrentLevel = levelName;
        }

        public void PauseGame()
        {
            IsGamePaused = true;
        }

        public void ResumeGame()
        {
            IsGamePaused = false;
        }
    }

    /// <summary>
    /// LevelController-like scene singleton for practical usage testing
    /// </summary>
    public sealed class LevelController : SceneSingleton<LevelController>
    {
        public string LevelName { get; private set; }
        public int EnemyCount { get; private set; }
        public bool IsLevelComplete { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            LevelName = "DefaultLevel";
            EnemyCount = 5;
            IsLevelComplete = false;
        }

        public void SetLevelInfo(string levelName, int enemies)
        {
            LevelName = levelName;
            EnemyCount = enemies;
        }

        public void CompleteLevel()
        {
            IsLevelComplete = true;
        }
    }

    [TestFixture]
    public class PolicyBehaviorTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up any created instances
            if (TestPersistentSingleton.TryGetInstance(instance: out var persistent))
            {
                Object.DestroyImmediate(obj: persistent.gameObject);
            }
            if (TestSceneSingleton.TryGetInstance(instance: out var scene))
            {
                Object.DestroyImmediate(obj: scene.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
            default(TestSceneSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator PersistentPolicy_EnablesAutoCreation()
        {
            // Access Instance - should auto-create due to PersistentPolicy
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "PersistentPolicy should enable auto-creation");
            Assert.AreEqual(expected: "TestPersistentSingleton", actual: instance.gameObject.name, message: "Auto-created object should have correct name");
        }

        [UnityTest]
        public IEnumerator ScenePolicy_DisablesAutoCreation()
        {
            // Try to access Instance when not placed - should throw exception due to ScenePolicy
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<System.InvalidOperationException>(() => { _ = TestSceneSingleton.Instance; },
                message: "ScenePolicy should throw exception when not placed (DEV/EDITOR)");
#else
            var instance = TestSceneSingleton.Instance;
            Assert.IsNull(instance, message: "ScenePolicy should return null when not placed (Release)");
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator PersistentSingleton_SurvivesDontDestroyOnLoad()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            // Check if DontDestroyOnLoad was applied
            Assert.IsTrue(
                condition: instance.gameObject.scene.name == "DontDestroyOnLoad" || instance.transform.parent == null,
                message: "Persistent singleton should be in DontDestroyOnLoad scene or root"
            );

            // Verify it's still accessible after scene operations
            var sameInstance = TestPersistentSingleton.Instance;
            Assert.AreSame(expected: instance, actual: sameInstance, message: "Should return the same instance after DontDestroyOnLoad");
        }
    }

    [TestFixture]
    public class ErrorRecoveryTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up any created instances
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }
            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

    }

    [TestFixture]
    public class ResourceManagementTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up any created instances
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                Object.DestroyImmediate(obj: instance.gameObject);
            }
            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Singleton_CanBeDestroyed_Safely()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Should create instance");

            // Destroy the instance
            Object.DestroyImmediate(obj: instance.gameObject);
            yield return null;

            // Access again - should create new instance
            var newInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: newInstance, message: "Should create new instance after destruction");
            Assert.AreNotSame(expected: instance, actual: newInstance, message: "Should be different instances");
        }

        [UnityTest]
        public IEnumerator DestroyedInstance_DoesNotAffectNewInstance()
        {
            var firstInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: firstInstance, message: "Should create first instance");

            // Destroy first instance while keeping reference
            Object.DestroyImmediate(obj: firstInstance.gameObject);
            yield return null;

            // Create new instance
            var secondInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: secondInstance, message: "Should create second instance");

            // Verify second instance is functional
            Assert.AreNotSame(expected: firstInstance, actual: secondInstance, message: "Should be different instances");
            Assert.IsFalse(condition: ReferenceEquals(objA: null, objB: secondInstance.gameObject), message: "Second instance GameObject should exist");
        }

        [UnityTest]
        public IEnumerator Instance_CanBeAccessed_DuringDestruction()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Should create instance");

            // Access while instance still exists
            var sameInstance = TestPersistentSingleton.Instance;
            Assert.AreSame(expected: instance, actual: sameInstance, message: "Should return same instance while alive");

            // Destroy and verify TryGetInstance behavior
            Object.DestroyImmediate(obj: instance.gameObject);
            yield return null;

            bool result = TestPersistentSingleton.TryGetInstance(instance: out var retrieved);
            Assert.IsFalse(condition: result, message: "TryGetInstance should return false after destruction");
            Assert.IsNull(anObject: retrieved, message: "Retrieved instance should be null after destruction");
        }
    }

    [TestFixture]
    public class PracticalUsageTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up GameManager
            if (GameManager.TryGetInstance(instance: out var gm))
            {
                Object.DestroyImmediate(obj: gm.gameObject);
            }
            default(GameManager).ResetStaticCacheForTesting();

            // Clean up LevelController
            if (LevelController.TryGetInstance(instance: out var lc))
            {
                Object.DestroyImmediate(obj: lc.gameObject);
            }
            default(LevelController).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator GameManager_PersistsAcrossAccess()
        {
            // First access should create instance
            var gm1 = GameManager.Instance;
            yield return null;

            Assert.IsNotNull(anObject: gm1, message: "GameManager should be created");
            Assert.AreEqual(expected: 0, actual: gm1.PlayerScore, message: "Initial score should be 0");
            Assert.AreEqual(expected: "MainMenu", actual: gm1.CurrentLevel, message: "Initial level should be MainMenu");

            // Modify state
            gm1.AddScore(points: 100);
            gm1.SetLevel(levelName: "Level1");
            gm1.PauseGame();

            // Second access should return same instance
            var gm2 = GameManager.Instance;
            yield return null;

            Assert.AreSame(expected: gm1, actual: gm2, message: "Should return same instance");
            Assert.AreEqual(expected: 100, actual: gm2.PlayerScore, message: "Score should persist");
            Assert.AreEqual(expected: "Level1", actual: gm2.CurrentLevel, message: "Level should persist");
            Assert.IsTrue(condition: gm2.IsGamePaused, message: "Pause state should persist");
        }

        [UnityTest]
        public IEnumerator LevelController_RequiresPlacement()
        {
            // Without placement, should return null
            bool result = LevelController.TryGetInstance(instance: out var controller);
            yield return null;

            Assert.IsFalse(condition: result, message: "Should return false when not placed");
            Assert.IsNull(anObject: controller, message: "Controller should be null");

            // Place in scene
            var go = new GameObject(name: "LevelController");
            var placedController = go.AddComponent<LevelController>();
            yield return null;

            // Now should work
            bool result2 = LevelController.TryGetInstance(instance: out var controller2);
            yield return null;

            Assert.IsTrue(condition: result2, message: "Should return true when placed");
            Assert.AreSame(expected: placedController, actual: controller2, message: "Should return placed instance");
            Assert.AreEqual(expected: "DefaultLevel", actual: controller2.LevelName, message: "Should be initialized");
        }

        [UnityTest]
        public IEnumerator Singleton_StateManagement_WorksCorrectly()
        {
            // Test GameManager state management
            var gm = GameManager.Instance;
            yield return null;

            // Initial state
            Assert.AreEqual(expected: 0, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "MainMenu", actual: gm.CurrentLevel);
            Assert.IsFalse(condition: gm.IsGamePaused);

            // Modify state like a real game
            gm.AddScore(points: 500);
            gm.SetLevel(levelName: "BossLevel");
            gm.PauseGame();

            // Verify state changes
            Assert.AreEqual(expected: 500, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "BossLevel", actual: gm.CurrentLevel);
            Assert.IsTrue(condition: gm.IsGamePaused);

            // Resume and continue
            gm.AddScore(points: 1000);
            gm.SetLevel(levelName: "Victory");
            gm.ResumeGame();

            Assert.AreEqual(expected: 1500, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "Victory", actual: gm.CurrentLevel);
            Assert.IsFalse(condition: gm.IsGamePaused);
        }

        [UnityTest]
        public IEnumerator SceneSingleton_LevelManagement_WorksCorrectly()
        {
            // Create and place LevelController
            var go = new GameObject(name: "LevelController");
            var controller = go.AddComponent<LevelController>();
            yield return null;

            // Initial state
            Assert.AreEqual(expected: "DefaultLevel", actual: controller.LevelName);
            Assert.AreEqual(expected: 5, actual: controller.EnemyCount);
            Assert.IsFalse(condition: controller.IsLevelComplete);

            // Simulate level progression
            controller.SetLevelInfo(levelName: "CastleLevel", enemies: 10);

            // Simulate gameplay - defeat all enemies
            controller.SetLevelInfo(levelName: "CastleLevel", enemies: 0);

            // Complete level
            controller.CompleteLevel();

            Assert.AreEqual(expected: "CastleLevel", actual: controller.LevelName);
            Assert.AreEqual(expected: 0, actual: controller.EnemyCount);
            Assert.IsTrue(condition: controller.IsLevelComplete);
        }

        [UnityTest]
        public IEnumerator Singleton_InitializationOrder_WorksCorrectly()
        {
            // Create multiple singletons to test initialization order
            var gm = GameManager.Instance;
            yield return null;

            // Place level controller
            var go = new GameObject(name: "LevelController");
            var lc = go.AddComponent<LevelController>();
            yield return null;

            // Both should be properly initialized
            Assert.IsNotNull(anObject: gm);
            Assert.IsNotNull(anObject: lc);
            Assert.AreEqual(expected: 0, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "DefaultLevel", actual: lc.LevelName);

            // Modify both
            gm.AddScore(points: 100);
            lc.SetLevelInfo(levelName: "OrderedLevel", enemies: 3);

            // Verify both maintain state
            Assert.AreEqual(expected: 100, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "OrderedLevel", actual: lc.LevelName);
            Assert.AreEqual(expected: 3, actual: lc.EnemyCount);
        }

        [UnityTest]
        public IEnumerator Singleton_ResourceManagement_WorksCorrectly()
        {
            // Test proper cleanup on destroy
            var gm = GameManager.Instance;
            yield return null;

            // Set up some state
            gm.AddScore(points: 999);
            gm.SetLevel(levelName: "FinalLevel");

            // Destroy instance (simulate scene unload for persistent singleton)
            Object.DestroyImmediate(obj: gm.gameObject);
            yield return null;

            // Next access should create new instance with fresh state
            var gm2 = GameManager.Instance;
            yield return null;

            Assert.IsNotNull(anObject: gm2);
            Assert.AreNotSame(expected: gm, actual: gm2, message: "Should be different instance");
            Assert.AreEqual(expected: 0, actual: gm2.PlayerScore, message: "Should have fresh score");
            Assert.AreEqual(expected: "MainMenu", actual: gm2.CurrentLevel, message: "Should have fresh level");
        }
    }

    /// <summary>
    /// Tests for Domain Reload disabled scenarios.
    /// These tests verify that singleton infrastructure properly handles
    /// Play session boundaries when Domain Reload is disabled.
    /// </summary>
    [TestFixture]
    public class DomainReloadTests
    {
        [TearDown]
        public void TearDown()
        {
            // Reset quitting flag FIRST so TryGetInstance works
            TestExtensions.ResetQuittingFlagForTesting();

            // Clean up instances directly to avoid TryGetInstance issues
            var softResetInstances = Object.FindObjectsByType<TestSoftResetSingleton>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);
            foreach (var inst in softResetInstances)
            {
                Object.DestroyImmediate(obj: inst.gameObject);
            }

            var persistentInstances = Object.FindObjectsByType<TestPersistentSingleton>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);
            foreach (var inst in persistentInstances)
            {
                Object.DestroyImmediate(obj: inst.gameObject);
            }

            default(TestSoftResetSingleton).ResetStaticCacheForTesting();
            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator StaticCache_IsInvalidated_WhenPlaySessionIdChanges()
        {
            // Create and cache instance
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Instance should be created");

            // Simulate Play session boundary (Domain Reload disabled scenario)
            TestExtensions.AdvancePlaySessionIdForTesting();

            // Access again - cache should be invalidated but same GameObject reused
            var sameInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.AreSame(expected: instance, actual: sameInstance, message: "Should return same GameObject instance");
        }

        [UnityTest]
        public IEnumerator OnPlaySessionStart_IsCalledAgain_AfterPlaySessionBoundary()
        {
            var instance = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreEqual(expected: 1, actual: instance.PlaySessionStartCalls, message: "OnPlaySessionStart should be called once initially");

            // Simulate multiple Play session boundaries
            TestExtensions.AdvancePlaySessionIdForTesting();
            _ = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreEqual(expected: 2, actual: instance.PlaySessionStartCalls, message: "OnPlaySessionStart should be called again after boundary");

            TestExtensions.AdvancePlaySessionIdForTesting();
            _ = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreEqual(expected: 3, actual: instance.PlaySessionStartCalls, message: "OnPlaySessionStart should be called for each Play session");
        }

        [UnityTest]
        public IEnumerator AwakeCount_DoesNotIncrease_OnPlaySessionBoundary()
        {
            var instance = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreEqual(expected: 1, actual: instance.AwakeCalls, message: "Awake should be called once on creation");

            // Cross multiple Play session boundaries
            for (int i = 0; i < 3; i++)
            {
                TestExtensions.AdvancePlaySessionIdForTesting();
                _ = TestSoftResetSingleton.Instance;
                yield return null;
            }

            Assert.AreEqual(expected: 1, actual: instance.AwakeCalls, message: "Awake should NOT be called again on Play session boundary");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_WorksCorrectly_AcrossPlaySessionBoundary()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            bool resultBefore = TestPersistentSingleton.TryGetInstance(instance: out var retrieved1);
            Assert.IsTrue(condition: resultBefore, message: "Should find instance before boundary");
            Assert.AreSame(expected: instance, actual: retrieved1);

            // Cross Play session boundary
            TestExtensions.AdvancePlaySessionIdForTesting();

            bool resultAfter = TestPersistentSingleton.TryGetInstance(instance: out var retrieved2);
            Assert.IsTrue(condition: resultAfter, message: "Should still find instance after boundary");
            Assert.AreSame(expected: instance, actual: retrieved2, message: "Should return same instance");
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsNull_WhenQuitting()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Instance should exist before quitting");

            // Simulate quitting
            Core.SingletonRuntime.NotifyQuitting();

            // TryGetInstance should return false during quit
            bool result = TestPersistentSingleton.TryGetInstance(instance: out var retrieved);
            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when quitting");
            Assert.IsNull(anObject: retrieved, message: "Retrieved instance should be null when quitting");
        }

        [UnityTest]
        public IEnumerator NewInstance_IsNotCreated_WhenQuitting()
        {
            // Simulate quitting before any instance exists
            Core.SingletonRuntime.NotifyQuitting();

            // Try to access Instance - should return null, not create new
            var instance = TestPersistentSingleton.Instance;

            Assert.IsNull(anObject: instance, message: "Should not create instance when quitting");

            // Verify no instance was created
            var found = Object.FindAnyObjectByType<TestPersistentSingleton>();
            Assert.IsNull(anObject: found, message: "No instance should exist in scene");

            yield return null;
        }
    }

    /// <summary>
    /// Tests for singleton behavior with parent GameObjects.
    /// </summary>
    [TestFixture]
    public class ParentHierarchyTests
    {
        [TearDown]
        public void TearDown()
        {
            var allObjects = Object.FindObjectsByType<TestPersistentSingleton>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                Object.DestroyImmediate(obj: obj.gameObject);
            }

            // Clean up any parent objects
            var parents = Object.FindObjectsByType<GameObject>(sortMode: FindObjectsSortMode.None);
            foreach (var parent in parents)
            {
                if (parent.name.Contains(value: "Parent"))
                {
                    Object.DestroyImmediate(obj: parent);
                }
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator PersistentSingleton_WithParent_IsReparentedToRoot()
        {
            // Create parent-child hierarchy
            var parent = new GameObject(name: "ParentObject");
            var child = new GameObject(name: "TestPersistentSingleton");
            child.transform.SetParent(p: parent.transform);

            // Expect warning log about reparenting
            LogAssert.Expect(type: LogType.Warning, message: new System.Text.RegularExpressions.Regex(pattern: @".*Reparented.*root.*"));

            // Add singleton component
            child.AddComponent<TestPersistentSingleton>();
            yield return null;

            // Access via Instance to trigger initialization
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            // Should be reparented to root for DontDestroyOnLoad
            Assert.IsNull(anObject: instance.transform.parent, message: "Singleton should be reparented to root");
        }

        [UnityTest]
        public IEnumerator AutoCreatedSingleton_HasNoParent()
        {
            // Auto-create singleton
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNull(anObject: instance.transform.parent, message: "Auto-created singleton should have no parent");
            Assert.AreEqual(expected: "TestPersistentSingleton", actual: instance.gameObject.name, message: "Auto-created singleton should have type name");
        }
    }

    /// <summary>
    /// Tests for base.Awake() call enforcement.
    /// </summary>
    public sealed class TestSingletonWithoutBaseAwake : GlobalSingleton<TestSingletonWithoutBaseAwake>
    {
        public bool AwakeWasCalled { get; private set; }

        protected override void Awake()
        {
            // Deliberately NOT calling base.Awake() to test error detection
            AwakeWasCalled = true;
        }
    }

    [TestFixture]
    public class BaseAwakeEnforcementTests
    {
        [TearDown]
        public void TearDown()
        {
            var allObjects = Object.FindObjectsByType<TestSingletonWithoutBaseAwake>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                Object.DestroyImmediate(obj: obj.gameObject);
            }

            default(TestSingletonWithoutBaseAwake).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Singleton_LogsError_WhenBaseAwakeNotCalled()
        {
            // Expect error log about base.Awake() not being called
            LogAssert.Expect(type: LogType.Error, message: new System.Text.RegularExpressions.Regex(pattern: @".*base\.Awake\(\).*not called.*"));

            var go = new GameObject(name: "TestSingletonWithoutBaseAwake");
            var singleton = go.AddComponent<TestSingletonWithoutBaseAwake>();
            yield return null;

            // Awake was called but base.Awake() was not - OnEnable should log error
            Assert.IsTrue(condition: singleton.AwakeWasCalled, message: "Custom Awake should have been called");
        }
    }

    /// <summary>
    /// Tests for edge cases and error recovery.
    /// </summary>
    [TestFixture]
    public class EdgeCaseTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSceneSingleton.TryGetInstance(instance: out var scene))
            {
                Object.DestroyImmediate(obj: scene.gameObject);
            }

            if (TestPersistentSingleton.TryGetInstance(instance: out var persistent))
            {
                Object.DestroyImmediate(obj: persistent.gameObject);
            }

            default(TestSceneSingleton).ResetStaticCacheForTesting();
            default(TestPersistentSingleton).ResetStaticCacheForTesting();
            TestExtensions.ResetQuittingFlagForTesting();
        }

        [UnityTest]
        public IEnumerator DestroyedInstance_IsProperlyCleanedUp_FromCache()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance);

            // Destroy via DestroyImmediate
            Object.DestroyImmediate(obj: instance.gameObject);

            // Cache should be cleared
            bool exists = TestPersistentSingleton.TryGetInstance(instance: out var retrieved);
            Assert.IsFalse(condition: exists, message: "TryGetInstance should return false after destruction");
            Assert.IsNull(anObject: retrieved, message: "Retrieved instance should be null");
        }

        [UnityTest]
        public IEnumerator MultipleRapidAccesses_ReturnSameInstance()
        {
            TestPersistentSingleton[] instances = new TestPersistentSingleton[10];

            for (int i = 0; i < 10; i++)
            {
                instances[i] = TestPersistentSingleton.Instance;
            }

            yield return null;

            for (int i = 1; i < 10; i++)
            {
                Assert.AreSame(expected: instances[0], actual: instances[i], message: $"Access {i} should return same instance");
            }

            Assert.AreEqual(expected: 1, actual: Object.FindObjectsByType<TestPersistentSingleton>(sortMode: FindObjectsSortMode.None).Length, message: "Only one instance should exist");
        }

        [UnityTest]
        public IEnumerator SceneSingleton_AccessBeforePlacement_ThenPlacement_Works()
        {
            // Try to access before placement
            bool beforeResult = TestSceneSingleton.TryGetInstance(instance: out var before);
            Assert.IsFalse(condition: beforeResult, message: "Should not find instance before placement");
            Assert.IsNull(anObject: before);

            yield return null;

            // Now place instance
            var go = new GameObject(name: "TestSceneSingleton");
            var placed = go.AddComponent<TestSceneSingleton>();
            yield return null;

            // Should now find it
            bool afterResult = TestSceneSingleton.TryGetInstance(instance: out var after);
            Assert.IsTrue(condition: afterResult, message: "Should find instance after placement");
            Assert.AreSame(expected: placed, actual: after);
        }
    }
}
