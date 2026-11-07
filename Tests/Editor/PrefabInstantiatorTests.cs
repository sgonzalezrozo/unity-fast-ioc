using System.Collections.Generic;
using FastIoC.Adapters;
using FastIoC.Core;
using FastIoC.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace FastIoC.Tests.Editor
{
    /// <summary>
    /// Active state of copies made by <see cref="FastIoCPrefabInstantiator"/> (used by tools through IPrefabInstantiator).
    /// No ProjectContext runs in Edit Mode tests, so injection itself is skipped with a warning.
    /// </summary>
    public class PrefabInstantiatorTests
    {
        private readonly List<GameObject> created = new();
        private FastIoCPrefabInstantiator instantiator;

        [SetUp]
        public void SetUp()
        {
            ContextRuntime context = new(0, new RecipeActivator[1], new Lifetime[1]);
            instantiator = new FastIoCPrefabInstantiator(new InstantiationFactory(context));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            created.Clear();
        }

        private GameObject Prefab(bool active)
        {
            GameObject prefab = new("Prefab", typeof(SpawnedView));
            prefab.SetActive(active);
            created.Add(prefab);
            return prefab;
        }

        private GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        [Test]
        public void Instantiate_ReturnsAnActiveCopyOfAnActivePrefab()
        {
            GameObject prefab = Prefab(active: true);
            GameObject copy = Track(instantiator.Instantiate(prefab));

            Assert.IsTrue(copy.activeSelf);
            Assert.IsTrue(prefab.activeSelf, "The prefab must be restored.");
        }

        [Test]
        public void Instantiate_KeepsAnInactivePrefabsCopyInactive()
        {
            GameObject prefab = Prefab(active: false);
            GameObject copy = Track(instantiator.Instantiate(prefab));

            Assert.IsFalse(copy.activeSelf);
            Assert.IsFalse(prefab.activeSelf);
        }

        [Test]
        public void Instantiate_UsesTheParent()
        {
            GameObject parent = Track(new GameObject("Parent"));
            GameObject copy = Track(instantiator.Instantiate(Prefab(active: true), parent.transform));

            Assert.AreSame(parent.transform, copy.transform.parent);
        }

        [Test]
        public void InstantiateGeneric_ReturnsTheComponentOfAnActiveCopy()
        {
            SpawnedView view = instantiator.Instantiate<SpawnedView>(Prefab(active: true));
            Track(view.gameObject);

            Assert.IsNotNull(view);
            Assert.IsTrue(view.gameObject.activeSelf);
        }

        [Test]
        public void InstantiateAndInject_LeavesTheCopyInactiveForTheCaller()
        {
            GameObject prefab = Prefab(active: true);
            GameObject copy = Track(instantiator.InstantiateAndInject(prefab));

            Assert.IsFalse(copy.activeSelf);
            Assert.IsTrue(prefab.activeSelf);
        }
    }
}
