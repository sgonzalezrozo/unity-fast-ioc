using System.Diagnostics;

namespace FastIoC.Core
{
    /// <summary>
    /// Startup timing for diagnosing slow boots. Compiled in only when the scripting define symbol
    /// <c>FASTIOC_BOOT_TIMING</c> is set: without it, the compiler removes every call, arguments included.
    /// Each line has the form <c>[BootTiming] FastIoC &lt;label&gt; &lt;ms&gt; ms  @ &lt;seconds since engine start&gt;</c>.
    /// The clock is <see cref="UnityEngine.Time.realtimeSinceStartupAsDouble"/>, so lines from other code that uses
    /// the same clock sort into one timeline.
    /// </summary>
    public static class BootTiming
    {
        /// <summary>The scripting define symbol that turns timing on.</summary>
        public const string SYMBOL = "FASTIOC_BOOT_TIMING";

        /// <summary>A start mark for <see cref="Span"/>. Cheap enough to call without the symbol.</summary>
        public static long Now() => Stopwatch.GetTimestamp();

        /// <summary>Logs how long it took since <paramref name="startTimestamp"/>.</summary>
        [Conditional(SYMBOL)]
        public static void Span(string label, long startTimestamp)
        {
            long ms = (Stopwatch.GetTimestamp() - startTimestamp) * 1000 / Stopwatch.Frequency;
            Write(label, ms.ToString());
        }

        /// <summary>Logs that something happened now.</summary>
        [Conditional(SYMBOL)]
        public static void Point(string label) => Write(label, "-");

        private static void Write(string label, string duration)
        {
            UnityEngine.Debug.Log($"[BootTiming] {"FastIoC " + label,-60} {duration,7} ms  @ {UnityEngine.Time.realtimeSinceStartupAsDouble,8:F3} s");
        }
    }
}
