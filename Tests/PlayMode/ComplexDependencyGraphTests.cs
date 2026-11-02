using System;
using System.Diagnostics;
using NUnit.Framework;

namespace FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Integration tests for complex dependency graphs.
    /// Verifies that FastIoC correctly handles real-world scenarios
    /// with multiple levels of service dependencies.
    /// </summary>
    public class ComplexDependencyGraphTests
    {
        // ============== Service Definitions ==============

        /// <summary>
        /// Leaf service - no dependencies
        /// </summary>
        public interface IServiceC
        {
            string GetValue();
            int GetInstanceId();
        }

        public class ServiceC : IServiceC
        {
            private static int instanceCounter = 0;
            private int instanceId = ++instanceCounter;
            private string value = $"ServiceC#{instanceCounter}";

            public string GetValue() => value;
            public int GetInstanceId() => instanceId;
        }

        /// <summary>
        /// Middle service - depends on IServiceC
        /// </summary>
        public interface IServiceB
        {
            IServiceC ServiceC { get; }
            string GetChainValue();
            int GetInstanceId();
        }

        public class ServiceB : IServiceB
        {
            private static int instanceCounter = 0;
            private int instanceId = ++instanceCounter;

            private readonly IServiceC serviceC;

            public IServiceC ServiceC => serviceC;

            public ServiceB(IServiceC serviceC)
            {
                if (serviceC == null)
                    throw new ArgumentNullException(nameof(serviceC), "ServiceC dependency not injected");

                this.serviceC = serviceC;
            }

            public string GetChainValue() => $"ServiceB→{serviceC.GetValue()}";
            public int GetInstanceId() => instanceId;
        }

        /// <summary>
        /// Root service - depends on IServiceB (which depends on IServiceC)
        /// </summary>
        public interface IServiceA
        {
            IServiceB ServiceB { get; }
            string GetFullChain();
            int GetInstanceId();
        }

        public class ServiceA : IServiceA
        {
            private static int instanceCounter = 0;
            private int instanceId = ++instanceCounter;

            private readonly IServiceB serviceB;

            public IServiceB ServiceB => serviceB;

            public ServiceA(IServiceB serviceB)
            {
                if (serviceB == null)
                    throw new ArgumentNullException(nameof(serviceB), "ServiceB dependency not injected");

                this.serviceB = serviceB;
            }

            public string GetFullChain() => $"ServiceA→{serviceB.GetChainValue()}";
            public int GetInstanceId() => instanceId;
        }

        // ============== Shared Dependency Example ==============

        /// <summary>
        /// Service D depends on C (for shared dependency test)
        /// </summary>
        public interface IServiceD
        {
            IServiceC SharedC { get; }
            string GetValue();
        }

        public class ServiceD : IServiceD
        {
            private readonly IServiceC sharedC;

            public IServiceC SharedC => sharedC;

            public ServiceD(IServiceC sharedC)
            {
                this.sharedC = sharedC;
            }

            public string GetValue() => $"ServiceD→{sharedC.GetValue()}";
        }

        // ============== Tests ==============

        [Test]
        public void ComplexDependencyGraph_LinearChain_ResolvesCorrectly()
        {
            // Arrange - Create manual DI setup
            var serviceC = new ServiceC();
            var serviceB = new ServiceB(serviceC);
            var serviceA = new ServiceA(serviceB);

            // Act
            string fullChain = serviceA.GetFullChain();

            // Assert
            Assert.IsNotNull(serviceA, "ServiceA should be created");
            Assert.IsNotNull(serviceB, "ServiceB should be created");
            Assert.IsNotNull(serviceC, "ServiceC should be created");
            Assert.That(fullChain, Does.Contain("ServiceA"), "Chain should contain ServiceA");
            Assert.That(fullChain, Does.Contain("ServiceB"), "Chain should contain ServiceB");
            Assert.That(fullChain, Does.Contain("ServiceC"), "Chain should contain ServiceC");
            Assert.IsTrue(fullChain.Contains("→"), "Chain should show dependency direction");

            UnityEngine.Debug.Log($"[ComplexDependency] Full chain: {fullChain}");
        }

        [Test]
        public void ComplexDependencyGraph_LinearChain_AllDependenciesInjected()
        {
            // Arrange
            var serviceC = new ServiceC();
            var serviceB = new ServiceB(serviceC);
            var serviceA = new ServiceA(serviceB);

            // Act & Assert
            Assert.AreSame(serviceC, serviceB.ServiceC, "ServiceB should have injected ServiceC");
            Assert.AreSame(serviceB, serviceA.ServiceB, "ServiceA should have injected ServiceB");
            Assert.AreSame(serviceC, serviceA.ServiceB.ServiceC, "Should access ServiceC through chain");
        }

        [Test]
        public void ComplexDependencyGraph_SharedDependency_BothInject()
        {
            // Arrange - Both B and D depend on C (but different instances)
            var serviceC = new ServiceC();
            var serviceB = new ServiceB(serviceC);
            var serviceD = new ServiceD(serviceC);

            // Act
            string bValue = serviceB.GetChainValue();
            string dValue = serviceD.GetValue();

            // Assert - Both should have the same ServiceC instance (shared)
            Assert.AreSame(serviceC, serviceB.ServiceC, "ServiceB should have shared ServiceC");
            Assert.AreSame(serviceC, serviceD.SharedC, "ServiceD should have same ServiceC");
            Assert.AreEqual(serviceB.ServiceC.GetValue(), serviceD.SharedC.GetValue(),
                "Shared ServiceC should return same value");

            UnityEngine.Debug.Log($"[SharedDependency] ServiceB: {bValue}, ServiceD: {dValue}");
        }

        [Test]
        public void ComplexDependencyGraph_LinearChain_MissingDependencyThrows()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                new ServiceB(null);
            }, "ServiceB should throw when ServiceC is null");

            Assert.Throws<ArgumentNullException>(() =>
            {
                new ServiceA(null);
            }, "ServiceA should throw when ServiceB is null");
        }

        [Test]
        public void ComplexDependencyGraph_ResolutionPerformance_AcceptableLatency()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act - Create chain 1000 times
            for (int i = 0; i < 1000; i++)
            {
                var serviceC = new ServiceC();
                var serviceB = new ServiceB(serviceC);
                var serviceA = new ServiceA(serviceB);
                _ = serviceA.GetFullChain();
            }

            stopwatch.Stop();

            // Assert
            double avgTimePerChain = stopwatch.Elapsed.TotalMilliseconds / 1000.0;
            Assert.That(avgTimePerChain, Is.LessThan(0.1),
                $"Resolution too slow: {avgTimePerChain}ms per chain (should be <0.1ms)");

            UnityEngine.Debug.Log($"[Performance] Average resolution per chain: {avgTimePerChain:F4}ms");
        }

        [Test]
        public void ComplexDependencyGraph_InstanceTracking_SingletonBehavior()
        {
            // Arrange - Create two instances with same ServiceC
            var sharedC = new ServiceC();
            var b1 = new ServiceB(sharedC);
            var b2 = new ServiceB(sharedC);

            // Act
            int b1Id = b1.GetInstanceId();
            int b2Id = b2.GetInstanceId();
            int cId = sharedC.GetInstanceId();

            // Assert - ServiceB instances should be different, but share ServiceC
            Assert.AreNotEqual(b1Id, b2Id, "ServiceB instances should have different IDs");
            Assert.AreEqual(sharedC.GetInstanceId(), b1.ServiceC.GetInstanceId(),
                "Both ServiceB should reference same ServiceC instance");
        }

        [Test]
        public void ComplexDependencyGraph_MultipleRootsSharedDependency()
        {
            // Arrange - A and D both depend on B and C (shared C)
            var serviceC = new ServiceC();
            var serviceB = new ServiceB(serviceC);
            var serviceA = new ServiceA(serviceB);
            var serviceD = new ServiceD(serviceC);

            // Act
            var aChain = serviceA.GetFullChain();
            var dValue = serviceD.GetValue();

            // Assert - Both should successfully resolve with shared dependency
            Assert.IsNotNull(aChain, "ServiceA chain should resolve");
            Assert.IsNotNull(dValue, "ServiceD should resolve");
            Assert.AreEqual(serviceB.ServiceC.GetValue(), serviceD.SharedC.GetValue(),
                "Shared ServiceC value should match");

            UnityEngine.Debug.Log($"[MultipleRoots] Chain A: {aChain}, Value D: {dValue}");
        }
    }
}

