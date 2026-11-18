using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace KnockRush.Utilities
{
    /// <summary>
    /// Project-wide logging wrapper.
    ///
    /// <see cref="Info"/> calls are compiled out of release builds entirely (including the
    /// string formatting at the call site) unless KNOCKRUSH_VERBOSE_LOGS is defined.
    /// Warnings, errors and exceptions are always kept because they feed crash reporting.
    /// </summary>
    public static class Log
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("KNOCKRUSH_VERBOSE_LOGS")]
        public static void Info(string tag, string message, Object context = null)
        {
            Debug.Log($"[{tag}] {message}", context);
        }

        public static void Warning(string tag, string message, Object context = null)
        {
            Debug.LogWarning($"[{tag}] {message}", context);
        }

        public static void Error(string tag, string message, Object context = null)
        {
            Debug.LogError($"[{tag}] {message}", context);
        }

        public static void Exception(Exception exception, Object context = null)
        {
            Debug.LogException(exception, context);
        }
    }
}
