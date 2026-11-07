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

    public static class BenchRegistry
    {
        public static IBenchFixture Fixture { get; set; }
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
