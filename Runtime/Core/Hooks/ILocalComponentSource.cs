using System;
using UnityEngine;

namespace FastIoC.Core
{
    /// <summary>
    /// Supplies components from the hierarchy being instantiated, for local (sibling/child) references.
    /// </summary>
    public interface ILocalComponentSource
    {
        /// <summary>Returns a component assignable to <paramref name="componentType"/>, or null.</summary>
        MonoBehaviour ResolveLocal(Type componentType);
    }

    /// <summary>
    /// Resolves local references by searching the instantiated hierarchy (GetComponentInChildren).
    /// </summary>
    public sealed class HierarchyComponentSource : ILocalComponentSource
    {
        private readonly GameObject root;

        public HierarchyComponentSource(GameObject root)
        {
            this.root = root;
        }

        public MonoBehaviour ResolveLocal(Type componentType)
        {
            if (root == null || componentType == null)
                return null;

            return root.GetComponentInChildren(componentType, includeInactive: true) as MonoBehaviour;
        }
    }
}
