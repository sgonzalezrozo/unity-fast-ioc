using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastIoCBench
{
    /// <summary>
    /// Runs one variant per launch: build the global container, load the injected scene,
    /// reload it, spawn prefabs, write the result and quit. A shell loop relaunches the app
    /// until the plan in persistentDataPath is done.
    /// Abstract because Unity can't attach scripts from a folder ending in '~' (Benchmarks~):
    /// the generator writes the concrete FastIoCBench.Gen.BenchBoot into Assets.
    /// </summary>
    public abstract class BenchBootBase : MonoBehaviour
    {
        public const int RELOAD_CYCLES = 5;
        public const int PREFAB_INSTANCES = 1000;
        public const int PREFAB_BATCH = 50;
        public const int FRAMES_BEFORE_SPAWN = 10;
        public const string BASELINE = "none";

        private string status = "";
        private bool showStatus;

        protected IEnumerator Start()
        {
            long bootStart = BenchClock.Now;
            double bootRealtimeMs = Time.realtimeSinceStartupAsDouble * 1000.0;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Max(120, Mathf.CeilToInt((float)Screen.currentResolution.refreshRateRatio.value));
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            IBenchFixture fixture = BenchRegistry.Fixture;
            BenchBuildInfo info = BenchBuildInfo.Load();
            if (fixture == null || info == null)
            {
                Finish(fixture == null
                    ? "No generated fixture. Run Tools > FastIoC Benchmark > Generate."
                    : "No build info. Build from the benchmark window, or enter Play Mode once from the Editor.");
                yield break;
            }

            BenchPlan plan = BenchPlan.LoadOrCreate(info.buildId, fixture.ProfileName, info.rounds, fixture.Variants);
            if (plan.IsDone)
            {
                Finish($"Plan finished: {plan.launchesCompleted}/{plan.TotalLaunches} launches completed. Results in {BenchPlan.Folder}");
                yield break;
            }

            int launch = plan.launchesStarted;
            string variant = plan.VariantOf(launch);
            int runIndex = plan.RoundOf(launch) + 1;
            plan.launchesStarted++;
            plan.Save();

            List<BenchMetric> metrics = new();
            metrics.Add("boot-start", BenchClock.Ms(BenchClock.T0, bootStart), "ms");
            metrics.Add("boot-realtime", bootRealtimeMs, "ms");

            // --- Cold start: build the container, then load the scene that resolves the whole graph.
            long buildStart = BenchClock.Now;
            fixture.BuildContainer(variant);
            long buildEnd = BenchClock.Now;

            string sceneName = fixture.SceneName(variant);
            BenchSceneMarkerBase.ResetMarks();
            long loadStart = BenchClock.Now;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            while (!BenchSceneMarkerBase.IsReady)
                yield return null;

            long ready = BenchSceneMarkerBase.Ready;
            long firstAwake = BenchSceneMarkerBase.FirstAwake;
            bool isBaseline = variant == BASELINE;
            long heapAfterStartup = GC.GetTotalMemory(false);
            int gen0AfterStartup = GC.CollectionCount(0);

            metrics.Add("container-build", BenchClock.Ms(buildStart, buildEnd), "ms");
            metrics.Add("scene-first-inject", BenchClock.Ms(BenchSceneMarkerBase.FirstAwake, ready), "ms");
            metrics.Add("scene-first-load", BenchClock.Ms(loadStart, ready), "ms");
            metrics.Add("scene-first-preawake", BenchClock.Ms(loadStart, firstAwake), "ms");
            metrics.Add("startup-total", BenchClock.Ms(BenchClock.T0, ready), "ms");
            metrics.Add("startup-heap-growth", heapAfterStartup - BenchClock.HeapAtT0, "bytes");
            metrics.Add("startup-gc-collections", gen0AfterStartup - BenchClock.Gen0AtT0, "count");

            Scene scene = SceneManager.GetSceneByName(sceneName);
            // The baseline injects nothing, so there is no graph to check.
            BenchVerifier.Result check = isBaseline ? default : BenchVerifier.Verify(scene);
            bool graphValid = isBaseline || IsValid(check, fixture);
            if (!isBaseline)
            {
                metrics.Add("graph-valid", graphValid ? 1 : 0, "count");
                metrics.Add("distinct-instances", check.DistinctInstances, "count");
                metrics.Add("injected-fields", check.InjectedFields, "count");
                metrics.Add("null-fields", check.NullFields, "count");
            }

            if (!graphValid)
                Debug.LogError($"[FastIoCBench] {variant}: wrong graph. hash {check.Hash} (expected {fixture.ExpectedGraphHash}), instances {check.DistinctInstances} (expected {fixture.ExpectedDistinctInstances}), fields {check.InjectedFields} (expected {fixture.ExpectedInjectedFields}), null {check.NullFields}, inconsistent {check.InconsistentNodes}");

            yield return null;
            metrics.Add("heap-after-ready", GC.GetTotalMemory(true) / (1024.0 * 1024.0), "mb");
            yield return Settle();

            // --- Warm: unload and load the same scene again. Singletons already exist.
            List<double> reloadInject = new();
            List<double> reloadLoad = new();
            List<double> unload = new();
            List<double> reloadPreawake = new();
            long heapBeforeReloads = GC.GetTotalMemory(false);
            int gen0BeforeReloads = GC.CollectionCount(0);

            for (int i = 0; i < RELOAD_CYCLES; i++)
            {
                long unloadStart = BenchClock.Now;
                AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
                while (op != null && !op.isDone)
                    yield return null;
                unload.Add(BenchClock.Ms(unloadStart, BenchClock.Now));
                yield return null;

                BenchSceneMarkerBase.ResetMarks();
                long start = BenchClock.Now;
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                while (!BenchSceneMarkerBase.IsReady)
                    yield return null;

                reloadInject.Add(BenchClock.Ms(BenchSceneMarkerBase.FirstAwake, BenchSceneMarkerBase.Ready));
                reloadPreawake.Add(BenchClock.Ms(start, BenchSceneMarkerBase.FirstAwake));
                reloadLoad.Add(BenchClock.Ms(start, BenchSceneMarkerBase.Ready));
                scene = SceneManager.GetSceneByName(sceneName);
                yield return null;
            }

            metrics.AddStats("scene-reload-inject", reloadInject, "ms");
            metrics.AddStats("scene-reload-load", reloadLoad, "ms");
            metrics.AddStats("scene-unload", unload, "ms");
            metrics.AddStats("scene-reload-preawake", reloadPreawake, "ms");
            metrics.Add("reload-heap-growth", GC.GetTotalMemory(false) - heapBeforeReloads, "bytes");
            metrics.Add("reload-gc-collections", GC.CollectionCount(0) - gen0BeforeReloads, "count");

            if (!isBaseline)
            {
                BenchVerifier.Result recheck = BenchVerifier.Verify(scene);
                metrics.Add("graph-valid-after-reload", IsValid(recheck, fixture) ? 1 : 0, "count");
            }
            yield return Settle();

            // --- Gameplay: spawn injected prefabs in batches, a few frames after the scene is ready.
            IBenchSpawner spawner = FindSpawner(scene);
            if (spawner != null && spawner.PrefabCount > 0)
            {
                for (int i = 0; i < FRAMES_BEFORE_SPAWN; i++)
                    yield return null;

                GameObject parentObject = new("Spawned");
                SceneManager.MoveGameObjectToScene(parentObject, scene);
                Transform parent = parentObject.transform;

                List<double> batches = new();
                long heapBeforeSpawn = GC.GetTotalMemory(false);
                int gen0BeforeSpawn = GC.CollectionCount(0);
                int spawned = 0;
                double totalMs = 0;

                while (spawned < PREFAB_INSTANCES)
                {
                    int count = Math.Min(PREFAB_BATCH, PREFAB_INSTANCES - spawned);
                    long start = BenchClock.Now;
                    for (int k = 0; k < count; k++)
                    {
                        spawner.Spawn(spawned % spawner.PrefabCount, parent);
                        spawned++;
                    }

                    double ms = BenchClock.Ms(start, BenchClock.Now);
                    batches.Add(ms);
                    totalMs += ms;
                    yield return null;
                }

                metrics.Add("prefab-heap-growth", GC.GetTotalMemory(false) - heapBeforeSpawn, "bytes");
                metrics.Add("prefab-gc-collections", GC.CollectionCount(0) - gen0BeforeSpawn, "count");
                metrics.Add("prefab-first-batch", batches[0], "ms");
                metrics.AddStats("prefab-batch", batches, "ms");
                metrics.Add("prefab-total", totalMs, "ms");
                metrics.Add("prefab-per-instance", totalMs / spawned, "ms");
                if (!isBaseline)
                    metrics.Add("prefab-null-fields", BenchVerifier.CountPrefabNullFields(parent), "count");
                Destroy(parentObject);
            }
            else
            {
                Debug.LogError($"[FastIoCBench] {variant}: no spawner in {sceneName}.");
            }

            string notes = string.Join("; ",
                $"buildId={info.buildId}",
                $"launch={launch + 1}/{plan.TotalLaunches}",
                $"order={string.Join(",", OrderOfRound(plan, plan.RoundOf(launch)))}",
                $"fastioc={info.fastIocVersion}",
                $"fastiocDirty={info.gitDirty.ToString().ToLowerInvariant()}",
                $"zenject={info.zenjectVersion}",
                $"vcontainer={info.vcontainerVersion}",
                $"stripping={info.strippingLevel}",
                $"incrementalGc={info.incrementalGc.ToString().ToLowerInvariant()}",
                $"bindings={fixture.BindingCount}",
                $"implementations={fixture.ImplementationCount}",
                $"monoInjectables={fixture.MonoInjectableCount}",
                $"graphHash={check.Hash.ToString(CultureInfo.InvariantCulture)}",
                $"expectedGraphHash={fixture.ExpectedGraphHash.ToString(CultureInfo.InvariantCulture)}",
                $"reloadCycles={RELOAD_CYCLES}",
                $"prefabInstances={PREFAB_INSTANCES}",
                $"prefabBatch={PREFAB_BATCH}",
                $"fastiocGenerationSeconds={info.fastIocGenerationSeconds.ToString("0.00", CultureInfo.InvariantCulture)}");

            string path = BenchReport.Write(info, fixture, variant, runIndex, plan.rounds, notes, metrics);
            plan.launchesCompleted++;
            plan.Save();

            Debug.Log($"[FastIoCBench] {variant} run {runIndex}: wrote {path}");
            Finish($"{variant} run {runIndex}/{plan.rounds} done ({plan.launchesCompleted}/{plan.TotalLaunches}).\n{(isBaseline ? "Baseline (no DI)" : graphValid ? "Graph OK" : "WRONG GRAPH")}");
        }

        private static bool IsValid(BenchVerifier.Result r, IBenchFixture fixture)
        {
            return r.Hash == fixture.ExpectedGraphHash
                   && r.DistinctInstances == fixture.ExpectedDistinctInstances
                   && r.InjectedFields == fixture.ExpectedInjectedFields
                   && r.NullFields == 0
                   && r.InconsistentNodes == 0;
        }

        private static IEnumerable<string> OrderOfRound(BenchPlan plan, int round)
        {
            for (int slot = 0; slot < plan.variants.Length; slot++)
                yield return plan.variants[(slot + round) % plan.variants.Length];
        }

        private static IBenchSpawner FindSpawner(Scene scene)
        {
            List<MonoBehaviour> buffer = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                buffer.Clear();
                root.GetComponentsInChildren(true, buffer);
                foreach (MonoBehaviour mb in buffer)
                {
                    if (mb is IBenchSpawner spawner)
                        return spawner;
                }
            }

            return null;
        }

        private static IEnumerator Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return new WaitForSecondsRealtime(1f);
        }

        private void Finish(string message)
        {
            status = message;
            showStatus = true;
            Debug.Log($"[FastIoCBench] {message}");
            StartCoroutine(QuitSoon());
        }

        private IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(2f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        protected void OnGUI()
        {
            if (!showStatus)
                return;

            GUI.skin.label.fontSize = Mathf.Max(24, Screen.height / 40);
            GUI.Label(new Rect(20, 60, Screen.width - 40, Screen.height - 120), status);
        }
    }
}
