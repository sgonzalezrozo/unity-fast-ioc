using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tools.FastIoC.Runtime.Core
{
    /// <summary>
    /// Provides a local scope for newly instantiated components, allowing child components
    /// to resolve references to siblings within the same instantiated hierarchy.
    ///
    /// This component is automatically added to the root of an instantiated prefab
    /// and stores references to the root component and any other locally-scoped dependencies.
    ///
    /// Cleanup: Automatically clears dependencies when the scope is destroyed
    /// to prevent memory leaks and dangling references.
    /// </summary>
    public class InstantiationScope : MonoBehaviour
    {
        private readonly Dictionary<Type, MonoBehaviour> localDependencies = new();

        /// <summary>
        /// Register a component in this scope so child components can resolve it.
        /// </summary>
        public void RegisterLocal<T>(T component) where T : MonoBehaviour
        {
            if (component == null)
                return;

            Type type = typeof(T);
            localDependencies[type] = component;
        }

        /// <summary>
        /// Try to resolve a dependency from the local scope.
        /// </summary>
        public bool TryResolveLocal<T>(out T component) where T : MonoBehaviour
        {
            Type type = typeof(T);
            if (localDependencies.TryGetValue(type, out MonoBehaviour result))
            {
                component = (T)(object)result;
                return component != null;
            }

            component = default;
            return false;
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
            Debug.Log($"[FastIoC] InstantiationScope destroyed - dependencies cleared");
        }
    }
}

