using Tools.FastIoC.Runtime.API;
using UnityEngine;

namespace Tools.FastIoC.UnityIntegration.Runtime.Bootstrap
{
    public static class ProjectContextHelper
    {
        private const string PROJECT_CONTEXT_PREFAB_PATH = "Tools/FastIoC/ProjectContext";
        private static ProjectContext s_cachedInstance;

        public static ProjectContext LoadOrCreateProjectContext()
        {
            if (s_cachedInstance != null)
                return s_cachedInstance;

            ProjectContext existing = Object.FindFirstObjectByType<ProjectContext>();
            if (existing != null)
            {
                s_cachedInstance = existing;
                return existing;
            }

            GameObject prefab = Resources.Load<GameObject>(PROJECT_CONTEXT_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[FastIoC] ProjectContext prefab not found at Resources/{PROJECT_CONTEXT_PREFAB_PATH}.prefab\n" +
                    "Expected location: Assets/Resources/Tools/FastIoC/ProjectContext.prefab\n" +
                    "Run 'Tools > FastIoC > Setup Project' to create it.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = "ProjectContext";

            ProjectContext context = instance.GetComponent<ProjectContext>();
            if (context == null)
            {
                Debug.LogError("[FastIoC] ProjectContext component not found on instantiated prefab");
                Object.DestroyImmediate(instance);
                return null;
            }

            s_cachedInstance = context;
            return context;
        }

        public static ProjectContext GetCachedInstance()
            => s_cachedInstance ?? ProjectContext.Instance;

        internal static void ClearCache()
        {
            s_cachedInstance = null;
        }
    }
}
