using System;
using FastIoC.Core;
using FastIoC.Fixtures;
using NUnit.Framework;

namespace FastIoC.Tests.Editor
{
    /// <summary>Resolution and lifetimes of <see cref="ContextRuntime"/>, with hand-written activators.</summary>
    public class ContainerTests
    {
        private const int SINGLETON = 0, TRANSIENT = 1, CACHED = 2, SCENE_ONLY = 3;
        private ServiceLocatorEnforcementLevel previousLevel;
        private ContextRuntime context;

        [SetUp]
        public void SetUp()
        {
            // Tests resolve directly, which the service-locator check would reject.
            previousLevel = ServiceLocatorEnforcer.EnforcementLevel;
            ServiceLocatorEnforcer.EnforcementLevel = ServiceLocatorEnforcementLevel.Off;

            RecipeActivator[] activators = new RecipeActivator[4];
            Lifetime[] lifetimes = new Lifetime[4];
            activators[SINGLETON] = _ => new object();
            lifetimes[SINGLETON] = Lifetime.Singleton;
            activators[TRANSIENT] = _ => new object();
            lifetimes[TRANSIENT] = Lifetime.Transient;
            activators[CACHED] = _ => new object();
            lifetimes[CACHED] = Lifetime.Cached;
            context = new ContextRuntime(3, activators, lifetimes);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocatorEnforcer.EnforcementLevel = previousLevel;
        }

        private object Resolve(int id) => context.Resolve(new TypeId(id));

        [Test]
        public void Singleton_ReturnsTheSameInstance()
        {
            Assert.AreSame(Resolve(SINGLETON), Resolve(SINGLETON));
        }

        [Test]
        public void Transient_ReturnsANewInstanceEachTime()
        {
            Assert.AreNotSame(Resolve(TRANSIENT), Resolve(TRANSIENT));
        }

        [Test]
        public void Cached_IsKeptUntilASceneUnloads()
        {
            object first = Resolve(CACHED);
            Assert.AreSame(first, Resolve(CACHED));

            context.PopSceneContainer("AnyScene");

            Assert.AreNotSame(first, Resolve(CACHED));
        }

        [Test]
        public void Singleton_SurvivesASceneUnload()
        {
            object first = Resolve(SINGLETON);
            context.PopSceneContainer("AnyScene");
            Assert.AreSame(first, Resolve(SINGLETON));
        }

        [Test]
        public void SceneBinding_IsAvailableOnlyWhileItsSceneIsLoaded()
        {
            RecipeActivator[] sceneActivators = new RecipeActivator[4];
            Lifetime[] sceneLifetimes = new Lifetime[4];
            sceneActivators[SCENE_ONLY] = _ => "scene";
            sceneLifetimes[SCENE_ONLY] = Lifetime.SceneSingleton;

            context.PushSceneContainer("Shop", sceneActivators, sceneLifetimes);
            Assert.AreEqual("scene", Resolve(SCENE_ONLY));

            context.PopSceneContainer("Shop");
            Assert.IsFalse(context.TryResolve(new TypeId(SCENE_ONLY), out _));
        }

        [Test]
        public void TryResolve_ReturnsFalseForAnUnboundType()
        {
            Assert.IsFalse(context.TryResolve(new TypeId(SCENE_ONLY), out object instance));
            Assert.IsNull(instance);
        }

        [Test]
        public void Resolve_ThrowsForAnUnboundType()
        {
            Assert.Throws<InvalidOperationException>(() => Resolve(SCENE_ONLY));
        }

        [Test]
        public void AsSingle_AllContractsToSameConcrete_ResolveSameInstance()
        {
            // Matches the generated pattern for:
            //   Bind<IFirst>().To<Both>().AsSingle();
            //   Bind<ISecond>().To<Both>().AsSingle();
            //   Bind<Both>().AsSingle();
            //
            // IFirst and ISecond don't get their own TypeIds; all resolution goes through
            // Both's slot via interfaceImplementations in the generator.
            const int BOTH_ID = 0;

            RecipeActivator[] activators = new RecipeActivator[] { _ => new Both() };
            Lifetime[] lifetimes = new Lifetime[] { Lifetime.Singleton };
            ContextRuntime ctx = new(0, activators, lifetimes);

            ServiceLocatorEnforcer.EnforcementLevel = ServiceLocatorEnforcementLevel.Off;

            object viaIFirst = ctx.Resolve(new TypeId(BOTH_ID));
            object viaISecond = ctx.Resolve(new TypeId(BOTH_ID));
            object viaBoth = ctx.Resolve(new TypeId(BOTH_ID));

            Assert.IsInstanceOf<Both>(viaBoth);
            Assert.AreSame(viaIFirst, viaBoth);
            Assert.AreSame(viaISecond, viaBoth);
        }
    }
}
