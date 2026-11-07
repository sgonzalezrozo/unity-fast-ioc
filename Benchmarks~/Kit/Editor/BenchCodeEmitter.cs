using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FastIoCBench.Editor
{
    /// <summary>
    /// Writes the C# for one graph: services, bindings for every container, scene MonoBehaviours,
    /// prefab components, spawners and the fixture. Everything lands in Assembly-CSharp, next to
    /// Fast IoC's own generated code. Each MonoBehaviour gets its own file, as Unity requires.
    /// </summary>
    public static class BenchCodeEmitter
    {
        public const string NAMESPACE = "FastIoCBench.Gen";
        private const string NODE = "global::FastIoCBench.IBenchNode";
        private const string INJECT = "[global::Zenject.Inject, global::VContainer.Inject]";

        public const string OPT_NAMESPACE = "FastIoCBench.GenOpt";
        public const string OPT_ASSEMBLY = "FastIoCBench.GenOpt";

        private static System.Action<string, string> Writer(BenchGraph g, string folder, string ns, List<string> files)
        {
            Directory.CreateDirectory(folder);
            return (name, body) =>
            {
                string path = Path.Combine(folder, name + ".cs");
                File.WriteAllText(path, Header(g) + "namespace " + ns + "\n{\n" + body + "}\n");
                files.Add(path);
            };
        }

        /// <summary>
        /// A second copy of the same graph for the optimized rivals (Zenject reflection baking, VContainer source
        /// generator), in its own assembly. Both optimizations change the compiled types, so the default variants
        /// must not share these types. The baking is limited to this namespace and the source generator to this
        /// assembly, which nothing references: it registers itself at startup.
        /// </summary>
        public static List<string> EmitOptimized(BenchGraph g, string folder)
        {
            List<string> files = new();
            var Write = Writer(g, folder, OPT_NAMESPACE, files);

            for (int f = 0; f < g.Profile.featureCount; f++)
            {
                List<BenchGraph.Impl> impls = g.Impls.Where(i => i.Feature == f).OrderBy(i => i.Id).ToList();
                if (impls.Count > 0)
                    Write($"Services_F{f:000}", Services(impls));
            }

            Write("ZenjectBindings", ZenjectBindings(g));
            Write("VContainerBindings", VContainerBindings(g));
            Write("VContainerRootScope", VContainerRootScope(g));
            Write("VContainerSceneScope", VContainerSceneScope());

            foreach (BenchGraph.MonoType probe in g.Probes)
                Write(probe.Name, Injectable(probe));
            foreach (BenchGraph.MonoType type in g.MonoTypes)
                Write(type.Name, Injectable(type));

            foreach (BenchGraph.PrefabType prefab in g.Prefabs)
            {
                Write(prefab.Name, PrefabPart(prefab.Name, prefab.RootFields, isRoot: false));
                if (prefab.HasChild)
                    Write(prefab.Name + "Child", PrefabPart(prefab.Name + "Child", prefab.ChildFields, isRoot: false));
            }

            Write("ZenjectSpawner", ZenjectSpawner());
            Write("VContainerSpawner", VContainerSpawner());
            Write("OptimizedFixture", OptimizedFixture(g));

            string asmdef = Path.Combine(folder, OPT_ASSEMBLY + ".asmdef");
            File.WriteAllText(asmdef,
                "{\n" +
                $"    \"name\": \"{OPT_ASSEMBLY}\",\n" +
                $"    \"rootNamespace\": \"{OPT_NAMESPACE}\",\n" +
                "    \"references\": [\"FastIoCBench.Runtime\", \"Zenject\", \"VContainer\"],\n" +
                "    \"includePlatforms\": [],\n" +
                "    \"excludePlatforms\": [],\n" +
                "    \"allowUnsafeCode\": false,\n" +
                "    \"overrideReferences\": false,\n" +
                "    \"precompiledReferences\": [],\n" +
                "    \"autoReferenced\": false,\n" +
                "    \"defineConstraints\": [],\n" +
                "    \"versionDefines\": [],\n" +
                "    \"noEngineReferences\": false\n" +
                "}\n");
            files.Add(asmdef);
            return files;
        }

        private static string OptimizedFixture(BenchGraph g)
        {
            StringBuilder sb = new();
            sb.Append("    /// <summary>Entry point of the optimized copy. Registers itself, so no other assembly references this one.</summary>\n");
            sb.Append("    public sealed class OptimizedFixture : global::FastIoCBench.IBenchOptimizedFixture\n    {\n");
            sb.Append("        [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]\n");
            sb.Append("        private static void Register() => global::FastIoCBench.BenchRegistry.Optimized = new OptimizedFixture();\n\n");
            sb.Append("        public void InstallZenject(object diContainer)\n        {\n");
            sb.Append("            var c = (global::Zenject.DiContainer)diContainer;\n");
            foreach (IGrouping<int, BenchGraph.Impl> feature in ByFeature(g))
                sb.Append($"            ZenjectBindings.InstallF{feature.Key:000}(c);\n");
            sb.Append("        }\n\n");
            sb.Append("        /// <summary>Every class the optimizations should cover, to check on the device that they did.</summary>\n");
            sb.Append("        public global::System.Type[] InjectableTypes => new global::System.Type[]\n        {\n");
            foreach (BenchGraph.Impl impl in g.Impls.OrderBy(i => i.Id))
                sb.Append($"            typeof({impl.Name}),\n");
            foreach (BenchGraph.MonoType t in g.Probes.Concat(g.MonoTypes))
                sb.Append($"            typeof({t.Name}),\n");
            foreach (BenchGraph.PrefabType prefab in g.Prefabs)
            {
                sb.Append($"            typeof({prefab.Name}),\n");
                if (prefab.HasChild)
                    sb.Append($"            typeof({prefab.Name}Child),\n");
            }
            sb.Append("        };\n    }\n");
            return sb.ToString();
        }

        public static List<string> Emit(BenchGraph g, string folder, bool withSourceGenerator)
        {
            List<string> files = new();
            var Write = Writer(g, folder, NAMESPACE, files);

            for (int f = 0; f < g.Profile.featureCount; f++)
            {
                List<BenchGraph.Impl> impls = g.Impls.Where(i => i.Feature == f).OrderBy(i => i.Id).ToList();
                if (impls.Count > 0)
                    Write($"Services_F{f:000}", Services(impls));
            }

            Write("FastIoCInstallers", FastIoCInstallers(g));
            Write("ZenjectBindings", ZenjectBindings(g));
            Write("ZenjectRootInstaller", ZenjectRootInstaller(g));
            Write("VContainerBindings", VContainerBindings(g));
            Write("VContainerRootScope", VContainerRootScope(g));
            Write("VContainerSceneScope", VContainerSceneScope());

            foreach (BenchGraph.MonoType probe in g.Probes)
                Write(probe.Name, Injectable(probe));
            foreach (BenchGraph.MonoType type in g.MonoTypes)
                Write(type.Name, Injectable(type));

            foreach (BenchGraph.PrefabType prefab in g.Prefabs)
            {
                Write(prefab.Name, PrefabPart(prefab.Name, prefab.RootFields, isRoot: true));
                if (prefab.HasChild)
                    Write(prefab.Name + "Child", PrefabPart(prefab.Name + "Child", prefab.ChildFields, isRoot: false));
            }

            Write("FastIoCSpawner", FastIoCSpawner(g));
            Write("ZenjectSpawner", ZenjectSpawner());
            Write("VContainerSpawner", VContainerSpawner());
            Write("NoneSpawner", NoneSpawner());
            Write("GeneratedBenchFixture", Fixture(g, withSourceGenerator));
            Write("BenchBoot", "    public sealed class BenchBoot : global::FastIoCBench.BenchBootBase { }\n");
            Write("BenchSceneMarker", "    [global::UnityEngine.DefaultExecutionOrder(-32000)]\n    public sealed class BenchSceneMarker : global::FastIoCBench.BenchSceneMarkerBase { }\n");

            return files;
        }

        private static string Header(BenchGraph g) =>
            "// <auto-generated>\n" +
            $"// Fast IoC benchmark fixture. Profile {g.Profile.profileName}, seed {g.Profile.seed}.\n" +
            "// Written by Tools > FastIoC Benchmark. Do not edit: it is regenerated.\n" +
            "// </auto-generated>\n";

        private static string Services(List<BenchGraph.Impl> impls)
        {
            StringBuilder sb = new();
            foreach (BenchGraph.Impl impl in impls)
            {
                foreach (string iface in impl.Interfaces)
                    sb.Append("    public interface ").Append(iface).Append(" : ").Append(NODE).Append(" { }\n");

                sb.Append("    public sealed class ").Append(impl.Name).Append(" : ").Append(string.Join(", ", impl.Interfaces)).Append("\n    {\n");
                sb.Append("        public ").Append(impl.Name).Append('(');
                sb.Append(string.Join(", ", impl.Deps.Select((d, i) => d.Interface + " d" + i)));
                sb.Append(")\n        {\n");
                if (impl.Deps.Count == 0)
                    sb.Append("            Deps = global::System.Array.Empty<").Append(NODE).Append(">();\n");
                else
                    sb.Append("            Deps = new ").Append(NODE).Append("[] { ").Append(string.Join(", ", impl.Deps.Select((_, i) => "d" + i))).Append(" };\n");
                sb.Append("        }\n\n");
                sb.Append("        public int NodeId => ").Append(impl.Id).Append(";\n");
                sb.Append("        public ").Append(NODE).Append("[] Deps { get; }\n");
                sb.Append("    }\n\n");
            }

            return sb.ToString();
        }

        private static IEnumerable<IGrouping<int, BenchGraph.Impl>> ByFeature(BenchGraph g) =>
            g.Impls.OrderBy(i => i.Id).GroupBy(i => i.Feature).OrderBy(x => x.Key);

        private static string FastIoCInstallers(BenchGraph g)
        {
            StringBuilder sb = new();
            foreach (IGrouping<int, BenchGraph.Impl> feature in ByFeature(g))
            {
                sb.Append("    [global::FastIoC.API.MonoInstaller]\n");
                sb.Append($"    public sealed class F{feature.Key:000}FastIoCInstaller : global::FastIoC.API.BindingRecorder\n    {{\n");
                sb.Append("        public override void InstallBindings()\n        {\n");
                foreach (BenchGraph.Impl impl in feature)
                {
                    string life = impl.Transient ? "AsTransient" : "AsSingle";
                    foreach (string iface in impl.Interfaces)
                        sb.Append($"            Bind<{iface}>().To<{impl.Name}>().{life}();\n");
                }

                sb.Append("        }\n    }\n\n");
            }

            return sb.ToString();
        }

        private static string ZenjectBindings(BenchGraph g)
        {
            StringBuilder sb = new();
            sb.Append("    public static class ZenjectBindings\n    {\n");
            foreach (IGrouping<int, BenchGraph.Impl> feature in ByFeature(g))
            {
                sb.Append($"        public static void InstallF{feature.Key:000}(global::Zenject.DiContainer c)\n        {{\n");
                foreach (BenchGraph.Impl impl in feature)
                {
                    string life = impl.Transient ? "AsTransient" : "AsSingle";
                    if (impl.Interfaces.Count == 1)
                        sb.Append($"            c.Bind<{impl.Interfaces[0]}>().To<{impl.Name}>().{life}();\n");
                    else
                        sb.Append($"            c.Bind({string.Join(", ", impl.Interfaces.Select(i => $"typeof({i})"))}).To<{impl.Name}>().{life}();\n");
                }

                sb.Append("        }\n\n");
            }

            sb.Append("    }\n");
            return sb.ToString();
        }

        private static string ZenjectRootInstaller(BenchGraph g)
        {
            StringBuilder sb = new();
            sb.Append("    /// <summary>The ProjectContext prefab is shared by both Zenject variants; this picks the copy to install.</summary>\n");
            sb.Append("    public sealed class ZenjectRootInstaller : global::Zenject.MonoInstaller\n    {\n");
            sb.Append("        public override void InstallBindings()\n        {\n");
            sb.Append("            if (global::FastIoCBench.BenchRegistry.ZenjectOptimized)\n            {\n");
            sb.Append("                global::FastIoCBench.BenchRegistry.Optimized.InstallZenject(Container);\n");
            sb.Append("                return;\n            }\n\n");
            foreach (IGrouping<int, BenchGraph.Impl> feature in ByFeature(g))
                sb.Append($"            ZenjectBindings.InstallF{feature.Key:000}(Container);\n");
            sb.Append("        }\n    }\n");
            return sb.ToString();
        }

        private static string VContainerBindings(BenchGraph g)
        {
            StringBuilder sb = new();
            sb.Append("    using global::VContainer;\n\n");
            sb.Append("    public static class VContainerBindings\n    {\n");
            foreach (IGrouping<int, BenchGraph.Impl> feature in ByFeature(g))
            {
                sb.Append($"        public static void ConfigureF{feature.Key:000}(global::VContainer.IContainerBuilder b)\n        {{\n");
                foreach (BenchGraph.Impl impl in feature)
                {
                    string life = impl.Transient ? "Transient" : "Singleton";
                    sb.Append($"            b.Register<{impl.Name}>(Lifetime.{life}).As<{string.Join(", ", impl.Interfaces)}>();\n");
                }

                sb.Append("        }\n\n");
            }

            sb.Append("    }\n");
            return sb.ToString();
        }

        private static string VContainerRootScope(BenchGraph g)
        {
            StringBuilder sb = new();
            sb.Append("    public sealed class VContainerRootScope : global::VContainer.Unity.LifetimeScope\n    {\n");
            sb.Append("        protected override void Configure(global::VContainer.IContainerBuilder builder)\n        {\n");
            foreach (IGrouping<int, BenchGraph.Impl> feature in ByFeature(g))
                sb.Append($"            VContainerBindings.ConfigureF{feature.Key:000}(builder);\n");
            sb.Append("        }\n    }\n");
            return sb.ToString();
        }

        private static string VContainerSceneScope() =>
            "    /// <summary>Scene scope: no registrations of its own, injects the scene from VContainerRootScope.</summary>\n" +
            "    public sealed class VContainerSceneScope : global::VContainer.Unity.LifetimeScope\n    {\n" +
            "        protected override void Configure(global::VContainer.IContainerBuilder builder)\n        {\n        }\n    }\n";

        private static string Injectable(BenchGraph.MonoType type)
        {
            StringBuilder sb = new();
            sb.Append("    public sealed class ").Append(type.Name).Append(" : global::UnityEngine.MonoBehaviour, global::FastIoCBench.IBenchInjectable\n    {\n");
            for (int i = 0; i < type.Fields.Count; i++)
                sb.Append("        ").Append(INJECT).Append(" internal ").Append(type.Fields[i].Interface).Append(" f").Append(i).Append(";\n");
            sb.Append("        [global::UnityEngine.SerializeField] private int benchIndex;\n\n");
            sb.Append("        public int BenchIndex => benchIndex;\n\n");
            sb.Append("        public void CollectFields(global::System.Collections.Generic.List<").Append(NODE).Append("> into)\n        {\n");
            for (int i = 0; i < type.Fields.Count; i++)
                sb.Append("            into.Add(f").Append(i).Append(");\n");
            sb.Append("        }\n    }\n");
            return sb.ToString();
        }

        private static string PrefabPart(string name, List<BenchGraph.Field> fields, bool isRoot)
        {
            StringBuilder sb = new();
            sb.Append("    public sealed class ").Append(name).Append(" : global::UnityEngine.MonoBehaviour, global::FastIoCBench.IBenchPrefabPart");
            if (isRoot)
                sb.Append(", global::FastIoC.Core.IInstanciable");
            sb.Append("\n    {\n");
            for (int i = 0; i < fields.Count; i++)
                sb.Append("        ").Append(INJECT).Append(" internal ").Append(fields[i].Interface).Append(" f").Append(i).Append(";\n");
            sb.Append("\n        public int NullFieldCount()\n        {\n            int n = 0;\n");
            for (int i = 0; i < fields.Count; i++)
                sb.Append("            if (f").Append(i).Append(" == null) n++;\n");
            sb.Append("            return n;\n        }\n    }\n");
            return sb.ToString();
        }

        private static string FastIoCSpawner(BenchGraph g)
        {
            StringBuilder sb = new();
            sb.Append("    public sealed class FastIoCSpawner : global::UnityEngine.MonoBehaviour, global::FastIoCBench.IBenchSpawner\n    {\n");
            sb.Append("        internal global::FastIoC.Core.IInstantiator instantiator;\n");
            sb.Append("        [global::UnityEngine.SerializeField] private global::UnityEngine.GameObject[] prefabs;\n\n");
            sb.Append("        public int PrefabCount => prefabs.Length;\n\n");
            sb.Append("        public global::UnityEngine.GameObject Spawn(int prefabIndex, global::UnityEngine.Transform parent)\n        {\n");
            sb.Append("            switch (prefabIndex)\n            {\n");
            for (int i = 0; i < g.Prefabs.Count; i++)
                sb.Append($"                case {i}: return instantiator.Instantiate<{g.Prefabs[i].Name}>(prefabs[{i}], parent).gameObject;\n");
            sb.Append("                default: throw new global::System.ArgumentOutOfRangeException(nameof(prefabIndex));\n");
            sb.Append("            }\n        }\n    }\n");
            return sb.ToString();
        }

        private static string ZenjectSpawner() =>
            "    public sealed class ZenjectSpawner : global::UnityEngine.MonoBehaviour, global::FastIoCBench.IBenchSpawner\n    {\n" +
            "        [global::Zenject.Inject] private global::Zenject.DiContainer container;\n" +
            "        [global::UnityEngine.SerializeField] private global::UnityEngine.GameObject[] prefabs;\n\n" +
            "        public int PrefabCount => prefabs.Length;\n\n" +
            "        public global::UnityEngine.GameObject Spawn(int prefabIndex, global::UnityEngine.Transform parent)\n" +
            "            => container.InstantiatePrefab(prefabs[prefabIndex], parent);\n    }\n";

        private static string NoneSpawner() =>
            "    /// <summary>Baseline: plain Instantiate, nothing injected.</summary>\n" +
            "    public sealed class NoneSpawner : global::UnityEngine.MonoBehaviour, global::FastIoCBench.IBenchSpawner\n    {\n" +
            "        [global::UnityEngine.SerializeField] private global::UnityEngine.GameObject[] prefabs;\n\n" +
            "        public int PrefabCount => prefabs.Length;\n\n" +
            "        public global::UnityEngine.GameObject Spawn(int prefabIndex, global::UnityEngine.Transform parent)\n" +
            "            => global::UnityEngine.Object.Instantiate(prefabs[prefabIndex], parent);\n    }\n";

        private static string VContainerSpawner() =>
            "    public sealed class VContainerSpawner : global::UnityEngine.MonoBehaviour, global::FastIoCBench.IBenchSpawner\n    {\n" +
            "        [global::VContainer.Inject] private global::VContainer.IObjectResolver resolver;\n" +
            "        [global::UnityEngine.SerializeField] private global::UnityEngine.GameObject[] prefabs;\n\n" +
            "        public int PrefabCount => prefabs.Length;\n\n" +
            "        public global::UnityEngine.GameObject Spawn(int prefabIndex, global::UnityEngine.Transform parent)\n" +
            "            => global::VContainer.Unity.ObjectResolverUnityExtensions.Instantiate(resolver, prefabs[prefabIndex], parent);\n    }\n";

        private static string Fixture(BenchGraph g, bool withSourceGenerator)
        {
            BenchProfile p = g.Profile;
            StringBuilder sb = new();
            sb.Append("    public sealed class GeneratedBenchFixture : global::FastIoCBench.IBenchFixture\n    {\n");
            sb.Append("        [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]\n");
            sb.Append("        private static void Register()\n        {\n");
            sb.Append("            // Every container stays dormant until the runner starts the one this launch measures.\n");
            sb.Append("            global::FastIoC.UnityIntegration.Runtime.Bootstrap.FastIoCUnityBootstrapper.AutoStart = false;\n");
            sb.Append("            global::FastIoCBench.BenchRegistry.Fixture = new GeneratedBenchFixture();\n        }\n\n");
            sb.Append($"        public string ProfileName => \"{p.profileName}\";\n");
            sb.Append($"        public int Seed => {p.seed};\n");
            sb.Append($"        public int BindingCount => {g.InterfaceCount};\n");
            sb.Append($"        public int ImplementationCount => {g.Impls.Count};\n");
            sb.Append($"        public int MonoInjectableCount => {g.MonoInstanceTypes.Count};\n");
            sb.Append($"        public int FeatureCount => {p.featureCount};\n");
            sb.Append($"        public int MaxDepth => {p.maxDepth};\n");
            string variants = string.Join(", ", BenchSceneBuilder.VariantsFor(withSourceGenerator).Select(v => "\"" + v + "\""));
            sb.Append($"        public string[] Variants => new[] {{ {variants} }};\n");
            sb.Append($"        public uint ExpectedGraphHash => {g.ExpectedHash}u;\n");
            sb.Append($"        public int ExpectedDistinctInstances => {g.ExpectedDistinctInstances};\n");
            sb.Append($"        public int ExpectedInjectedFields => {g.ExpectedInjectedFields};\n\n");
            sb.Append("        public string SceneName(string variant) => \"Inject_\" + variant;\n\n");
            sb.Append("        public void BuildContainer(string variant)\n        {\n");
            sb.Append("            switch (variant)\n            {\n");
            sb.Append("                case \"none\":\n");
            sb.Append("                    // Baseline: same scene and prefabs, no container. Measures what Unity costs on its own.\n");
            sb.Append("                    break;\n");
            sb.Append("                case \"fastioc\":\n");
            sb.Append("                    global::FastIoC.UnityIntegration.Runtime.Bootstrap.FastIoCUnityBootstrapper.Start();\n");
            sb.Append("                    break;\n");
            sb.Append("                case \"zenject\":\n");
            sb.Append("                    // Loads Resources/ProjectContext and installs ZenjectRootInstaller.\n");
            sb.Append("                    _ = global::Zenject.ProjectContext.Instance;\n");
            sb.Append("                    break;\n");
            sb.Append("                case \"zenject-baked\":\n");
            sb.Append("                    // Same ProjectContext prefab; its installer installs the baked copy.\n");
            sb.Append("                    global::FastIoCBench.BenchRegistry.ZenjectOptimized = true;\n");
            sb.Append("                    _ = global::Zenject.ProjectContext.Instance;\n");
            sb.Append("                    break;\n");
            sb.Append("                case \"vcontainer-sg\":\n");
            sb.Append("                {\n");
            sb.Append("                    var prefab = global::UnityEngine.Resources.Load<global::UnityEngine.GameObject>(\"FastIoCBench/VContainerRootSG\");\n");
            sb.Append("                    var root = global::UnityEngine.Object.Instantiate(prefab);\n");
            sb.Append("                    global::UnityEngine.Object.DontDestroyOnLoad(root);\n");
            sb.Append("                    break;\n");
            sb.Append("                }\n");
            sb.Append("                case \"vcontainer\":\n");
            sb.Append("                {\n");
            sb.Append("                    var prefab = global::UnityEngine.Resources.Load<global::UnityEngine.GameObject>(\"FastIoCBench/VContainerRoot\");\n");
            sb.Append("                    var root = global::UnityEngine.Object.Instantiate(prefab);\n");
            sb.Append("                    global::UnityEngine.Object.DontDestroyOnLoad(root);\n");
            sb.Append("                    break;\n");
            sb.Append("                }\n");
            sb.Append("                default:\n");
            sb.Append("                    throw new global::System.ArgumentException(\"Unknown variant \" + variant);\n");
            sb.Append("            }\n        }\n    }\n");
            return sb.ToString();
        }
    }
}
