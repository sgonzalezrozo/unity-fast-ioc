using System;
using ToolsCore;
using FastIoC.API;
using FastIoC.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FastIoC.Adapters
{
    /// <summary>
    /// <see cref="IPrefabInstantiator"/> for tools that don't know about Fast IoC.
    /// The copy is created inactive, injected, then given the prefab's own active state,
    /// so Awake and OnEnable run after injection and the result matches <c>Object.Instantiate</c>.
    /// </summary>
    public class FastIoCPrefabInstantiator : IPrefabInstantiator
    {
        private ContextRuntime contextRuntime;

        // IInstantiator stays in the constructor so the generated dependency graph is unchanged.
        public FastIoCPrefabInstantiator(IInstantiator instantiator)
        {
            if (instantiator == null)
                throw new ArgumentNullException(nameof(instantiator));
        }

        private ContextRuntime Context
        {
            get
            {
                if (contextRuntime == null)
                    contextRuntime = ProjectContext.Instance != null ? ProjectContext.Instance.Runtime : null;

                return contextRuntime;
            }
        }

        public GameObject Instantiate(GameObject prefab, Transform parent = null)
        {
            return Spawn(prefab, parent, null, activate: true);
        }

        public GameObject Instantiate(GameObject prefab, Transform parent, object data)
        {
            return Spawn(prefab, parent, data, activate: true);
        }

        /// <summary>
        /// Instantiates and injects the prefab but leaves the copy inactive, so the caller can bind data
        /// and activate it. Awake and OnEnable run when the caller activates it.
        /// </summary>
        public GameObject InstantiateAndInject(GameObject prefab, Transform parent = null)
        {
            return Spawn(prefab, parent, null, activate: false);
        }

        public T Instantiate<T>(GameObject prefab, Transform parent = null) where T : Component
        {
            return Instantiate<T>(prefab, parent, data: null);
        }

        public T Instantiate<T>(GameObject prefab, Transform parent, object data) where T : Component
        {
            GameObject instance = Spawn(prefab, parent, data, activate: true);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        private GameObject Spawn(GameObject prefab, Transform parent, object data, bool activate)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            // Create the copy inactive and restore the prefab right away, before injection runs.
            bool prefabWasActive = prefab.activeSelf;
            if (prefabWasActive)
                prefab.SetActive(false);

            GameObject instance;
            try
            {
                instance = parent != null ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab);
            }
            finally
            {
                if (prefabWasActive)
                    prefab.SetActive(true);
            }

            try
            {
                Inject(instance, data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error injecting '{prefab.name}': {ex.Message}\n{ex.StackTrace}");
                Object.Destroy(instance);
                throw;
            }

            if (activate && prefabWasActive)
                instance.SetActive(true);

            return instance;
        }

        private void Inject(GameObject root, object data)
        {
            ContextRuntime context = Context;
            if (context == null)
            {
                Debug.LogWarning("[FastIoCPrefabInstantiator] ContextRuntime not available. Skipping all injection.");
                return;
            }

            ComponentBuffer buffer = ComponentBuffer.Rent();
            try
            {
                root.GetComponentsInChildren(true, buffer.Items);

                IGeneratedModule module = GeneratedModuleRegistry.Module;
                bool needsLocal = module is not IGeneratedModuleFeatures features || features.HasLocalInjectors;
                InjectionRunner.InjectHierarchy(buffer.Items, context, data, needsLocal ? buffer : null);
            }
            finally
            {
                ComponentBuffer.Return(buffer);
            }
        }
    }
}
