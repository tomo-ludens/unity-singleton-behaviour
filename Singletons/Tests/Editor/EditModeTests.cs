using NUnit.Framework;
using Singletons.Core;

namespace Singletons.Tests.Editor
{
    [TestFixture]
    public class SingletonRuntimeEditModeTests
    {
        [Test]
        public void PlaySessionId_IsAccessible()
        {
            var sessionId = SingletonRuntime.PlaySessionId;
            Assert.GreaterOrEqual(arg1: sessionId, arg2: 0, message: "PlaySessionId should be non-negative");
        }

        [Test]
        public void IsQuitting_IsFalse_InEditMode()
        {
            SingletonRuntime.ResetQuittingFlagForTesting();
            Assert.IsFalse(condition: SingletonRuntime.IsQuitting, message: "IsQuitting should be false in Edit Mode");
        }

        [Test]
        public void SimulateQuitting_CanBeReset()
        {
            SingletonRuntime.SimulateQuittingForTesting();
            Assert.IsTrue(condition: SingletonRuntime.IsQuitting);

            SingletonRuntime.ResetQuittingFlagForTesting();
            Assert.IsFalse(condition: SingletonRuntime.IsQuitting);
        }

        [Test]
        public void AdvancePlaySession_IncrementsId()
        {
            var before = SingletonRuntime.PlaySessionId;
            SingletonRuntime.AdvancePlaySessionForTesting();
            var after = SingletonRuntime.PlaySessionId;

            Assert.AreEqual(expected: before + 1, actual: after, message: "PlaySessionId should increment");
        }

        [TearDown]
        public void TearDown()
        {
            SingletonRuntime.ResetQuittingFlagForTesting();
        }
    }
}
