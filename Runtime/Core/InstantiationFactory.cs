using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FastIoC.Core
{
    public class InstantiationFactory : IInstantiator
    {
        private readonly ContextRuntime context;

        public InstantiationFactory(ContextRuntime context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public T Instantiate<T>(GameObject prefab, Transform parent = null, object data = null) where T : MonoBehaviour, IInstanciable
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            // The copy is created inactive so its Awake/OnEnable run after injection.
            // The prefab is restored right after the copy, before injection runs.
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
                if (!instance.TryGetComponent(out T rootComponent))
                    throw new InvalidOperationException($"Prefab does not contain component of type {typeof(T).Name}");

                InjectInstance(instance, data);
                instance.SetActive(true);
                return rootComponent;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error during instantiation: {ex.Message}\n{ex.StackTrace}");
                Object.Destroy(instance);
                throw;
            }
        }

        public T Instantiate<T>(T prefabComponent, Transform parent = null, object data = null) where T : MonoBehaviour, IInstanciable
        {
            if (prefabComponent == null)
                throw new ArgumentNullException(nameof(prefabComponent));

            return Instantiate<T>(prefabComponent.gameObject, parent, data);
        }

        private void InjectInstance(GameObject root, object data)
        {
            ComponentBuffer buffer = ComponentBuffer.Rent();
            try
            {
                root.GetComponentsInChildren(true, buffer.Items);

                // Local references resolve against the same hierarchy. The buffer is the source,
                // so no component or dictionary is added to the instance.
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
