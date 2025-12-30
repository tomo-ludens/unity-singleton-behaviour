using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Singletons.Core
{
    /// <summary>
    /// Conditional logger for singleton infrastructure with improved ergonomics.
    /// </summary>
    /// <remarks>
    /// <para><b>Conditional:</b> All methods use <see cref="ConditionalAttribute"/>; call sites (including arguments) are stripped in release.</para>
    /// <para><b>ThrowInvalidOperation:</b> Stripped in release—execution continues silently. Callers must handle null/false.</para>
    /// <para><b>Generic overloads:</b> Type tag is automatically resolved from generic type parameter.</para>
    /// </remarks>
    internal static class SingletonLogger
    {
        private const string EditorSymbol = "UNITY_EDITOR";
        private const string DevBuildSymbol = "DEVELOPMENT_BUILD";

        /// <summary>
        /// Logs a warning for infrastructure components. Stripped in release builds.
        /// </summary>
        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol)]
        public static void LogWarning(string message, UnityEngine.Object context = null)
        {
            Debug.LogWarning(message: $"[{nameof(SingletonRuntime)}] {message}", context: context);
        }

        /// <summary>
        /// Logs a warning with automatic type tag resolution. Stripped in release builds.
        /// </summary>
        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol)]
        public static void LogWarning<T>(string message, UnityEngine.Object context = null)
        {
            Debug.LogWarning(message: $"[{typeof(T).FullName}] {message}", context: context);
        }

        /// <summary>
        /// Logs an error for infrastructure components. Stripped in release builds.
        /// </summary>
        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol)]
        public static void LogError(string message, UnityEngine.Object context = null)
        {
            Debug.LogError(message: $"[{nameof(SingletonRuntime)}] {message}", context: context);
        }

        /// <summary>
        /// Logs an error with automatic type tag resolution. Stripped in release builds.
        /// </summary>
        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol)]
        public static void LogError<T>(string message, UnityEngine.Object context = null)
        {
            Debug.LogError(message: $"[{typeof(T).FullName}] {message}", context: context);
        }

        /// <summary>
        /// Throws InvalidOperationException with automatic type tag resolution. Stripped in release builds.
        /// </summary>
        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol)]
        public static void ThrowInvalidOperation<T>(string message)
        {
            throw new InvalidOperationException(message: $"[{typeof(T).FullName}] {message}");
        }
    }
}
