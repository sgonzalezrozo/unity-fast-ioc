using System.Reflection;
using FastIoC.API.Initialization;
using FastIoC.Core;
using NUnit.Framework;
using UnityEngine;

namespace FastIoC.Tests.Editor
{
    public class SceneInjectionTimingTests
    {
        private ContextRuntime context;

        [SetUp]
        public void SetUp()
        {
            context = new ContextRuntime(0, new RecipeActivator[0], new Lifetime[0]);
            SceneInjectionManifest.Awakened = null;
        }

        [TearDown]
        public void TearDown()
        {
            SceneInjectionManifest.Awakened = null;
            context.Dispose();
        }

        [Test]
        public void Manifest_ExecutionOrder_IsTheLowestExpected()
        {
            DefaultExecutionOrder order = typeof(SceneInjectionManifest).GetCustomAttribute<DefaultExecutionOrder>();

            Assert.IsNotNull(order);
            Assert.AreEqual(SceneInjectionManifest.EXECUTION_ORDER, order.order);
            Assert.LessOrEqual(order.order, -32000);
        }

        [Test]
        public void Subscribe_SetsTheManifestAwakeHook()
        {
            SceneEventSubscriber subscriber = new(context);

            subscriber.Subscribe();

            Assert.IsNotNull(SceneInjectionManifest.Awakened);
            subscriber.Unsubscribe();
        }

        [Test]
        public void Unsubscribe_ClearsTheManifestAwakeHook()
        {
            SceneEventSubscriber subscriber = new(context);
            subscriber.Subscribe();

            subscriber.Unsubscribe();

            Assert.IsNull(SceneInjectionManifest.Awakened);
        }

        [Test]
        public void Unsubscribe_OfAnOlderSubscriber_KeepsTheNewerHook()
        {
            SceneEventSubscriber older = new(context);
            SceneEventSubscriber newer = new(context);
            older.Subscribe();
            newer.Subscribe();

            older.Unsubscribe();

            Assert.IsNotNull(SceneInjectionManifest.Awakened);
            newer.Unsubscribe();
        }
    }
}
