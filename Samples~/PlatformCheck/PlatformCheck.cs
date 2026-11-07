using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FastIoC.API;
using FastIoC.Core;
using FastIoC.UnityIntegration.Runtime.Bootstrap;
using UnityEngine;

namespace FastIoC.Samples.PlatformCheck
{
    /// <summary>
    /// Add this component to any object in your first scene, build for the platform you want to check,
    /// and run the build. Results show on screen and in the log (the browser console on WebGL).
    /// </summary>
    public class PlatformCheck : MonoBehaviour
    {
        internal PlatformCheckService service;
        internal PlatformInitService init;
        internal IPlatformClock clock;
        internal Lazy<IPlatformClock> lazyClock;
        internal IInstantiator instantiator;

        private readonly List<string> lines = new();
        private int failures;
        private bool done;

        private IEnumerator Start()
        {
            // Startup state, to tell a platform problem from a setup problem when injection fails.
            Line($"Startup: bootstrapper started {FastIoCUnityBootstrapper.IsStarted}, generated module {GeneratedModuleRegistry.IsRegistered}, " +
                 $"ProjectContext {ProjectContext.Instance != null}, container {ProjectContext.Instance != null && ProjectContext.Instance.Runtime != null}, " +
                 $"baked scene manifest {FindObjectsByType<SceneInjectionManifest>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0}");

            Check("MonoBehaviour fields injected", service != null && clock != null && instantiator != null);
            Check("Singleton is shared", service != null && ReferenceEquals(service.Clock, clock));
            Check("Constructor Lazy<T> resolves", service?.LazyClock.Value?.Ticks == 7);
            Check("Field Lazy<T> resolves", lazyClock?.Value != null && ReferenceEquals(lazyClock.Value, clock));
            Check("Func<T> creates transients", service != null && service.CreateCounter().Number != service.CreateCounter().Number);
            CheckPrefab();

            // Async initialization gets a few seconds to finish.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (init != null && !init.AsyncInitialized && Time.realtimeSinceStartup < deadline)
                yield return null;

            Check("IInitializable ran", init != null && init.Initialized);
            Check("IAsyncInitializable ran", init != null && init.AsyncInitialized);

            done = true;
            string summary = failures == 0 ? "ALL PASSED" : $"{failures} FAILED";
            Line($"Result: {summary} on {Application.platform}, {Environment()}");
            Debug.Log("[FastIoC Platform Check]\n" + string.Join("\n", lines));
        }

        private void CheckPrefab()
        {
            GameObject template = new("PlatformCheckTemplate", typeof(PlatformCheckSpawned));
            template.SetActive(false);
            try
            {
                PlatformCheckSpawned spawned = instantiator?.Instantiate<PlatformCheckSpawned>(template, transform);
                Check("IInstantiator injects a prefab copy", spawned != null && ReferenceEquals(spawned.clock, clock));
                if (spawned != null)
                    Destroy(spawned.gameObject);
            }
            catch (Exception ex)
            {
                Check($"IInstantiator injects a prefab copy ({ex.GetType().Name}: {ex.Message})", false);
            }
            finally
            {
                Destroy(template);
            }
        }

        private static string Environment()
        {
            string backend =
#if ENABLE_IL2CPP
                "IL2CPP";
#else
                "Mono";
#endif
            string build = Debug.isDebugBuild ? "development build" : "release build";
            return $"{backend}, {build}, Unity {Application.unityVersion}";
        }

        private void Check(string name, bool passed)
        {
            if (!passed)
                failures++;
            Line($"{(passed ? "PASS" : "FAIL")}  {name}");
        }

        private void Line(string text) => lines.Add(text);

        private void OnGUI()
        {
            StringBuilder sb = new("Fast IoC platform check\n\n");
            foreach (string line in lines)
                sb.AppendLine(line);
            if (!done)
                sb.AppendLine("Running...");

            GUIStyle style = new(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 40), wordWrap = true };
            style.normal.textColor = failures == 0 ? Color.white : new Color(1f, 0.5f, 0.5f);
            GUI.Box(new Rect(10, 10, Screen.width - 20, Screen.height - 20), GUIContent.none);
            GUI.Label(new Rect(24, 24, Screen.width - 48, Screen.height - 48), sb.ToString(), style);
        }
    }

    public class PlatformCheckSpawned : MonoBehaviour, IInstanciable
    {
        internal IPlatformClock clock;
    }
}
