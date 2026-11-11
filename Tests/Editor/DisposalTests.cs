using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FastIoC.Core;
using FastIoC.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FastIoC.Tests.Editor
{
    /// <summary>Which instances <see cref="ContextRuntime"/> disposes, and when.</summary>
    public class DisposalTests
    {
        private const int SINGLETON = 0, TRANSIENT = 1, CACHED = 2, SCENE_ONLY = 3, FAILING = 4;

        private sealed class Probe : IDisposable
        {
            private readonly List<string> log;
            private readonly string name;

            public Probe(List<string> log, string name)
            {
                this.log = log;
                this.name = name;
            }

            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                log.Add(name);
            }
        }

        private sealed class FailingProbe : IDisposable
        {
            public void Dispose() => throw new InvalidOperationException("dispose failed on purpose");
        }

        private ServiceLocatorEnforcementLevel previousLevel;
        private List<string> log;
        private ContextRuntime context;

        [SetUp]
        public void SetUp()
        {
            previousLevel = ServiceLocatorEnforcer.EnforcementLevel;
            ServiceLocatorEnforcer.EnforcementLevel = ServiceLocatorEnforcementLevel.Off;

            log = new List<string>();
            RecipeActivator[] activators = new RecipeActivator[5];
            Lifetime[] lifetimes = new Lifetime[5];
            activators[SINGLETON] = _ => new Probe(log, "singleton");
            lifetimes[SINGLETON] = Lifetime.Singleton;
            activators[TRANSIENT] = _ => new Probe(log, "transient");
            lifetimes[TRANSIENT] = Lifetime.Transient;
            activators[CACHED] = _ => new Probe(log, "cached");
            lifetimes[CACHED] = Lifetime.Cached;
            activators[FAILING] = _ => new FailingProbe();
            lifetimes[FAILING] = Lifetime.Singleton;
            context = new ContextRuntime(4, activators, lifetimes);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocatorEnforcer.EnforcementLevel = previousLevel;
        }

        private Probe Resolve(int id) => (Probe)context.Resolve(new TypeId(id));

        private void PushSceneWithSingleton(string sceneName)
        {
            RecipeActivator[] sceneActivators = new RecipeActivator[5];
            Lifetime[] sceneLifetimes = new Lifetime[5];
            sceneActivators[SCENE_ONLY] = _ => new Probe(log, "scene");
            sceneLifetimes[SCENE_ONLY] = Lifetime.SceneSingleton;
            context.PushSceneContainer(sceneName, sceneActivators, sceneLifetimes);
        }

        [Test]
        public void SceneUnload_DisposesSceneSingletonsAndCachedInstances_NotSingletons()
        {
            PushSceneWithSingleton("Shop");
            Probe singleton = Resolve(SINGLETON);
            Probe cached = Resolve(CACHED);
            Probe scene = Resolve(SCENE_ONLY);

            context.PopSceneContainer("Shop");

            Assert.AreEqual(1, scene.DisposeCount);
            Assert.AreEqual(1, cached.DisposeCount);
            Assert.AreEqual(0, singleton.DisposeCount);
        }

        [Test]
        public void SceneUnload_DisposesInReverseCreationOrder()
        {
            PushSceneWithSingleton("Shop");
            Resolve(CACHED);
            Resolve(SCENE_ONLY);

            context.PopSceneContainer("Shop");

            CollectionAssert.AreEqual(new[] { "scene", "cached" }, log);
        }

        [Test]
        public void SceneUnload_NewInstanceAfterwardsIsDisposedOnTheNextUnload()
        {
            Probe first = Resolve(CACHED);
            context.PopSceneContainer("A");
            Probe second = Resolve(CACHED);

            context.PopSceneContainer("B");

            Assert.AreNotSame(first, second);
            Assert.AreEqual(1, first.DisposeCount);
            Assert.AreEqual(1, second.DisposeCount);
        }

        [Test]
        public void ContextDispose_DisposesEverythingOnce_SceneInstancesBeforeSingletons()
        {
            PushSceneWithSingleton("Shop");
            Probe singleton = Resolve(SINGLETON);
            Probe scene = Resolve(SCENE_ONLY);

            context.Dispose();
            context.Dispose();

            Assert.AreEqual(1, singleton.DisposeCount);
            Assert.AreEqual(1, scene.DisposeCount);
            CollectionAssert.AreEqual(new[] { "scene", "singleton" }, log);
        }

        [Test]
        public void Transient_IsNeverDisposed()
        {
            Probe transient = Resolve(TRANSIENT);

            context.PopSceneContainer("AnyScene");
            context.Dispose();

            Assert.AreEqual(0, transient.DisposeCount);
        }

        [Test]
        public void ClearSceneSingletons_KeepsCachedInstances()
        {
            PushSceneWithSingleton("Shop");
            Probe cached = Resolve(CACHED);
            Probe scene = Resolve(SCENE_ONLY);

            context.ClearSceneSingletons();

            Assert.AreEqual(1, scene.DisposeCount);
            Assert.AreEqual(0, cached.DisposeCount);
            Assert.AreSame(cached, Resolve(CACHED));
        }

        [Test]
        public void FailingDispose_IsLogged_AndTheRestAreStillDisposed()
        {
            Probe singleton = Resolve(SINGLETON);
            context.Resolve(new TypeId(FAILING));
            LogAssert.Expect(LogType.Exception, new Regex("dispose failed on purpose"));

            context.Dispose();

            Assert.AreEqual(1, singleton.DisposeCount);
        }

        [Test]
        public void InstanceBinding_IsNotDisposed()
        {
            InstanceCacheArray cache = new(1);
            Probe registered = new(log, "registered");
            Probe created = new(log, "created");
            DisposalCoordinator coordinator = new(cache, (typeId, instance) => ReferenceEquals(instance, registered));

            coordinator.TrackInstance(new TypeId(0), Lifetime.Singleton, registered);
            coordinator.TrackInstance(new TypeId(1), Lifetime.Singleton, created);
            coordinator.Dispose();

            Assert.AreEqual(0, registered.DisposeCount);
            Assert.AreEqual(1, created.DisposeCount);
        }

        [Test]
        public void UnityObject_IsNotDisposed()
        {
            DisposableAsset asset = ScriptableObject.CreateInstance<DisposableAsset>();
            try
            {
                DisposalCoordinator coordinator = new(new InstanceCacheArray(1));

                coordinator.TrackInstance(new TypeId(0), Lifetime.Singleton, asset);

                Assert.AreEqual(0, coordinator.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
