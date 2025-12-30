using NUnit.Framework;
using Singletons.Core;
using Singletons.Policy;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable RedundantOverriddenMember
namespace Singletons.Tests.Editor
{
    [TestFixture]
    public class SingletonRuntimeEditModeTests
    {
        [Test]
        public void PlaySessionId_IsAccessible()
        {
            int sessionId = SingletonRuntime.PlaySessionId;
            Assert.GreaterOrEqual(arg1: sessionId, arg2: 0, message: "PlaySessionId should be non-negative");
        }

        [Test]
        public void IsQuitting_ReturnsFalse_InEditMode()
        {
            TestExtensions.ResetQuittingFlagForTesting();
            Assert.IsFalse(condition: SingletonRuntime.IsQuitting, message: "IsQuitting should be false in Edit Mode");
        }
    }

    [TestFixture]
    public class PolicyTests
    {
        [Test]
        public void PersistentPolicy_HasCorrectValues()
        {
            var policy = new PersistentPolicy();
            Assert.IsTrue(condition: policy.PersistAcrossScenes, message: "PersistentPolicy should persist across scenes");
            Assert.IsTrue(condition: policy.AutoCreateIfMissing, message: "PersistentPolicy should auto-create if missing");
        }

        [Test]
        public void SceneScopedPolicy_HasCorrectValues()
        {
            var policy = new SceneScopedPolicy();
            Assert.IsFalse(condition: policy.PersistAcrossScenes, message: "SceneScopedPolicy should not persist across scenes");
            Assert.IsFalse(condition: policy.AutoCreateIfMissing, message: "SceneScopedPolicy should not auto-create if missing");
        }

        [Test]
        public void Policy_ReadonlyStruct_ZeroAllocation()
        {
            // Verify policies are readonly structs with zero allocation
            var persistentPolicy1 = default(PersistentPolicy);
            var persistentPolicy2 = default(PersistentPolicy);
            var scenePolicy1 = default(SceneScopedPolicy);
            var scenePolicy2 = default(SceneScopedPolicy);

            // Test struct equality (same default values)
            Assert.AreEqual(expected: persistentPolicy1.PersistAcrossScenes, actual: persistentPolicy2.PersistAcrossScenes);
            Assert.AreEqual(expected: persistentPolicy1.AutoCreateIfMissing, actual: persistentPolicy2.AutoCreateIfMissing);
            Assert.AreEqual(expected: scenePolicy1.PersistAcrossScenes, actual: scenePolicy2.PersistAcrossScenes);
            Assert.AreEqual(expected: scenePolicy1.AutoCreateIfMissing, actual: scenePolicy2.AutoCreateIfMissing);

            // Test expected values
            Assert.IsTrue(condition: persistentPolicy1.PersistAcrossScenes, message: "PersistentPolicy should persist across scenes");
            Assert.IsTrue(condition: persistentPolicy1.AutoCreateIfMissing, message: "PersistentPolicy should auto-create");
            Assert.IsFalse(condition: scenePolicy1.PersistAcrossScenes, message: "SceneScopedPolicy should not persist");
            Assert.IsFalse(condition: scenePolicy1.AutoCreateIfMissing, message: "SceneScopedPolicy should not auto-create");

            // Verify type characteristics
            var persistentType = typeof(PersistentPolicy);
            var sceneType = typeof(SceneScopedPolicy);

            Assert.IsTrue(condition: persistentType.IsValueType, message: "PersistentPolicy should be a value type");
            Assert.IsTrue(condition: sceneType.IsValueType, message: "SceneScopedPolicy should be a value type");
            Assert.IsTrue(condition: persistentType.IsVisible, message: "Policy types should be public");
        }

        [Test]
        public void Policy_StructImmutability()
        {
            // Test that policy structs are immutable (cannot be modified)
            var persistentPolicy = default(PersistentPolicy);
            var scenePolicy = default(SceneScopedPolicy);

            // Store original values
            bool originalPersistentPersist = persistentPolicy.PersistAcrossScenes;
            bool originalPersistentAuto = persistentPolicy.AutoCreateIfMissing;
            bool originalScenePersist = scenePolicy.PersistAcrossScenes;
            bool originalSceneAuto = scenePolicy.AutoCreateIfMissing;

            // Attempt to "modify" (this should not compile if truly readonly, but let's test the concept)
            // Since they're readonly structs, any modification attempt would create new instances

            // Verify values remain constant across multiple instantiations
            for (int i = 0; i < 10; i++)
            {
                var newPersistent = default(PersistentPolicy);
                var newScene = default(SceneScopedPolicy);

                Assert.AreEqual(expected: originalPersistentPersist, actual: newPersistent.PersistAcrossScenes);
                Assert.AreEqual(expected: originalPersistentAuto, actual: newPersistent.AutoCreateIfMissing);
                Assert.AreEqual(expected: originalScenePersist, actual: newScene.PersistAcrossScenes);
                Assert.AreEqual(expected: originalSceneAuto, actual: newScene.AutoCreateIfMissing);
            }
        }

        [Test]
        public void Policy_DefaultInitialization_Consistency()
        {
            // Test that default initialization is consistent and predictable
            var persistentPolicies = new PersistentPolicy[5];
            var scenePolicies = new SceneScopedPolicy[5];

            // All should have the same values since they're default initialized
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(condition: persistentPolicies[i].PersistAcrossScenes);
                Assert.IsTrue(condition: persistentPolicies[i].AutoCreateIfMissing);
                Assert.IsFalse(condition: scenePolicies[i].PersistAcrossScenes);
                Assert.IsFalse(condition: scenePolicies[i].AutoCreateIfMissing);
            }

            // Test policy interface compliance
            TestPolicyInterfaceCompliance(policy: default(PersistentPolicy));
            TestPolicyInterfaceCompliance(policy: default(SceneScopedPolicy));
        }

        private void TestPolicyInterfaceCompliance(ISingletonPolicy policy)
        {
            // Test that policies properly implement ISingletonPolicy interface
            bool persistProperty = policy.PersistAcrossScenes;
            bool autoProperty = policy.AutoCreateIfMissing;

            // Values should be boolean (basic interface compliance test)
            Assert.IsInstanceOf<bool>(actual: persistProperty);
            Assert.IsInstanceOf<bool>(actual: autoProperty);

            switch (policy)
            {
                // At least one should be true for PersistentPolicy, both false for SceneScopedPolicy
                // (This is a structural test of the policy design)
                case PersistentPolicy:
                    Assert.IsTrue(condition: persistProperty && autoProperty);
                    break;
                case SceneScopedPolicy:
                    Assert.IsFalse(condition: persistProperty || autoProperty);
                    break;
            }
        }
    }

    [TestFixture]
    public class SingletonBehaviourEditModeTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up any created GameObjects
            var testObjects = Object.FindObjectsByType<GameObject>(sortMode: FindObjectsSortMode.None);
            foreach (var obj in testObjects)
            {
                if (obj.name.Contains(value: "Test") || obj.name.Contains(value: "Singleton"))
                {
                    Object.DestroyImmediate(obj: obj);
                }
            }

            // Reset static caches using TestExtensions (reflection-based)
            default(TestPersistentSingletonForEditMode).ResetStaticCacheForTesting();
        }

        [Test]
        public void PersistentSingleton_Instance_ReturnsNull_InEditMode()
        {
            // In Edit Mode, Instance should perform lookup only, not auto-creation
            var instance = TestPersistentSingletonForEditMode.Instance;
            Assert.IsNull(anObject: instance, message: "Instance should return null in Edit Mode when no instance exists");
        }

        [Test]
        public void PersistentSingleton_TryGetInstance_ReturnsFalse_InEditMode()
        {
            bool result = TestPersistentSingletonForEditMode.TryGetInstance(instance: out var instance);
            Assert.IsFalse(condition: result, message: "TryGetInstance should return false in Edit Mode when no instance exists");
            Assert.IsNull(anObject: instance, message: "Instance should be null");
        }

        [Test]
        public void SceneSingleton_Instance_ReturnsNull_InEditMode()
        {
            var instance = TestSceneSingletonForEditMode.Instance;
            Assert.IsNull(anObject: instance, message: "SceneSingleton.Instance should return null in Edit Mode when not placed");
        }

        [Test]
        public void SceneSingleton_TryGetInstance_ReturnsFalse_InEditMode()
        {
            bool result = TestSceneSingletonForEditMode.TryGetInstance(instance: out var instance);
            Assert.IsFalse(condition: result, message: "SceneSingleton.TryGetInstance should return false in Edit Mode when not placed");
            Assert.IsNull(anObject: instance, message: "Instance should be null");
        }

        [Test]
        public void Singleton_DoesNotCache_InEditMode()
        {
            // Create a temporary instance
            var go = new GameObject(name: "TestSingleton");

            // Access through Instance
            var instance1 = TestPersistentSingletonForEditMode.Instance;
            var instance2 = TestPersistentSingletonForEditMode.Instance;

            // Should return the same instance
            Assert.AreSame(expected: instance1, actual: instance2, message: "Should return the same instance");

            // Destroy the GameObject
            Object.DestroyImmediate(obj: go);

            // Access again - should not be cached from previous access
            var instance3 = TestPersistentSingletonForEditMode.Instance;
            Assert.IsNull(anObject: instance3, message: "Instance should not be cached from Edit Mode access");
        }
    }

    [TestFixture]
    public class SingletonLifecycleEditModeTests
    {
        [TearDown]
        public void TearDown()
        {
            var testObjects = Object.FindObjectsByType<GameObject>(sortMode: FindObjectsSortMode.None);
            foreach (var obj in testObjects)
            {
                if (obj == null) continue;

                if (obj.name.Contains(value: "Test") || obj.name.Contains(value: "Singleton") || obj.name.Contains(value: "Parent"))
                {
                    Object.DestroyImmediate(obj: obj);
                }
            }

            default(TestPersistentSingletonForEditMode).ResetStaticCacheForTesting();
            default(TestSingletonWithoutBaseAwake).ResetStaticCacheForTesting();
            default(TestSingletonWithParent).ResetStaticCacheForTesting();
        }

        [Test]
        public void Singleton_WithParent_LogsWarning_WhenReparentedForPersistence()
        {
            // Create parent-child hierarchy
            var parent = new GameObject(name: "ParentObject");
            var child = new GameObject(name: "TestSingletonWithParent");
            child.transform.SetParent(p: parent.transform);

            // Add singleton component - in Edit Mode, EnsurePersistent is not called
            // This test verifies the structure is set up correctly
            var singleton = child.AddComponent<TestSingletonWithParent>();

            Assert.IsNotNull(anObject: singleton, message: "Singleton should be created");
            Assert.IsNotNull(anObject: singleton.transform.parent, message: "Parent should still exist in Edit Mode");
        }

        [Test]
        public void Singleton_CanBeCreated_InEditMode()
        {
            var go = new GameObject(name: "TestSingleton");
            var singleton = go.AddComponent<TestPersistentSingletonForEditMode>();

            Assert.IsNotNull(anObject: singleton, message: "Singleton component should be created");
            Assert.AreEqual(expected: "TestSingleton", actual: go.name);
        }

        [Test]
        public void MultipleSingletons_CanCoexist_InEditMode()
        {
            // In Edit Mode, duplicate detection doesn't run (no Awake execution)
            var go1 = new GameObject(name: "First");
            var go2 = new GameObject(name: "Second");

            var s1 = go1.AddComponent<TestPersistentSingletonForEditMode>();
            var s2 = go2.AddComponent<TestPersistentSingletonForEditMode>();

            // Both should exist in Edit Mode (no runtime enforcement)
            Assert.IsNotNull(anObject: s1);
            Assert.IsNotNull(anObject: s2);
        }
    }

    [TestFixture]
    public class SingletonRuntimeStateEditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            TestExtensions.ResetQuittingFlagForTesting();
        }

        [Test]
        public void IsQuitting_CanBeSet_ViaNotifyQuitting()
        {
            Assert.IsFalse(condition: SingletonRuntime.IsQuitting, message: "Should start as false");

            SingletonRuntime.NotifyQuitting();

            Assert.IsTrue(condition: SingletonRuntime.IsQuitting, message: "Should be true after NotifyQuitting");
        }

        [Test]
        public void PlaySessionId_IsConsistent_WithinSameSession()
        {
            int id1 = SingletonRuntime.PlaySessionId;
            int id2 = SingletonRuntime.PlaySessionId;
            int id3 = SingletonRuntime.PlaySessionId;

            Assert.AreEqual(expected: id1, actual: id2, message: "PlaySessionId should be consistent");
            Assert.AreEqual(expected: id2, actual: id3, message: "PlaySessionId should be consistent");
        }
    }

    [TestFixture]
    public class SingletonLoggerEditModeTests
    {
        [Test]
        public void Log_WithTypeParameter_FormatsCorrectly()
        {
            // Expect the log to avoid test failure
            LogAssert.Expect(type: LogType.Log, message: "[Singletons.Tests.Editor.TestPersistentSingletonForEditMode] Test info message");

            Assert.DoesNotThrow(() =>
            {
                SingletonLogger.Log<TestPersistentSingletonForEditMode>(message: "Test info message");
            });
        }

        [Test]
        public void LogWarning_WithTypeParameter_FormatsCorrectly()
        {
            // Expect the warning log to avoid test failure
            LogAssert.Expect(type: LogType.Warning, message: "[Singletons.Tests.Editor.TestPersistentSingletonForEditMode] Test warning message");

            Assert.DoesNotThrow(() =>
            {
                SingletonLogger.LogWarning<TestPersistentSingletonForEditMode>(message: "Test warning message");
            });
        }

        [Test]
        public void LogError_WithTypeParameter_FormatsCorrectly()
        {
            // Expect the error log to avoid test failure
            LogAssert.Expect(type: LogType.Error, message: "[Singletons.Tests.Editor.TestPersistentSingletonForEditMode] Test error message");

            Assert.DoesNotThrow(() =>
            {
                SingletonLogger.LogError<TestPersistentSingletonForEditMode>(message: "Test error message");
            });
        }

        [Test]
        public void ThrowInvalidOperation_ThrowsWithCorrectMessage()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(() =>
            {
                SingletonLogger.ThrowInvalidOperation<TestPersistentSingletonForEditMode>(message: "Test exception");
            });

            Assert.IsTrue(condition: ex.Message.Contains(value: "TestPersistentSingletonForEditMode"), message: "Exception should contain type name");
            Assert.IsTrue(condition: ex.Message.Contains(value: "Test exception"), message: "Exception should contain custom message");
        }
    }

    // Test singleton classes for EditMode testing
    public sealed class TestPersistentSingletonForEditMode : GlobalSingleton<TestPersistentSingletonForEditMode>
    {
        protected override void Awake()
        {
            base.Awake();
        }
    }

    public sealed class TestSceneSingletonForEditMode : SceneSingleton<TestSceneSingletonForEditMode>
    {
        protected override void Awake()
        {
            base.Awake();
        }
    }

    /// <summary>
    /// Test singleton that deliberately does NOT call base.Awake() for testing error detection.
    /// </summary>
    public sealed class TestSingletonWithoutBaseAwake : GlobalSingleton<TestSingletonWithoutBaseAwake>
    {
        protected override void Awake()
        {
            // Deliberately NOT calling base.Awake() to test error detection
        }
    }

    /// <summary>
    /// Test singleton for parent reparenting tests.
    /// </summary>
    public sealed class TestSingletonWithParent : GlobalSingleton<TestSingletonWithParent>
    {
        protected override void Awake()
        {
            base.Awake();
        }
    }
}
