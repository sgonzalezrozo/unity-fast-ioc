using System;
using UnityEngine;

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

            bool prefabWasActive = prefab.activeSelf;
            prefab.SetActive(false);

            try
            {
                GameObject instance = parent != null
                    ? UnityEngine.Object.Instantiate(prefab, parent)
                    : UnityEngine.Object.Instantiate(prefab);

                try
                {
                    T rootComponent = instance.GetComponent<T>();
                    if (rootComponent == null)
                        throw new InvalidOperationException($"Prefab does not contain component of type {typeof(T).Name}");

                    InjectInstance(instance, data);
                    instance.SetActive(true);

                    return rootComponent;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FastIoC] Error during instantiation: {ex.Message}\n{ex.StackTrace}");
                    UnityEngine.Object.Destroy(instance);
                    throw;
                }
            }
            finally
            {
                prefab.SetActive(prefabWasActive);
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
            MonoBehaviour[] allMonoBehaviours = root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);

            InstantiationScope scope = InstantiationScope.GetOrCreate(root);
            for (int i = 0; i < allMonoBehaviours.Length; i++)
                scope.RegisterLocal(allMonoBehaviours[i]);

            InjectionRunner.InjectHierarchy(allMonoBehaviours, context, data, scope);
        }
    }
}
