using ToolsCore;
using FastIoC.API;
using FastIoC.Core;
using UnityEngine;

namespace FastIoC.Adapters
{
    public class FastIoCPrefabInstantiator : IPrefabInstantiator
    {
        private ContextRuntime contextRuntime;

        // IInstantiator stays in the constructor so the generated dependency graph is unchanged.
        public FastIoCPrefabInstantiator(IInstantiator instantiator)
        {
            if (instantiator == null)
                throw new System.ArgumentNullException(nameof(instantiator));
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
            return Instantiate(prefab, parent, data: null);
        }

        public GameObject Instantiate(GameObject prefab, Transform parent, object data)
        {
            if (prefab == null)
                throw new System.ArgumentNullException(nameof(prefab));

            bool prefabWasActive = prefab.activeSelf;
            prefab.SetActive(false);

            try
            {
                GameObject instantiated = Object.Instantiate(prefab, parent);
                InjectMonoBehaviours(instantiated, data);
                prefab.SetActive(prefabWasActive);

                return instantiated;
            }
            finally
            {
                prefab.SetActive(prefabWasActive);
            }
        }

        public GameObject InstantiateAndInject(GameObject prefab, Transform parent = null)
        {
            GameObject instance = Object.Instantiate(prefab, parent);
            InjectMonoBehaviours(instance);
            // DO NOT activate here - let caller decide when to activate (after data binding)
            return instance;
        }

        public T Instantiate<T>(GameObject prefab, Transform parent = null) where T : Component
        {
            return Instantiate<T>(prefab, parent, data: null);
        }

        public T Instantiate<T>(GameObject prefab, Transform parent, object data) where T : Component
        {
            if (prefab == null)
                throw new System.ArgumentNullException(nameof(prefab));

            bool prefabWasActive = prefab.activeSelf;
            prefab.SetActive(false);

            try
            {
                T instantiatedComponent = Object.Instantiate(prefab, parent)?.GetComponent<T>();
                if (instantiatedComponent is MonoBehaviour mbInstance)
                    InjectMonoBehaviours(mbInstance.gameObject, data);

                prefab.SetActive(prefabWasActive);

                return instantiatedComponent;
            }
            finally
            {
                prefab.SetActive(prefabWasActive);
            }
        }

        private void InjectMonoBehaviours(GameObject gameObject, object data = null)
        {
            ContextRuntime context = Context;
            if (context == null)
            {
                Debug.LogWarning("[FastIoCPrefabInstantiator] ContextRuntime not available. Skipping all injection.");
                return;
            }

            MonoBehaviour[] monoBehaviours = gameObject.GetComponentsInChildren<MonoBehaviour>(true);
            InjectionRunner.InjectHierarchy(monoBehaviours, context, data, new HierarchyComponentSource(gameObject));
        }
    }
}
