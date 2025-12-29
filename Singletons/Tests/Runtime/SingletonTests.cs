using System.Collections;
using NUnit.Framework;
using Singletons.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Singletons.Tests.Runtime
{
    #region Test Singleton Classes

    public sealed class TestPersistentSingleton : PersistentSingletonBehaviour<TestPersistentSingleton>
    {
        public int AwakeCount { get; private set; }

        protected override void OnSingletonAwake()
        {
            AwakeCount++;
        }
    }

    public sealed class TestSceneSingleton : SceneSingletonBehaviour<TestSceneSingleton>
    {
        public bool WasInitialized { get; private set; }

        protected override void OnSingletonAwake()
        {
            WasInitialized = true;
        }
    }

    // For type mismatch tests - a base class that is NOT sealed
    public class TestBaseSingleton : PersistentSingletonBehaviour<TestBaseSingleton>
    {
    }

    // Derived class that should be rejected
    public sealed class TestDerivedSingleton : TestBaseSingleton
    {
    }

    // For inactive instance tests
    public sealed class TestInactiveSingleton : PersistentSingletonBehaviour<TestInactiveSingleton>
    {
    }

    #endregion

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

            TestPersistentSingleton.ResetStaticCacheForTesting();
            SingletonRuntime.ResetQuittingFlagForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_AutoCreates_WhenNotExists()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Instance should be auto-created");
            Assert.AreEqual(expected: 1, actual: instance.AwakeCount, message: "OnSingletonAwake should be called once");
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsSameInstance_OnMultipleAccess()
        {
            var first = TestPersistentSingleton.Instance;
            yield return null;

            var second = TestPersistentSingleton.Instance;
            yield return null;

            Assert.AreSame(expected: first, actual: second, message: "Multiple accesses should return the same instance");
            Assert.AreEqual(expected: 1, actual: first.AwakeCount, message: "OnSingletonAwake should be called only once");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenNotExists()
        {
            var result = TestPersistentSingleton.TryGetInstance(instance: out var instance);
            yield return null;

            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when no instance exists");
            Assert.IsNull(anObject: instance, message: "Out parameter should be null");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsTrue_WhenExists()
        {
            var created = TestPersistentSingleton.Instance;
            yield return null;

            var result = TestPersistentSingleton.TryGetInstance(instance: out var instance);

            Assert.IsTrue(condition: result, message: "TryGetInstance should return true when instance exists");
            Assert.AreSame(expected: created, actual: instance, message: "Should return the same instance");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_DoesNotAutoCreate()
        {
            TestPersistentSingleton.TryGetInstance(instance: out _);
            yield return null;

            var exists = TestPersistentSingleton.TryGetInstance(instance: out _);

            Assert.IsFalse(condition: exists, message: "TryGetInstance should not auto-create");
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsNull_WhenQuitting()
        {
            var _ = TestPersistentSingleton.Instance;
            yield return null;

            SingletonRuntime.SimulateQuittingForTesting();
            TestPersistentSingleton.ResetStaticCacheForTesting();

            var instance = TestPersistentSingleton.Instance;

            Assert.IsNull(anObject: instance, message: "Instance should return null during quit");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenQuitting()
        {
            var _ = TestPersistentSingleton.Instance;
            yield return null;

            SingletonRuntime.SimulateQuittingForTesting();

            var result = TestPersistentSingleton.TryGetInstance(instance: out var instance);

            Assert.IsFalse(condition: result, message: "TryGetInstance should return false during quit");
            Assert.IsNull(anObject: instance, message: "Instance should be null during quit");
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
        public IEnumerator PlaySessionChange_InvalidatesCache_AndRerunsOnSingletonAwake()
        {
            var instance = TestPersistentSingleton.Instance;
            Assert.AreEqual(expected: 1, actual: instance.AwakeCount);
            yield return null;

            SingletonRuntime.AdvancePlaySessionForTesting();

            var sameInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.AreSame(expected: instance, actual: sameInstance, message: "Should return the same GameObject instance");
            Assert.AreEqual(expected: 2, actual: instance.AwakeCount, message: "OnSingletonAwake should be called again after session change");
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

            TestSceneSingleton.ResetStaticCacheForTesting();
            SingletonRuntime.ResetQuittingFlagForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsPlacedInstance()
        {
            var go = new GameObject(name: "TestSceneSingleton");
            var placed = go.AddComponent<TestSceneSingleton>();
            yield return null;

            var instance = TestSceneSingleton.Instance;

            Assert.AreSame(expected: placed, actual: instance, message: "Should return the placed instance");
            Assert.IsTrue(condition: instance.WasInitialized, message: "OnSingletonAwake should be called");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenNotPlaced()
        {
            var result = TestSceneSingleton.TryGetInstance(instance: out var instance);
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

            var result = TestSceneSingleton.TryGetInstance(instance: out var instance);

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
    public class SingletonRuntimeTests
    {
        [TearDown]
        public void TearDown()
        {
            SingletonRuntime.ResetQuittingFlagForTesting();
        }

        [Test]
        public void IsQuitting_IsFalse_Initially()
        {
            SingletonRuntime.ResetQuittingFlagForTesting();

            Assert.IsFalse(condition: SingletonRuntime.IsQuitting, message: "IsQuitting should be false initially");
        }

        [Test]
        public void SimulateQuitting_SetsIsQuittingTrue()
        {
            SingletonRuntime.SimulateQuittingForTesting();

            Assert.IsTrue(condition: SingletonRuntime.IsQuitting, message: "IsQuitting should be true after simulating quit");
        }

        [Test]
        public void AdvancePlaySession_IncrementsPlaySessionId()
        {
            var before = SingletonRuntime.PlaySessionId;

            SingletonRuntime.AdvancePlaySessionForTesting();

            Assert.AreEqual(expected: before + 1, actual: SingletonRuntime.PlaySessionId, message: "PlaySessionId should increment");
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

            TestInactiveSingleton.ResetStaticCacheForTesting();
            SingletonRuntime.ResetQuittingFlagForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_ThrowsInEditor_WhenInactiveInstanceExists()
        {
            // Create GO, add component (Awake caches it), then set inactive
            var go = new GameObject(name: "InactiveSingleton");
            go.AddComponent<TestInactiveSingleton>();
            go.SetActive(value: false);

            // Reset cache to force fresh search
            TestInactiveSingleton.ResetStaticCacheForTesting();
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
            TestInactiveSingleton.ResetStaticCacheForTesting();
            yield return null;

            // With Exclude policy, inactive GO is not found
            var result = TestInactiveSingleton.TryGetInstance(instance: out var instance);

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
            TestInactiveSingleton.ResetStaticCacheForTesting();
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

            TestBaseSingleton.ResetStaticCacheForTesting();
            SingletonRuntime.ResetQuittingFlagForTesting();
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

            TestPersistentSingleton.ResetStaticCacheForTesting();
            SingletonRuntime.ResetQuittingFlagForTesting();
        }

        [UnityTest]
        public IEnumerator BackgroundThread_ThrowsUnityException()
        {
            // Unity API (Application.isPlaying) throws when called from background thread
            System.Exception threadException = null;
            bool threadCompleted = false;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var _ = TestPersistentSingleton.Instance;
                }
                catch (System.Exception ex)
                {
                    threadException = ex;
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

            Assert.IsNotNull(anObject: threadException, message: "Should throw exception from background thread");
            Assert.IsInstanceOf<UnityException>(actual: threadException, message: "Should be UnityException for main-thread-only API");
        }

        [UnityTest]
        public IEnumerator BackgroundThread_TryGetInstance_ThrowsUnityException()
        {
            var _ = TestPersistentSingleton.Instance;
            yield return null;

            System.Exception threadException = null;
            bool threadCompleted = false;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    TestPersistentSingleton.TryGetInstance(instance: out var _);
                }
                catch (System.Exception ex)
                {
                    threadException = ex;
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

            Assert.IsNotNull(anObject: threadException, message: "Should throw exception from background thread");
            Assert.IsInstanceOf<UnityException>(actual: threadException, message: "Should be UnityException for main-thread-only API");
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

            TestPersistentSingleton.ResetStaticCacheForTesting();
            SingletonRuntime.ResetQuittingFlagForTesting();
        }

        [UnityTest]
        public IEnumerator OnSingletonDestroy_CalledWhenDestroyed()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Object.DestroyImmediate(obj: instance.gameObject);
            yield return null;

            // After destruction, TryGetInstance should return false
            var exists = TestPersistentSingleton.TryGetInstance(instance: out _);
            Assert.IsFalse(condition: exists, message: "Instance should not exist after destruction");
        }

        [UnityTest]
        public IEnumerator Instance_CanBeRecreated_AfterDestruction()
        {
            var first = TestPersistentSingleton.Instance;
            yield return null;

            Object.DestroyImmediate(obj: first.gameObject);
            TestPersistentSingleton.ResetStaticCacheForTesting();
            yield return null;

            var second = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: second, message: "New instance should be created");
            Assert.AreNotSame(expected: first, actual: second, message: "Should be a different instance");
            Assert.AreEqual(expected: 1, actual: second.AwakeCount, message: "New instance should have fresh AwakeCount");
        }

        [UnityTest]
        public IEnumerator MultiplePlaySessions_ResetState()
        {
            var instance = TestPersistentSingleton.Instance;
            Assert.AreEqual(expected: 1, actual: instance.AwakeCount);
            yield return null;

            // Simulate multiple play sessions
            for (int i = 0; i < 3; i++)
            {
                SingletonRuntime.AdvancePlaySessionForTesting();
                _ = TestPersistentSingleton.Instance;
            }

            Assert.AreEqual(expected: 4, actual: instance.AwakeCount, message: "OnSingletonAwake should be called for each session");
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

            TestSceneSingleton.ResetStaticCacheForTesting();
            SingletonRuntime.ResetQuittingFlagForTesting();
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
}
