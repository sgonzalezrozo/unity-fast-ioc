using System.Collections.Generic;
using UnityEngine;

namespace FastIoCBench
{
    /// <summary>Implemented by every generated service, so the runner can walk the resolved graph without reflection.</summary>
    public interface IBenchNode
    {
        int NodeId { get; }
        IBenchNode[] Deps { get; }
    }

    /// <summary>A scene MonoBehaviour (probe or injectable) whose injected fields the runner checks after injection.</summary>
    public interface IBenchInjectable
    {
        int BenchIndex { get; }
        void CollectFields(List<IBenchNode> into);
    }

    /// <summary>Spawns the generated prefabs through the variant's own instantiation API.</summary>
    public interface IBenchSpawner
    {
        int PrefabCount { get; }
        GameObject Spawn(int prefabIndex, Transform parent);
    }

    /// <summary>A component on a generated prefab.</summary>
    public interface IBenchPrefabPart
    {
        int NullFieldCount();
    }

    /// <summary>The generated fixture. Registered by generated code before the first scene loads.</summary>
    public interface IBenchFixture
    {
        string ProfileName { get; }
        int Seed { get; }
        int BindingCount { get; }
        int ImplementationCount { get; }
        int MonoInjectableCount { get; }
        int FeatureCount { get; }
        int MaxDepth { get; }
        string[] Variants { get; }

        uint ExpectedGraphHash { get; }
        int ExpectedDistinctInstances { get; }
        int ExpectedInjectedFields { get; }

        /// <summary>Builds the global container of <paramref name="variant"/>. Bindings only: singletons are created on first use.</summary>
        void BuildContainer(string variant);

        string SceneName(string variant);
    }

    /// <summary>The optimized copy of the graph (FastIoCBench.GenOpt), used by zenject-baked and vcontainer-sg.</summary>
    public interface IBenchOptimizedFixture
    {
        /// <summary>Installs the optimized copy into a Zenject DiContainer (passed as object: this assembly doesn't reference Zenject).</summary>
        void InstallZenject(object diContainer);

        /// <summary>Every class of the copy, to check on the device that the optimization covered it.</summary>
        System.Type[] InjectableTypes { get; }
    }

    public static class BenchRegistry
    {
        public static IBenchFixture Fixture { get; set; }

        public static IBenchOptimizedFixture Optimized { get; set; }

        /// <summary>Set before Zenject's ProjectContext is created, so its installer installs the baked copy.</summary>
        public static bool ZenjectOptimized { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFlags() => ZenjectOptimized = false;
    }

    /// <summary>Checks, by reflection and after the timed phases, how much of the optimized copy each optimization covered.</summary>
    public static class BenchOptimizationCheck
    {
        /// <summary>Method Zenject's reflection baking weaves into each class (Zenject TypeAnalyzer.ReflectionBakingGetInjectInfoMethodName).</summary>
        public const string ZENJECT_BAKED_METHOD = "__zenCreateInjectTypeInfo";

        public static double CoveragePercent(string variant)
        {
            System.Type[] types = BenchRegistry.Optimized?.InjectableTypes;
            if (types == null || types.Length == 0)
                return 0;

            int covered = 0;
            foreach (System.Type t in types)
            {
                bool ok = variant == "zenject-baked"
                    ? t.GetMethod(ZENJECT_BAKED_METHOD, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) != null
                    // VContainer's InjectorCache looks for this type first.
                    : t.Assembly.GetType(t.FullName + "GeneratedInjector", false) != null;
                if (ok)
                    covered++;
            }

            return covered * 100.0 / types.Length;
        }
    }

    /// <summary>FNV-1a over 32-bit integers. The generator computes the same value in the Editor.</summary>
    public struct BenchHash
    {
        private uint value;
        private bool started;

        public uint Value => started ? value : 2166136261u;

        public void Add(int item)
        {
            if (!started)
            {
                value = 2166136261u;
                started = true;
            }

            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    value ^= (byte)(item >> (8 * i));
                    value *= 16777619u;
                }
            }
        }
    }
}
