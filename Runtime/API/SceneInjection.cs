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

        // Objects injected while their scene was still loading (the first scene, injected from ProjectContext
        // before Awake). Their scene isn't marked as injected until it finishes loading, so objects that weren't
        // reachable yet still get injected then; these are skipped so nothing is injected twice.
        private static readonly HashSet<int> s_injectedEarly = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_injectedScenes.Clear();
            s_injectedEarly.Clear();
        }

        /// <summary>Injects every loaded scene that hasn't been injected yet.</summary>
        public static void InjectLoadedScenes(ContextRuntime context)
            => Inject(context, filter: null);

        /// <summary>Injects <paramref name="scene"/> unless it has already been injected.</summary>
        public static void InjectScene(Scene scene, ContextRuntime context)
        {
            // A loaded scene is injected from its own root objects: no search over every object
            // in memory and no allocation. Scenes not fully loaded yet take the general path.
            if (context == null || !scene.IsValid() || !scene.isLoaded)
            {
                Inject(context, scene);
                return;
            }

            if (s_injectedScenes.Contains(scene.handle))
                return;

            if (!GeneratedModuleRegistry.IsRegistered)
            {
                Debug.LogWarning("[FastIoC] Generated code is not registered. Run 'Tools > FastIoC > Regenerate Code'.");
                return;
            }

            s_roots.Clear();
            scene.GetRootGameObjects(s_roots);

            SceneInjectionManifest manifest = null;
            for (int i = 0; i < s_roots.Count; i++)
            {
                if (s_roots[i].TryGetComponent(out manifest))
                    break;
            }

            HashSet<int> skip = s_injectedEarly.Count > 0 ? s_injectedEarly : null;
            if (manifest != null)
                manifest.InjectAll(context, skip, null);
            else
                ScanRoots(context, skip);

            s_roots.Clear();
            s_injectedScenes.Add(scene.handle);
            s_injectedEarly.Clear();
        }

        private static readonly List<GameObject> s_roots = new();
        private static readonly List<MonoBehaviour> s_scanBuffer = new();

        private static void ScanRoots(ContextRuntime context, HashSet<int> skip)
        {
            IGeneratedModule module = GeneratedModuleRegistry.Module;
            for (int r = 0; r < s_roots.Count; r++)
            {
                s_scanBuffer.Clear();
                s_roots[r].GetComponentsInChildren(true, s_scanBuffer);
                for (int i = 0; i < s_scanBuffer.Count; i++)
                {
                    MonoBehaviour mb = s_scanBuffer[i];
                    if (mb == null || mb is ProjectContext)
                        continue;

                    if (skip != null && skip.Contains(mb.GetInstanceID()))
                        continue;

                    int injectorId = module.GetInjectorId(mb.GetType());
                    if (injectorId >= 0)
                        InjectionRunner.InjectAt(injectorId, mb, context);
                }
            }

            s_scanBuffer.Clear();
        }

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
            HashSet<int> loadingScenes = new();

            SceneInjectionManifest[] manifests = Object.FindObjectsByType<SceneInjectionManifest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SceneInjectionManifest manifest in manifests)
            {
                Scene scene = manifest.gameObject.scene;
                if (!ShouldInject(scene, filter, injectedNow))
                    continue;

                if (scene.isLoaded)
                {
                    injectedNow.Add(scene.handle);
                    manifest.InjectAll(context);
                }
                else
                {
                    // Still loading (ProjectContext starts before the first scene finishes loading):
                    // inject what's there now, and finish when the scene's load event arrives.
                    manifest.InjectAll(context, s_injectedEarly, s_injectedEarly);
                    loadingScenes.Add(scene.handle);
                }
            }

            // A manifest covers its whole scene. Only scan when a scene we need has no manifest.
            bool needsScan = filter.HasValue
                ? !injectedNow.Contains(filter.Value.handle) && !loadingScenes.Contains(filter.Value.handle) && !s_injectedScenes.Contains(filter.Value.handle)
                : injectedNow.Count == 0 && loadingScenes.Count == 0;
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

                int injectorId = module.GetInjectorId(mb.GetType());

                if (!scene.isLoaded)
                {
                    // The scene is still loading: inject this object now, and leave the scene unmarked so
                    // its load event injects whatever wasn't reachable yet.
                    if (injectorId >= 0 && s_injectedEarly.Add(mb.GetInstanceID()))
                        InjectionRunner.InjectAt(injectorId, mb, context);
                    continue;
                }

                injectedNow.Add(scene.handle);
                if (injectorId >= 0)
                    InjectionRunner.InjectAt(injectorId, mb, context);
            }
        }
    }
}
