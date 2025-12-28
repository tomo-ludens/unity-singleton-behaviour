using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Singletons.Core
{
    /// <summary>
    /// Conditional logger for singleton infrastructure.
    /// </summary>
    /// <remarks>
    /// <para><b>Conditional:</b> All methods use <see cref="ConditionalAttribute"/>; call sites (including arguments) are stripped in release.</para>
    /// <para><b>ThrowInvalidOperation:</b> Stripped in release—execution continues silently. Callers must handle null/false.</para>
    /// </remarks>
    internal static class SingletonLogger
    {
        private const string EditorSymbol = "UNITY_EDITOR";
        private const string DevBuildSymbol = "DEVELOPMENT_BUILD";

        /// <summary>
        /// Logs a warning. Stripped in release builds.
        /// </summary>
        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol)]
        public static void LogWarning(string message, string typeTag, UnityEngine.Object context = null)
        {
            Debug.LogWarning(message: $"[{typeTag}] {message}", context: context);
        }

        /// <summary>
        /// Logs an error. Stripped in release builds.
        /// </summary>
        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol)]
        public static void LogError(string message, string typeTag, UnityEngine.Object context = null)
        {
            Debug.LogError(message: $"[{typeTag}] {message}", context: context);
        }

        /// <summary>
        /// Throws in DEV/EDITOR only. In release builds, call site is removed—execution continues past it.
        /// </summary>
        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol)]
        public static void ThrowInvalidOperation(string message, string typeTag)
        {
            throw new InvalidOperationException(message: $"[{typeTag}] {message}");
        }
    }
}
