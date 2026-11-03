#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FastIoC.API;
using FastIoC.Editor.Graph;

namespace FastIoC.Editor.Generation
{
    public static class InstanceBindingsBuilder
    {
        public static Dictionary<Type, bool> BuildInstanceBindingsMap(Dictionary<Type, List<Type>> installerBindings)
        {
            Dictionary<Type, bool> result = new();

            foreach (KeyValuePair<Type, BindingInfo> binding in InstallerGraphCollector.GetCachedBindingInfo())
            {
                if (binding.Value.IsInstance)
                    result[binding.Value.ImplementationType] = true;
            }

            return result;
        }
    }
}
#endif

