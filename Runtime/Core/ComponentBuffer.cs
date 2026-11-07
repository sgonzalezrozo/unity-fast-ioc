using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastIoC.Core
{
    /// <summary>
    /// Reusable component lists for injecting instantiated hierarchies, so instantiation allocates no arrays.
    /// A small stack of lists supports nested instantiation (a constructor that instantiates a prefab).
    /// Main thread only, like the rest of Unity's object API.
    /// </summary>
    internal sealed class ComponentBuffer : ILocalComponentSource
    {
        private static readonly Stack<ComponentBuffer> s_pool = new();

        public readonly List<MonoBehaviour> Items = new(32);

        public static ComponentBuffer Rent()
        {
            return s_pool.Count > 0 ? s_pool.Pop() : new ComponentBuffer();
        }

        public static void Return(ComponentBuffer buffer)
        {
            buffer.Items.Clear();
            s_pool.Push(buffer);
        }

        /// <summary>Local references resolve to a component of the same hierarchy, searched in the buffer.</summary>
        public MonoBehaviour ResolveLocal(Type componentType)
        {
            if (componentType == null)
                return null;

            for (int i = 0; i < Items.Count; i++)
            {
                MonoBehaviour item = Items[i];
                if (item != null && componentType.IsInstanceOfType(item))
                    return item;
            }

            return null;
        }
    }
}
