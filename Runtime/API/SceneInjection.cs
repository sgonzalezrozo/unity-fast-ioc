using System.Collections.Generic;
using FastIoC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastIoC.API
{
    /// <summary>
    /// Injects the MonoBehaviours of loaded scenes.
    /// Uses the SceneInjectionManifest baked into each scene (serialized references, no lookups).
    /// If a scene has no manifest (for example Play Mode with scene reload disabled), it falls back to
    /// FindObjectsByType plus the generated type-to-injector map. Neither path uses reflection.
    /// </summary>
    public static class SceneInjection
    {
        private static readonly HashSet<int> s_injectedScenes = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_injectedScenes.Clear();
        }

        /// <summary>Injects every loaded scene that hasn't been injected yet.</summary>
        public static void InjectLoadedScenes(ContextRuntime context)
            => Inject(context, filter: null);

        /// <summary>Injects <paramref name="scene"/> unless it has already been injected.</summary>
        public static void InjectScene(Scene scene, ContextRuntime context)
            => Inject(context, scene);

        /// <summary>Forgets a scene so it is injected again if it is loaded again.</summary>
        public static void MarkUnloaded(Scene scene)
        {
            s_injectedScenes.Remove(scene.handle);
        }

        private static void Inject(ContextRuntime context, Scene? filter)
        {
            if (context == null)
                return;

            if (!GeneratedModuleRegistry.IsRegistered)
            {
                Debug.LogWarning("[FastIoC] Generated code is not registered. Run 'Tools > FastIoC > Regenerate Code'.");
                return;
            }

            HashSet<int> injectedNow = new();

            SceneInjectionManifest[] manifests = Object.FindObjectsByType<SceneInjectionManifest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SceneInjectionManifest manifest in manifests)
            {
                Scene scene = manifest.gameObject.scene;
                if (!ShouldInject(scene, filter, injectedNow))
                    continue;

                injectedNow.Add(scene.handle);
                manifest.InjectAll(context);
            }

            // A manifest covers its whole scene. Only scan when a scene we need has no manifest.
            bool needsScan = filter.HasValue ? !injectedNow.Contains(filter.Value.handle) && !s_injectedScenes.Contains(filter.Value.handle) : injectedNow.Count == 0;
            if (needsScan)
                ScanAndInject(context, filter, injectedNow);

            foreach (int handle in injectedNow)
                s_injectedScenes.Add(handle);
        }

        private static bool ShouldInject(Scene scene, Scene? filter, HashSet<int> injectedNow)
        {
            if (filter.HasValue && scene != filter.Value)
                return false;

            return !s_injectedScenes.Contains(scene.handle) || injectedNow.Contains(scene.handle);
        }

        private static void ScanAndInject(ContextRuntime context, Scene? filter, HashSet<int> injectedNow)
        {
            HashSet<int> manifestScenes = new(injectedNow);
            IGeneratedModule module = GeneratedModuleRegistry.Module;

            MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MonoBehaviour mb in all)
            {
                if (mb == null || mb is ProjectContext)
                    continue;

                Scene scene = mb.gameObject.scene;
                if (manifestScenes.Contains(scene.handle))
                    continue;

                if (!ShouldInject(scene, filter, injectedNow))
                    continue;

                injectedNow.Add(scene.handle);

                int injectorId = module.GetInjectorId(mb.GetType());
                if (injectorId >= 0)
                    InjectionRunner.InjectAt(injectorId, mb, context);
            }
        }
    }
}
