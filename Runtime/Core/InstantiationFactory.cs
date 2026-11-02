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
            GeneratedInjectorsCache.Initialize();
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

            foreach (MonoBehaviour mb in allMonoBehaviours)
            {
                if (data != null)
                    InjectableDataHandler.TrySetInjectableData(mb, data);
            }

            InstantiationScope scope = InstantiationScope.GetOrCreate(root);

            foreach (MonoBehaviour mb in allMonoBehaviours)
            {
                if (mb == null)
                    continue;

                Type mbType = mb.GetType();
                System.Reflection.MethodInfo registerMethod = typeof(InstantiationScope)
                    .GetMethod("RegisterLocal")
                    ?.MakeGenericMethod(mbType);

                registerMethod?.Invoke(scope, new object[] { mb });
            }

            InjectAllMonoBehaviours(allMonoBehaviours);

            LocalComponentResolver.InjectLocalReferencesWithScope(root, allMonoBehaviours);
        }

        private void InjectAllMonoBehaviours(MonoBehaviour[] allMonoBehaviours)
        {
            System.Reflection.MethodInfo injectMethod = GeneratedInjectorsCache.GetInjectMethod();
            if (injectMethod == null)
            {
                Debug.LogWarning("[FastIoC] GeneratedMonoInjectors.Inject method not available. Injection skipped.");
                return;
            }

            foreach (MonoBehaviour monoBehaviour in allMonoBehaviours)
            {
                if (monoBehaviour == null)
                    continue;

                try
                {
                    injectMethod.Invoke(null, new object[] { monoBehaviour, context });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FastIoC] Error injecting {monoBehaviour.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        }
    }
}

