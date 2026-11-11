using FastIoC.API;
using UnityEngine;

namespace FastIoC.UnityIntegration.Runtime.Bootstrap
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

            long phase = FastIoC.Core.BootTiming.Now();
            GameObject prefab = Resources.Load<GameObject>(PROJECT_CONTEXT_PREFAB_PATH);
            FastIoC.Core.BootTiming.Span("Bootstrap: load ProjectContext prefab", phase);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[FastIoC] ProjectContext prefab not found at Resources/{PROJECT_CONTEXT_PREFAB_PATH}.prefab\n" +
                    "Expected location: Assets/Resources/Tools/FastIoC/ProjectContext.prefab\n" +
                    "Run 'Tools > FastIoC > Setup Project' to create it.");
                return null;
            }

            // ProjectContext.Awake runs inside Instantiate: its own lines break this down.
            phase = FastIoC.Core.BootTiming.Now();
            GameObject instance = Object.Instantiate(prefab);
            FastIoC.Core.BootTiming.Span("Bootstrap: instantiate ProjectContext (includes Awake)", phase);
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
