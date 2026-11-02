using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FastIoC.Core;

namespace FastIoC.Tests.PlayMode
{
    public class AsyncInitializationTests
    {
        private class AsyncService : IAsyncInitializable
        {
            public bool InitializedAsync { get; private set; } = false;

            public async Task InitializeAsync(CancellationToken ct = default)
            {
                await Task.Delay(10, ct);
                InitializedAsync = true;
            }
        }

        private class SyncService : IInitializable
        {
            public bool InitializedSync { get; private set; } = false;

            public void Initialize()
            {
                InitializedSync = true;
            }
        }

        private class BothService : IInitializable, IAsyncInitializable
        {
            public bool InitializedSync { get; private set; } = false;
            public bool InitializedAsync { get; private set; } = false;
            public List<string> InitOrder { get; private set; } = new();

            public void Initialize()
            {
                InitializedSync = true;
                InitOrder.Add("Sync");
            }

            public async Task InitializeAsync(CancellationToken ct = default)
            {
                await Task.Delay(5, ct);
                InitializedAsync = true;
                InitOrder.Add("Async");
            }
        }

        private static void SetInitializableInstances(ContextRuntime context, List<object> instances)
        {
            // Access the InitializationCoordinator from ContextRuntime via reflection
            var coordField = typeof(ContextRuntime)
                .GetField("initCoordinator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (coordField?.GetValue(context) is InitializationCoordinator coordinator)
            {
                // Set the initializableInstances field on the coordinator
                var instancesField = typeof(InitializationCoordinator)
                    .GetField("initializableInstances", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                instancesField?.SetValue(coordinator, instances);
            }
        }

        [Test]
        public async Task AsyncInitialization_SingleService_Initializes()
        {
            // Arrange
            ContextRuntime context = new(10, new RecipeActivator[10], new Lifetime[10]);
            AsyncService service = new();

            SetInitializableInstances(context, new List<object> { service });

            // Act
            await context.InitializeAllAsync();

            // Assert
            Assert.IsTrue(service.InitializedAsync);
        }

        [Test]
        public async Task AsyncInitialization_MultipleServices_RunConcurrently()
        {
            // Arrange
            ContextRuntime context = new(10, new RecipeActivator[10], new Lifetime[10]);
            var services = new List<AsyncService>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 5; i++)
            {
                services.Add(new AsyncService());
            }

            SetInitializableInstances(context, new List<object>(services));

            // Act
            await context.InitializeAllAsync();
            stopwatch.Stop();

            // Assert - all initialized
            foreach (var service in services)
            {
                Assert.IsTrue(service.InitializedAsync);
            }

            // Assert - ran in parallel (not sequentially)
            // 5 tasks × 10ms each = 50ms sequential, ~10ms parallel
            Assert.Less(stopwatch.ElapsedMilliseconds, 40, "Should run in parallel, not sequential");
        }

        [Test]
        public async Task AsyncInitialization_WithConcurrency_Limits()
        {
            // Arrange
            ContextRuntime context = new(10, new RecipeActivator[10], new Lifetime[10]);
            var services = new List<AsyncService>();
            var maxConcurrency = 2;

            for (int i = 0; i < 4; i++)
            {
                services.Add(new AsyncService());
            }

            SetInitializableInstances(context, new List<object>(services));

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await context.InitializeAllAsync(maxConcurrency);
            stopwatch.Stop();

            // Assert - respects concurrency limit
            // 4 tasks × 10ms each with maxConcurrency=2 should take ~20ms
            Assert.Greater(stopwatch.ElapsedMilliseconds, 15, "Should respect concurrency limit");
            Assert.Less(stopwatch.ElapsedMilliseconds, 50, "Should not be completely sequential");
        }

        [Test]
        public async Task SyncInitialization_RunsBeforeAsync()
        {
            // Arrange
            ContextRuntime context = new(10, new RecipeActivator[10], new Lifetime[10]);
            BothService service = new();

            SetInitializableInstances(context, new List<object> { service });

            // Act
            await context.InitializeAllAsync();

            // Assert
            Assert.IsTrue(service.InitializedSync, "Sync should be initialized");
            Assert.IsTrue(service.InitializedAsync, "Async should be initialized");
            Assert.AreEqual(2, service.InitOrder.Count);
            Assert.AreEqual("Sync", service.InitOrder[0], "Sync should run first");
            Assert.AreEqual("Async", service.InitOrder[1], "Async should run second");
        }

        [Test]
        public async Task AsyncInitialization_WithCancellation_Throws()
        {
            // Arrange
            ContextRuntime context = new(10, new RecipeActivator[10], new Lifetime[10]);
            var cts = new CancellationTokenSource();
            var service = new AsyncService();

            SetInitializableInstances(context, new List<object> { service });

            // Act
            cts.Cancel();

            // Assert
            // TaskCanceledException is thrown when a Task is canceled (subclass of OperationCanceledException)
            try
            {
                await context.InitializeAllAsync(cts.Token);
                Assert.Fail("Expected TaskCanceledException");
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // Expected
                Assert.Pass();
            }
        }

        [Test]
        public async Task AsyncInitialization_Idempotent_CalledTwice()
        {
            // Arrange
            ContextRuntime context = new(10, new RecipeActivator[10], new Lifetime[10]);
            var service = new AsyncService();

            SetInitializableInstances(context, new List<object> { service });

            // Act
            await context.InitializeAllAsync();
            await context.InitializeAllAsync(); // Call again

            // Assert - second call should return early
            Assert.IsTrue(service.InitializedAsync);
        }
    }
}


