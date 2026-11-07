using System;
using System.Collections.Generic;
using System.Linq;

namespace FastIoCBench.Editor
{
    /// <summary>
    /// One object graph per profile, built from the profile seed with a fixed PRNG, so every
    /// variant binds and resolves exactly the same types. Also computes what the runner must find.
    /// </summary>
    public sealed class BenchGraph
    {
        public sealed class Impl
        {
            public int Id;
            public int Feature;
            public int Layer;
            public bool Transient;
            public bool Hot;
            public readonly List<string> Interfaces = new();
            public readonly List<Dep> Deps = new();
            public string Name => $"F{Feature:000}S{Id:0000}";
        }

        public struct Dep
        {
            public Impl Impl;
            public string Interface;
        }

        public struct Field
        {
            public Impl Impl;
            public string Interface;
        }

        public sealed class MonoType
        {
            public string Name;
            public readonly List<Field> Fields = new();
        }

        public sealed class PrefabType
        {
            public string Name;
            public readonly List<Field> RootFields = new();
            public readonly List<Field> ChildFields = new();
            public bool HasChild => ChildFields.Count > 0;
        }

        public BenchProfile Profile;
        public readonly List<Impl> Impls = new();
        public readonly List<MonoType> MonoTypes = new();
        public readonly List<int> MonoInstanceTypes = new();
        public readonly List<MonoType> Probes = new();
        public readonly List<PrefabType> Prefabs = new();

        public uint ExpectedHash;
        public int ExpectedDistinctInstances;
        public int ExpectedInjectedFields;
        public int InterfaceCount => Impls.Sum(i => i.Interfaces.Count);

        private sealed class Rng
        {
            private ulong state;
            public Rng(int seed) { state = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL); }

            public ulong Next()
            {
                unchecked
                {
                    ulong z = state += 0x9E3779B97F4A7C15UL;
                    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                    return z ^ (z >> 31);
                }
            }

            public int Range(int min, int maxExclusive) => maxExclusive <= min ? min : min + (int)(Next() % (ulong)(maxExclusive - min));
            public double Unit() => (Next() >> 11) * (1.0 / 9007199254740992.0);
        }

        public static BenchGraph Build(BenchProfile p)
        {
            if (p.bindingCount < 2 || p.featureCount < 1 || p.maxDepth < 1)
                throw new ArgumentException($"Profile {p.profileName} is too small.");

            BenchGraph g = new() { Profile = p };
            Rng rng = new(p.seed);

            // Interfaces per implementation: most have one, multiInterfaceTypeCount have 2 or 3.
            int multi = Math.Min(p.multiInterfaceTypeCount, p.bindingCount / 3);
            List<int> extraInterfaces = new();
            int extras = 0;
            for (int i = 0; i < multi; i++)
            {
                int k = rng.Range(2, 4);
                extraInterfaces.Add(k - 1);
                extras += k - 1;
            }

            int implCount = p.bindingCount - extras;
            for (int i = 0; i < implCount; i++)
            {
                Impl impl = new()
                {
                    Id = i,
                    Feature = i < p.featureCount ? i : rng.Range(0, p.featureCount),
                    Layer = i < p.maxDepth ? i : rng.Range(0, p.maxDepth),
                };
                impl.Hot = rng.Unit() < 0.1;
                g.Impls.Add(impl);
            }

            // Multi-interface types are singletons; ~20% of the others are transient.
            List<Impl> shuffled = g.Impls.OrderBy(_ => rng.Next()).ToList();
            HashSet<Impl> multiSet = new();
            for (int i = 0; i < multi; i++)
            {
                Impl impl = shuffled[i];
                multiSet.Add(impl);
                impl.Interfaces.Add("I" + impl.Name);
                for (int e = 0; e < extraInterfaces[i]; e++)
                    impl.Interfaces.Add("I" + impl.Name + (char)('B' + e));
            }

            foreach (Impl impl in g.Impls)
            {
                if (multiSet.Contains(impl))
                    continue;

                impl.Interfaces.Add("I" + impl.Name);
                impl.Transient = rng.Unit() < 0.2;
            }

            // Dependencies point only to lower layers. Transients depend only on singletons,
            // so the number of objects a resolve creates stays bounded (and predictable).
            foreach (Impl impl in g.Impls.OrderBy(i => i.Layer).ThenBy(i => i.Id))
            {
                if (impl.Layer == 0)
                    continue;

                int wanted = rng.Range(1, 5);
                HashSet<Impl> used = new();

                bool Allowed(Impl candidate) => candidate.Layer < impl.Layer && !used.Contains(candidate) && (!impl.Transient || !candidate.Transient);

                // The first dependency comes from the layer just below, so the graph reaches maxDepth.
                List<Impl> below = g.Impls.Where(c => c.Layer == impl.Layer - 1 && Allowed(c)).ToList();
                if (below.Count > 0)
                    AddDep(impl, Pick(rng, below), used, rng);

                for (int attempt = 0; impl.Deps.Count < wanted && attempt < 16; attempt++)
                {
                    bool cross = rng.Unit() < p.crossFeatureDependencyRatio;
                    List<Impl> pool = g.Impls.Where(c => Allowed(c) && (cross ? c.Feature != impl.Feature : c.Feature == impl.Feature)).ToList();
                    if (pool.Count == 0)
                        pool = g.Impls.Where(Allowed).ToList();
                    if (pool.Count == 0)
                        break;

                    AddDep(impl, Pick(rng, pool), used, rng);
                }
            }

            List<Field> allTargets = g.Impls.SelectMany(i => i.Interfaces.Select(n => new Field { Impl = i, Interface = n })).ToList();

            // Probes: one per feature, with a field for every interface of the feature, so every binding is resolved.
            for (int f = 0; f < p.featureCount; f++)
            {
                MonoType probe = new() { Name = $"ProbeF{f:000}" };
                foreach (Impl impl in g.Impls.Where(i => i.Feature == f).OrderBy(i => i.Id))
                    foreach (string iface in impl.Interfaces)
                        probe.Fields.Add(new Field { Impl = impl, Interface = iface });
                g.Probes.Add(probe);
            }

            // Scene MonoBehaviours: half as many types as instances, 1-3 injected fields each.
            int monoTypeCount = Math.Max(1, p.monoInjectableCount / 2);
            for (int t = 0; t < monoTypeCount; t++)
            {
                MonoType type = new() { Name = $"M{t:0000}" };
                AddFields(type.Fields, rng.Range(1, 4), allTargets, rng);
                g.MonoTypes.Add(type);
            }

            for (int i = 0; i < p.monoInjectableCount; i++)
                g.MonoInstanceTypes.Add(i % monoTypeCount);

            // Prefabs: a root component with 1-3 fields, half of them with an injected child too.
            for (int i = 0; i < Math.Max(1, p.runtimePrefabInjectionCount); i++)
            {
                PrefabType prefab = new() { Name = $"P{i:000}" };
                AddFields(prefab.RootFields, rng.Range(1, 4), allTargets, rng);
                if (rng.Unit() < 0.5)
                    AddFields(prefab.ChildFields, rng.Range(1, 3), allTargets, rng);
                g.Prefabs.Add(prefab);
            }

            g.ComputeExpectations();
            return g;
        }

        private static Impl Pick(Rng rng, List<Impl> pool)
        {
            // A few hot services are used much more than the rest, like in a real project.
            int total = 0;
            foreach (Impl c in pool)
                total += c.Hot ? 5 : 1;

            int roll = rng.Range(0, total);
            foreach (Impl c in pool)
            {
                roll -= c.Hot ? 5 : 1;
                if (roll < 0)
                    return c;
            }

            return pool[pool.Count - 1];
        }

        private static void AddDep(Impl impl, Impl target, HashSet<Impl> used, Rng rng)
        {
            used.Add(target);
            impl.Deps.Add(new Dep { Impl = target, Interface = target.Interfaces[rng.Range(0, target.Interfaces.Count)] });
        }

        private static void AddFields(List<Field> into, int count, List<Field> targets, Rng rng)
        {
            HashSet<string> used = new();
            for (int attempt = 0; into.Count < count && attempt < 16; attempt++)
            {
                Field f = targets[rng.Range(0, targets.Count)];
                if (used.Add(f.Interface))
                    into.Add(f);
            }
        }

        /// <summary>Same walk as BenchVerifier at runtime: items in BenchIndex order (probes first), then every node once by id.</summary>
        private void ComputeExpectations()
        {
            BenchHash hash = default;
            int index = 0;
            int fields = 0;
            int transientRefs = 0;

            foreach (MonoType probe in Probes)
            {
                hash.Add(index++);
                foreach (Field f in probe.Fields)
                {
                    hash.Add(f.Impl.Id);
                    fields++;
                    if (f.Impl.Transient)
                        transientRefs++;
                }
            }

            foreach (int typeIndex in MonoInstanceTypes)
            {
                hash.Add(index++);
                foreach (Field f in MonoTypes[typeIndex].Fields)
                {
                    hash.Add(f.Impl.Id);
                    fields++;
                    if (f.Impl.Transient)
                        transientRefs++;
                }
            }

            foreach (Impl impl in Impls.OrderBy(i => i.Id))
            {
                hash.Add(impl.Id);
                hash.Add(impl.Deps.Count);
                foreach (Dep d in impl.Deps)
                    hash.Add(d.Impl.Id);

                // Each singleton is created once, and creates one instance per transient dependency.
                if (!impl.Transient)
                    transientRefs += impl.Deps.Count(d => d.Impl.Transient);
            }

            ExpectedHash = hash.Value;
            ExpectedInjectedFields = fields;
            ExpectedDistinctInstances = Impls.Count(i => !i.Transient) + transientRefs;
        }
    }
}
