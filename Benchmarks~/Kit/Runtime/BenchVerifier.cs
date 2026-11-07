using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastIoCBench
{
    /// <summary>
    /// Walks the graph injected into a scene and hashes it. Every variant must produce the
    /// hash, instance count and field count the generator expects; otherwise its timings mean nothing.
    /// Runs after the timed windows, so its allocations don't count.
    /// </summary>
    public static class BenchVerifier
    {
        public struct Result
        {
            public uint Hash;
            public int DistinctInstances;
            public int InjectedFields;
            public int NullFields;
            public int InconsistentNodes;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        public static Result Verify(Scene scene)
        {
            List<IBenchInjectable> items = new();
            List<MonoBehaviour> buffer = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                buffer.Clear();
                root.GetComponentsInChildren(true, buffer);
                foreach (MonoBehaviour mb in buffer)
                {
                    if (mb is IBenchInjectable injectable)
                        items.Add(injectable);
                }
            }

            items.Sort((a, b) => a.BenchIndex.CompareTo(b.BenchIndex));

            Result result = default;
            BenchHash hash = default;
            HashSet<object> visited = new(new ReferenceComparer());
            SortedDictionary<int, int[]> nodeDeps = new();
            Stack<IBenchNode> stack = new();
            List<IBenchNode> fields = new();

            foreach (IBenchInjectable item in items)
            {
                fields.Clear();
                item.CollectFields(fields);
                hash.Add(item.BenchIndex);

                foreach (IBenchNode node in fields)
                {
                    result.InjectedFields++;
                    if (node == null)
                    {
                        result.NullFields++;
                        hash.Add(-1);
                        continue;
                    }

                    hash.Add(node.NodeId);
                    stack.Push(node);
                }

                while (stack.Count > 0)
                {
                    IBenchNode node = stack.Pop();
                    if (!visited.Add(node))
                        continue;

                    IBenchNode[] deps = node.Deps ?? System.Array.Empty<IBenchNode>();
                    int[] depIds = new int[deps.Length];
                    for (int i = 0; i < deps.Length; i++)
                    {
                        depIds[i] = deps[i] != null ? deps[i].NodeId : -1;
                        if (deps[i] != null)
                            stack.Push(deps[i]);
                        else
                            result.NullFields++;
                    }

                    if (nodeDeps.TryGetValue(node.NodeId, out int[] known))
                    {
                        if (!SameIds(known, depIds))
                            result.InconsistentNodes++;
                    }
                    else
                    {
                        nodeDeps.Add(node.NodeId, depIds);
                    }
                }
            }

            foreach (KeyValuePair<int, int[]> pair in nodeDeps)
            {
                hash.Add(pair.Key);
                hash.Add(pair.Value.Length);
                foreach (int dep in pair.Value)
                    hash.Add(dep);
            }

            result.Hash = hash.Value;
            result.DistinctInstances = visited.Count;
            return result;
        }

        public static int CountPrefabNullFields(Transform parent)
        {
            int nulls = 0;
            List<MonoBehaviour> buffer = new();
            parent.GetComponentsInChildren(true, buffer);
            foreach (MonoBehaviour mb in buffer)
            {
                if (mb is IBenchPrefabPart part)
                    nulls += part.NullFieldCount();
            }

            return nulls;
        }

        private static bool SameIds(int[] a, int[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }
    }
}
