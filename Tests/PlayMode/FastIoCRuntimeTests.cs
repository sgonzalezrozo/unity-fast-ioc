using System.Collections;
using NUnit.Framework;
using Tools.FastIoC.Runtime.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tools.FastIoC.Tests.PlayMode
{
    public class FastIoCRuntimeTests
    {
        // Test service interfaces
        public interface ITestService
        {
            string GetName();
        }

        public class TestService : ITestService
        {
            public string GetName() => "TestService";
        }

        public class DependentService
        {
            private readonly ITestService service;

            public DependentService(ITestService service)
            {
                this.service = service;
            }

            public string GetServiceName() => service.GetName();
        }

        public class TestServiceMonoBehaviour : MonoBehaviour
        {
            internal ITestService testService;

            public string GetServiceName() => testService?.GetName() ?? "Uninjected";
        }

        [UnityTest]
        public IEnumerator TestTypeIdEquality()
        {
            TypeId id1 = new(42);
            TypeId id2 = new(42);
            TypeId id3 = new(43);

            Assert.AreEqual(id1, id2);
            Assert.AreNotEqual(id1, id3);
            yield break;
        }

        [UnityTest]
        public IEnumerator TestInstanceCacheArray()
        {
            InstanceCacheArray cache = new(10);
            object instance = new();
            TypeId typeId = new(0);

            // Should not be cached initially
            Assert.IsFalse(cache.TryGetCached(typeId, Lifetime.Singleton, out _));

            // Set and retrieve
            cache.SetCached(typeId, Lifetime.Singleton, instance);
            Assert.IsTrue(cache.TryGetCached(typeId, Lifetime.Singleton, out object cached));
            Assert.AreSame(instance, cached);
            yield break;
        }

        [UnityTest]
        public IEnumerator TestLifetimes()
        {
            Assert.AreEqual(0, (int)Lifetime.Transient);
            Assert.AreEqual(1, (int)Lifetime.Singleton);
            Assert.AreEqual(2, (int)Lifetime.Cached);
            yield break;
        }

        [UnityTest]
        public IEnumerator TestInitializableHook()
        {
            bool initialized = false;

            TestInitializable initService = new(() => { initialized = true; });
            Assert.IsFalse(initialized);

            initService.Initialize();
            Assert.IsTrue(initialized);
            yield break;
        }

        private class TestInitializable : IInitializable
        {
            private readonly System.Action onInit;

            public TestInitializable(System.Action onInit)
            {
                this.onInit = onInit;
            }

            public void Initialize() => onInit?.Invoke();
        }

        [UnityTest]
        public IEnumerator TestDisposableHook()
        {
            bool disposed = false;

            TestDisposable dispService = new(() => { disposed = true; });
            Assert.IsFalse(disposed);

            dispService.OnDispose();
            Assert.IsTrue(disposed);
            yield break;
        }

        private class TestDisposable : IDisposableHook
        {
            private readonly System.Action onDispose;

            public TestDisposable(System.Action onDispose)
            {
                this.onDispose = onDispose;
            }

            public void OnDispose() => onDispose?.Invoke();
        }
    }
}


