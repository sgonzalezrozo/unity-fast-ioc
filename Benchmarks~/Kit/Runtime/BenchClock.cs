using System;
using System.Diagnostics;
using UnityEngine;

namespace FastIoCBench
{
    /// <summary>Timestamps taken from the first managed code that runs in the process.</summary>
    public static class BenchClock
    {
        public static long T0 { get; private set; }
        public static long HeapAtT0 { get; private set; }
        public static int Gen0AtT0 { get; private set; }

        public static long Now => Stopwatch.GetTimestamp();

        public static double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            T0 = Stopwatch.GetTimestamp();
            HeapAtT0 = GC.GetTotalMemory(false);
            Gen0AtT0 = GC.CollectionCount(0);
        }
    }
}
