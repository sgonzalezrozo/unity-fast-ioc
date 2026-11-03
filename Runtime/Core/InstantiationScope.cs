using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastIoC.Core
{
    /// <summary>
    /// Provides a local scope for newly instantiated components, allowing child components
    /// to resolve references to siblings within the same instantiated hierarchy.
    ///
    /// This component is automatically added to the root of an instantiated prefab
    /// and stores references to the components of that hierarchy, keyed by their exact type.
    ///
    /// Cleanup: Automatically clears dependencies when the scope is destroyed
    /// to prevent memory leaks and dangling references.
    /// </summary>
    public class InstantiationScope : MonoBehaviour, ILocalComponentSource
    {
        private readonly Dictionary<Type, MonoBehaviour> localDependencies = new();

        /// <summary>
        /// Register a component in this scope under its runtime type.
        /// </summary>
        public void RegisterLocal(MonoBehaviour component)
        {
            if (component == null)
                return;

            localDependencies[component.GetType()] = component;
        }

        /// <summary>
        /// Register a component in this scope under <typeparamref name="T"/>.
        /// </summary>
        public void RegisterLocal<T>(T component) where T : MonoBehaviour
        {
            if (component == null)
                return;

            localDependencies[typeof(T)] = component;
        }

        /// <summary>
        /// Resolve a component registered under <paramref name="componentType"/>, or null.
        /// </summary>
        public MonoBehaviour ResolveLocal(Type componentType)
        {
            if (componentType != null && localDependencies.TryGetValue(componentType, out MonoBehaviour result) && result != null)
                return result;

            return null;
        }

        /// <summary>
        /// Try to resolve a dependency from the local scope.
        /// </summary>
        public bool TryResolveLocal<T>(out T component) where T : MonoBehaviour
        {
            component = ResolveLocal(typeof(T)) as T;
            return component != null;
        }

        /// <summary>
        /// Find or create the InstantiationScope on a GameObject.
        /// </summary>
        public static InstantiationScope GetOrCreate(GameObject gameObject)
        {
            InstantiationScope scope = gameObject.GetComponent<InstantiationScope>();
            if (scope == null)
                scope = gameObject.AddComponent<InstantiationScope>();

            return scope;
        }

        /// <summary>
        /// Clean up scope dependencies when destroyed.
        /// Prevents memory leaks from dangling references.
        /// </summary>
        private void OnDestroy()
        {
            localDependencies.Clear();
        }
    }
}
