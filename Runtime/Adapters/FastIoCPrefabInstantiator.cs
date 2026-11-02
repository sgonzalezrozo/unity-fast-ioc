using Tools.Core;
using Tools.FastIoC.Runtime.Core;
using UnityEngine;

namespace Tools.FastIoC.Runtime.Adapters
{
    public class FastIoCPrefabInstantiator : IPrefabInstantiator
    {
        private readonly IInstantiator instantiator;
        private ContextRuntime contextRuntime;

        private System.Reflection.MethodInfo cachedGenericInstantiateMethod;

        public FastIoCPrefabInstantiator(IInstantiator instantiator)
        {
            this.instantiator = instantiator ?? throw new System.ArgumentNullException(nameof(instantiator));
            GeneratedInjectorsCache.Initialize();
            contextRuntime = GetContextRuntimeFromProjectContext();

            cachedGenericInstantiateMethod = instantiator.GetType().GetMethod(
                "Instantiate",
                1,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null,
                new System.Type[] { typeof(object), typeof(Transform), typeof(object) },
                null);
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
                if (cachedGenericInstantiateMethod != null)
                {
                    MonoBehaviour[] components = prefab.GetComponents<MonoBehaviour>();

                    foreach (MonoBehaviour component in components)
                    {
                        bool isCoreInstantiable = component is IInstantiable;
                        bool isFastIoCInstantiable = component is IInstanciable;
                        if (!isCoreInstantiable && !isFastIoCInstantiable)
                            continue;

                        try
                        {
                            System.Reflection.MethodInfo instantiateMethod = cachedGenericInstantiateMethod.MakeGenericMethod(component.GetType());
                            object instance = instantiateMethod.Invoke(instantiator, new object[] { component, parent, data });

                            if (instance is MonoBehaviour mb)
                            {
                                prefab.SetActive(prefabWasActive);
                                return mb.gameObject;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[FastIoCPrefabInstantiator] FastIoC instantiation failed for {component.GetType().Name}: {ex.Message}. Falling back to standard instantiation.");
                        }
                    }
                }

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
                T component = prefab.GetComponent<T>();

                // Only attempt FastIoC instantiation if we have a cached method and component is marked as instantiable.
                bool isCoreInstantiableType = typeof(IInstantiable).IsAssignableFrom(typeof(T));
                bool isFastIoCInstantiableType = typeof(IInstanciable).IsAssignableFrom(typeof(T));
                if ((isCoreInstantiableType || isFastIoCInstantiableType) && cachedGenericInstantiateMethod != null && typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
                {
                    try
                    {
                        System.Reflection.MethodInfo instantiateMethod = cachedGenericInstantiateMethod.MakeGenericMethod(typeof(T));
                        object instance = instantiateMethod.Invoke(instantiator, new object[] { component, parent, data });
                        prefab.SetActive(prefabWasActive);

                        return instance as T;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[FastIoCPrefabInstantiator] FastIoC instantiation failed for {typeof(T).Name}: {ex.Message}. Falling back to standard instantiation.");
                    }
                }

                // Fallback: Standard instantiation with manual injection
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
            if (contextRuntime == null)
            {
                Debug.LogWarning("[FastIoCPrefabInstantiator] ContextRuntime not available. Skipping all injection.");
                return;
            }

            System.Reflection.MethodInfo injectMethod = GeneratedInjectorsCache.GetInjectMethod();
            if (injectMethod == null)
            {
                Debug.LogWarning("[FastIoCPrefabInstantiator] GeneratedMonoInjectors.Inject method not available. Injection skipped.");
                return;
            }

            MonoBehaviour[] monoBehaviours = gameObject.GetComponentsInChildren<MonoBehaviour>(true);

            object[] injectParams = new object[] { null, contextRuntime };

            for (int i = 0; i < monoBehaviours.Length; i++)
            {
                MonoBehaviour mb = monoBehaviours[i];
                if (mb == null)
                    continue;

                if (data != null)
                    InjectableDataHandler.TrySetInjectableData(mb, data);

                try
                {
                    injectParams[0] = mb;
                    injectMethod.Invoke(null, injectParams);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FastIoCPrefabInstantiator] Container injection failed for {mb.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            LocalComponentResolver.InjectLocalReferencesFromHierarchy(gameObject, monoBehaviours);
        }

        private ContextRuntime GetContextRuntimeFromProjectContext()
        {
            try
            {
                System.Type projectContextType = ReflectionUtility.ResolveType(FastIoCConstants.PROJECT_CONTEXT_TYPE);
                if (projectContextType == null)
                {
                    Debug.LogError("[FastIoCPrefabInstantiator] ProjectContext type not found in any assembly!");
                    return null;
                }

                System.Reflection.PropertyInfo instanceProperty = ReflectionUtility.GetPublicStaticProperty(projectContextType, FastIoCConstants.METHOD_INSTANCE);
                if (instanceProperty == null)
                {
                    Debug.LogError("[FastIoCPrefabInstantiator] ProjectContext.Instance property not found!");
                    return null;
                }

                object projectContextInstance = ReflectionUtility.GetStaticPropertyValue(instanceProperty, "GetProjectContextInstance");
                if (projectContextInstance == null)
                {
                    Debug.LogError("[FastIoCPrefabInstantiator] ProjectContext.Instance is NULL!");
                    return null;
                }

                System.Reflection.PropertyInfo runtimeProperty = ReflectionUtility.GetPublicInstanceProperty(projectContextType, FastIoCConstants.METHOD_RUNTIME);
                if (runtimeProperty == null)
                {
                    Debug.LogError("[FastIoCPrefabInstantiator] ProjectContext.Runtime property not found!");
                    return null;
                }

                object runtimeObj = runtimeProperty.GetValue(projectContextInstance);
                if (runtimeObj is ContextRuntime ctx)
                    return ctx;

                Debug.LogError("[FastIoCPrefabInstantiator] ProjectContext.Runtime is not a ContextRuntime instance!");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FastIoCPrefabInstantiator] Error getting ContextRuntime: {ex.Message}");
                return null;
            }
        }
    }
}
