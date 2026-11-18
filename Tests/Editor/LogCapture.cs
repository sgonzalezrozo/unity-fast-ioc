using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FastIoC.Tests.Editor
{
    /// <summary>
    /// Keeps the logs written while it's active out of the Console: it replaces Unity's log handler and records each
    /// message, so tests that trigger warnings or errors on purpose stay quiet and can still assert on them.
    /// Logs aren't sent to the Test Runner either, so an unexpected error doesn't fail the test while it's active.
    /// Define TOOLS_TEST_LOGS (Player Settings > Scripting Define Symbols) to also print them, as plain logs so an
    /// expected error doesn't fail the test.
    /// </summary>
    internal sealed class LogCapture : ILogHandler, IDisposable
    {
        private readonly ILogHandler _previous;
        private readonly List<(LogType Type, string Message)> _entries = new();
        private readonly object _lock = new();

        public LogCapture()
        {
            _previous = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = this;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            string message = args == null || args.Length == 0 ? format : string.Format(format, args);
            lock (_lock)
                _entries.Add((logType, message));
            Print(logType, message);
        }

        public void LogException(Exception exception, Object context)
        {
            lock (_lock)
                _entries.Add((LogType.Exception, exception.ToString()));
            Print(LogType.Exception, exception.ToString());
        }

        /// <summary>Fails the test unless a message of this type matching the pattern was logged.</summary>
        public void AssertLogged(LogType type, string pattern)
        {
            lock (_lock)
            {
                Regex regex = new(pattern);
                if (_entries.Exists(entry => entry.Type == type && regex.IsMatch(entry.Message)))
                    return;

                Assert.Fail($"Expected a {type} log matching \"{pattern}\". Logged:\n{Describe()}");
            }
        }

        /// <summary>Fails the test unless exactly one message was logged, of this type and matching the pattern.</summary>
        public void AssertOnlyLogged(LogType type, string pattern)
        {
            lock (_lock)
            {
                if (_entries.Count == 1 && _entries[0].Type == type && Regex.IsMatch(_entries[0].Message, pattern))
                    return;

                Assert.Fail($"Expected only one {type} log matching \"{pattern}\". Logged:\n{Describe()}");
            }
        }

        /// <summary>Fails the test if anything was logged.</summary>
        public void AssertNothingLogged()
        {
            lock (_lock)
            {
                if (_entries.Count > 0)
                    Assert.Fail($"Expected no logs. Logged:\n{Describe()}");
            }
        }

        public void Dispose()
        {
            if (Debug.unityLogger.logHandler == this)
                Debug.unityLogger.logHandler = _previous;
        }

        [System.Diagnostics.Conditional("TOOLS_TEST_LOGS")]
        private void Print(LogType type, string message)
            => _previous.LogFormat(LogType.Log, null, "{0}", $"[Test log, {type}] {message}");

        private string Describe()
        {
            if (_entries.Count == 0)
                return "  (nothing)";

            List<string> lines = new();
            foreach ((LogType type, string message) in _entries)
                lines.Add($"  [{type}] {message}");
            return string.Join("\n", lines);
        }
    }
}
