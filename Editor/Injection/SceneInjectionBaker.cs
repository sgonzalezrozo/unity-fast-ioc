#if UNITY_EDITOR
using System.Collections.Generic;
using FastIoC.API;
using FastIoC.Core;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastIoC.Editor.Injection
{
    /// <summary>
    /// Bakes a SceneInjectionManifest into every scene: serialized references to the injectable
    /// MonoBehaviours already placed in the scene, plus the index of their generated injector.
    /// At runtime the scene is injected by walking two arrays: no FindObjectsByType, no type lookups,
    /// no reflection.
    ///
    /// Unity calls this for each scene in a player build, and in the editor for each scene loaded
    /// in Play Mode. It always works on a copy in memory, so the scene asset is never modified.
    /// </summary>
    internal sealed class SceneInjectionBaker : IProcessSceneWithReport
    {
        private const string MANIFEST_OBJECT_NAME = "[FastIoC] SceneInjectionManifest";

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            IGeneratedModule module = GeneratedModuleRegistry.Module;
            if (module == null)
            {
                // No generated code yet: the runtime falls back to scanning the scene.
                return;
            }

            List<MonoBehaviour> targets = new();
            List<int> injectorIds = new();
            List<MonoBehaviour> buffer = new();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<SceneInjectionManifest>() != null)
                    return; // already baked

                buffer.Clear();
                root.GetComponentsInChildren(true, buffer);

                foreach (MonoBehaviour monoBehaviour in buffer)
                {
                    // ProjectContext is skipped like in the runtime scan.
                    if (monoBehaviour == null || monoBehaviour is ProjectContext)
                        continue;

                    int injectorId = module.GetInjectorId(monoBehaviour.GetType());
                    if (injectorId < 0)
                        continue;

                    targets.Add(monoBehaviour);
                    injectorIds.Add(injectorId);
                }
            }

            // The manifest is added even when it is empty: it tells the runtime the scene
            // was baked and has nothing to inject, so it doesn't fall back to a scan.
            GameObject manifestObject = new(MANIFEST_OBJECT_NAME)
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector
            };
            SceneManager.MoveGameObjectToScene(manifestObject, scene);

            SceneInjectionManifest manifest = manifestObject.AddComponent<SceneInjectionManifest>();
            manifest.SetEntries(targets.ToArray(), injectorIds.ToArray());
        }
    }
}
#endif
