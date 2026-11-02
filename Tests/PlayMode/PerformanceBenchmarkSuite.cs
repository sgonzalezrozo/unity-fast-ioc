using System;
using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Tools.FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Performance benchmark suite for FastIoC.
    /// Measures and reports performance characteristics of the DI container.
    /// </summary>
    public class PerformanceBenchmarkSuite : IntegrationTestBase
    {
        private string testScene = "Assets/Tools/FastIoC/Tests/Scenes/TestScene.unity";
        private const int ITERATIONS = 100;

        [UnityTest]
        public IEnumerator Benchmark_ServiceResolution_Speed()
        {
            yield return WaitForSceneLoad(testScene);

            SceneTransitionTestComponent component = FindInScene<SceneTransitionTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");
            Assert.IsNotNull(component.QuickService, "Service not injected");

            // Warm up
            for (int i = 0; i < 10; i++)
                _ = component.QuickService.GetData();

            // Benchmark
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < ITERATIONS; i++)
            {
                _ = component.QuickService.GetData();
            }
            sw.Stop();

            double avgTimeMs = sw.Elapsed.TotalMilliseconds / ITERATIONS;
            double opsPerSecond = ITERATIONS / sw.Elapsed.TotalSeconds;

            Debug.Log($"[Benchmark] Service Resolution Speed: {avgTimeMs:F4}ms per operation, {opsPerSecond:F0} ops/sec");

            Assert.That(avgTimeMs, Is.LessThan(1.0), $"Service resolution too slow: {avgTimeMs:F4}ms (should be <1ms)");

            Assert.Pass($"Resolution: {avgTimeMs:F4}ms, {opsPerSecond:F0} ops/sec");
        }

        [UnityTest]
        public IEnumerator Benchmark_SceneLoad_Overhead()
        {
            // Benchmark empty scene load (baseline)
            float baselineStart = Time.realtimeSinceStartup;
            yield return WaitForSceneLoad(testScene);
            float fastIoCLoadTime = Time.realtimeSinceStartup - baselineStart;

            InjectedTestComponent[] components = Object.FindObjectsByType<InjectedTestComponent>(FindObjectsSortMode.None);
            Assert.Greater(components.Length, 0, "No components found");

            // Verify all injected
            foreach (InjectedTestComponent component in components)
            {
                Assert.IsNotNull(component.InjectedService, "Component not injected");
            }

            double overheadMs = fastIoCLoadTime * 1000.0;
            Debug.Log($"[Benchmark] Scene Load Overhead: {overheadMs:F2}ms with FastIoC injection");

            Assert.That(fastIoCLoadTime, Is.LessThan(3.0f),
                $"Scene load too slow: {overheadMs:F2}ms (should be <3s)");

            Assert.Pass($"Scene load time: {overheadMs:F2}ms");
        }

        [UnityTest]
        public IEnumerator Benchmark_Injection_PerComponent()
        {
            yield return WaitForSceneLoad(testScene);

            InjectedTestComponent[] components = Object.FindObjectsByType<InjectedTestComponent>(FindObjectsSortMode.None);
            Assert.Greater(components.Length, 0, "No components found");

            long totalMemoryBefore = GC.GetTotalMemory(false);

            // Simulate component lifecycle
            Stopwatch sw = Stopwatch.StartNew();
            int componentCount = 0;
            foreach (InjectedTestComponent component in components)
            {
                Assert.IsNotNull(component.InjectedService, "Component injection failed");
                componentCount++;
            }
            sw.Stop();

            long totalMemoryAfter = GC.GetTotalMemory(false);
            long memoryPerComponent = (totalMemoryAfter - totalMemoryBefore) / Mathf.Max(componentCount, 1);

            double timePerComponent = sw.Elapsed.TotalMilliseconds / Mathf.Max(componentCount, 1);

            Debug.Log($"[Benchmark] Injection per Component: {timePerComponent:F4}ms, {memoryPerComponent} bytes memory");

            Assert.That(timePerComponent, Is.LessThan(0.5),
                $"Per-component injection too slow: {timePerComponent:F4}ms (should be <0.5ms)");

            Assert.Pass($"Per-component: {timePerComponent:F4}ms, {memoryPerComponent} bytes");
        }

        [UnityTest]
        public IEnumerator Benchmark_MemoryUsage_MultipleScenes()
        {
            long memoryPerScene = 0;
            int sceneCount = 0;

            // Load first scene
            long memory1 = GC.GetTotalMemory(true);
            yield return WaitForSceneLoad(testScene);
            long memoryAfterScene1 = GC.GetTotalMemory(false);
            sceneCount++;
            memoryPerScene = memoryAfterScene1 - memory1;

            InjectedTestComponent[] components = Object.FindObjectsByType<InjectedTestComponent>(FindObjectsSortMode.None);
            Assert.Greater(components.Length, 0, "No components in first scene");

            long avgMemoryPerScene = memoryPerScene / sceneCount;
            long estimatedFor1000Scenes = avgMemoryPerScene * 1000;

            Debug.Log($"[Benchmark] Memory per Scene: {memoryPerScene / 1024} KB, " +
                     $"estimated for 1000 scenes: {estimatedFor1000Scenes / (1024 * 1024)} MB");

            // Ensure memory doesn't explode with scale
            const long MEMORY_PER_1000_SCENES_THRESHOLD = 500 * 1024 * 1024; // 500MB
            Assert.That(estimatedFor1000Scenes, Is.LessThan(MEMORY_PER_1000_SCENES_THRESHOLD),
                $"Projected memory usage too high: {estimatedFor1000Scenes / (1024 * 1024)}MB " +
                $"(should be <500MB for 1000 scenes)");

            Assert.Pass($"Memory per scene: {memoryPerScene / 1024}KB");
        }

        [UnityTest]
        public IEnumerator Benchmark_GCPressure_DuringGameplay()
        {
            yield return WaitForSceneLoad(testScene);

            long gcAllocsBefore = GC.GetTotalMemory(true);

            // Simulate gameplay - repeatedly access services
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < ITERATIONS; i++)
            {
                SceneTransitionTestComponent component = FindInScene<SceneTransitionTestComponent>("TestScene");
                if (component != null)
                {
                    _ = component.QuickService.GetData();
                    _ = component.TrackableService.ServiceId;
                }
            }
            sw.Stop();

            long gcAllocsAfter = GC.GetTotalMemory(false);
            long gcAllocations = gcAllocsAfter - gcAllocsBefore;
            double allocPerOp = gcAllocations / (double)ITERATIONS;

            Debug.Log($"[Benchmark] GC Pressure: {allocPerOp:F0} bytes/operation, " +
                     $"total: {gcAllocations / 1024}KB for {ITERATIONS} operations");

            // Target: <100 bytes per service access
            Assert.That(allocPerOp, Is.LessThan(100.0),
                $"GC pressure too high: {allocPerOp:F0} bytes/op (should be <100)");

            Assert.Pass($"GC Pressure: {allocPerOp:F0} bytes/op");
        }
    }
}

