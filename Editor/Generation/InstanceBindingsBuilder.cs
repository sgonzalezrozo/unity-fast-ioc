#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Tools.FastIoC.Editor.Generation
{
    public static class InstanceBindingsBuilder
    {
        public static Dictionary<Type, bool> BuildInstanceBindingsMap(Dictionary<Type, List<Type>> installerBindings)
        {
            Dictionary<Type, bool> result = new();
            return result;
        }
    }
}
#endif

