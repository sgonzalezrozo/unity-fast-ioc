#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tools.FastIoC.Runtime.Core;

namespace Tools.FastIoC.Editor.Generation
{
    public static class RegistryEmitter
    {
        public static string GenerateRegistry(
            HashSet<Type> reachableTypes,
            Dictionary<Type, int> typeIdMap,
            Dictionary<Type, Lifetime> typeLifetimes,
            Dictionary<string, List<Type>> installersByScene,
            Dictionary<Type, List<Type>> installerBindings,
            List<Type> allInstallers)
        {
            StringBuilder sb = new();

            Dictionary<Type, HashSet<string>> typeToScopes = TypeScopeAnalyzer.BuildTypeScopeMap(
                installersByScene,
                installerBindings,
                allInstallers,
                reachableTypes);

            GenerateHeader(sb);
            GenerateActivatorFields(sb, installersByScene);
            GenerateStaticConstructor(sb, installersByScene);
            GenerateEnsureInitializedMethod(sb);
            GenerateCreateSceneContextRuntimeMethod(sb, installersByScene);
            GenerateCreateProjectContextRuntimeMethod(sb);
            GenerateCreateSceneContextRuntimeFallbackMethod(sb);

            GenerateSceneBindingMethods(sb, installersByScene, typeIdMap, typeLifetimes, typeToScopes);
            GeneratePerSceneRuntimeMethods(sb, installersByScene, typeIdMap, typeLifetimes, typeToScopes);

            GenerateFooter(sb);

            return sb.ToString();
        }

        private static void GenerateSceneBindingMethods(
            StringBuilder sb,
            Dictionary<string, List<Type>> installersByScene,
            Dictionary<Type, int> typeIdMap,
            Dictionary<Type, Lifetime> typeLifetimes,
            Dictionary<Type, HashSet<string>> typeToScopes)
        {
            sb.AppendLine("        private static List<(RecipeActivator, Lifetime)> GetGlobalBindings()");
            sb.AppendLine("        {");
            sb.AppendLine("            List<(RecipeActivator, Lifetime)> bindings = new();");

            List<Type> globalTypes = typeToScopes
                .Where(kvp => kvp.Value.Contains(FastIoCConstants.GLOBAL_SCOPE))
                .Select(kvp => kvp.Key)
                .OrderBy(t => t.FullName ?? "")
                .ToList();

            foreach (Type type in globalTypes)
            {
                if (typeIdMap.TryGetValue(type, out _))
                {
                    string methodName = NamingConventions.GetActivationMethodName(type.FullName ?? "Unknown");
                    Lifetime lifetime = typeLifetimes.TryGetValue(type, out Lifetime lt) ? lt : Lifetime.Transient;
                    sb.AppendLine($"            bindings.Add((GeneratedActivators.Activate_{methodName}, Lifetime.{lifetime}));");
                }
            }

            sb.AppendLine("            return bindings;");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        private static List<(RecipeActivator, Lifetime)> GetSceneBindings(string sceneName)");
            sb.AppendLine("        {");
            sb.AppendLine("            return sceneName switch");
            sb.AppendLine("            {");

            foreach (KeyValuePair<string, List<Type>> kvp in installersByScene.Where(kv => kv.Key != FastIoCConstants.GLOBAL_SCOPE))
            {
                string sceneName = kvp.Key;
                sb.AppendLine($"                \"{sceneName}\" => GetBindings_{NamingConventions.EscapeSceneName(sceneName)}(),");
            }

            sb.AppendLine("                _ => new()");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (KeyValuePair<string, List<Type>> sceneKvp in installersByScene.Where(kv => kv.Key != FastIoCConstants.GLOBAL_SCOPE))
            {
                string sceneName = sceneKvp.Key;

                List<Type> sceneOnlyTypes = typeToScopes
                    .Where(typeKvp =>
                        typeKvp.Value.Contains(sceneName) &&
                        !typeKvp.Value.Contains(FastIoCConstants.GLOBAL_SCOPE))
                    .Select(typeKvp => typeKvp.Key)
                    .OrderBy(t => t.FullName ?? "")
                    .ToList();

                sb.AppendLine($"        private static List<(RecipeActivator, Lifetime)> GetBindings_{NamingConventions.EscapeSceneName(sceneName)}()");
                sb.AppendLine("        {");
                sb.AppendLine("            List<(RecipeActivator, Lifetime)> bindings = new();");

                foreach (Type type in sceneOnlyTypes)
                {
                    if (typeIdMap.TryGetValue(type, out _))
                    {
                        string methodName = NamingConventions.GetActivationMethodName(type.FullName ?? "Unknown");
                        Lifetime lifetime = typeLifetimes.TryGetValue(type, out Lifetime lt) ? lt : Lifetime.Transient;
                        sb.AppendLine($"            bindings.Add((GeneratedActivators.Activate_{methodName}, Lifetime.{lifetime}));");
                    }
                }

                sb.AppendLine("            return bindings;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }

        private static void GeneratePerSceneRuntimeMethods(
            StringBuilder sb,
            Dictionary<string, List<Type>> installersByScene,
            Dictionary<Type, int> typeIdMap,
            Dictionary<Type, Lifetime> typeLifetimes,
            Dictionary<Type, HashSet<string>> typeToScopes)
        {
            sb.AppendLine("        private static (RecipeActivator[], Lifetime[]) CreateGlobalRecipe()");
            sb.AppendLine("        {");
            sb.AppendLine("            int maxTypeId = GeneratedTypeIds.MaxTypeId;");
            sb.AppendLine("            RecipeActivator[] activators = new RecipeActivator[maxTypeId + 1];");
            sb.AppendLine("            Lifetime[] lifetimes = new Lifetime[maxTypeId + 1];");
            sb.AppendLine();

            List<Type> globalTypes = typeToScopes
                .Where(kvp => kvp.Value.Contains(FastIoCConstants.GLOBAL_SCOPE))
                .Select(kvp => kvp.Key)
                .OrderBy(t => t.FullName ?? "")
                .ToList();

            foreach (Type type in globalTypes)
            {
                if (typeIdMap.TryGetValue(type, out int typeId))
                {
                    string methodName = NamingConventions.GetActivationMethodName(type.FullName ?? "Unknown");
                    Lifetime lifetime = typeLifetimes.TryGetValue(type, out Lifetime lt) ? lt : Lifetime.Transient;

                    sb.AppendLine($"            activators[{typeId}] = GeneratedActivators.Activate_{methodName};");
                    sb.AppendLine($"            lifetimes[{typeId}] = Lifetime.{lifetime};");
                }
            }

            sb.AppendLine();
            sb.AppendLine("            return (activators, lifetimes);");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (KeyValuePair<string, List<Type>> kvp in installersByScene.Where(kv => kv.Key != FastIoCConstants.GLOBAL_SCOPE))
            {
                string sceneName = kvp.Key;
                string sceneNameSafe = NamingConventions.EscapeSceneName(sceneName);

                sb.AppendLine($"        private static (RecipeActivator[], Lifetime[]) Create{sceneNameSafe}Recipe()");
                sb.AppendLine("        {");
                sb.AppendLine("            int maxTypeId = GeneratedTypeIds.MaxTypeId;");
                sb.AppendLine("            RecipeActivator[] activators = new RecipeActivator[maxTypeId + 1];");
                sb.AppendLine("            Lifetime[] lifetimes = new Lifetime[maxTypeId + 1];");
                sb.AppendLine();

                List<Type> sceneGlobalTypes = typeToScopes
                    .Where(typeKvp => typeKvp.Value.Contains(FastIoCConstants.GLOBAL_SCOPE))
                    .Select(typeKvp => typeKvp.Key)
                    .ToList();

                List<Type> sceneTypes = typeToScopes
                    .Where(typeKvp =>
                        typeKvp.Value.Contains(sceneName) &&
                        !typeKvp.Value.Contains(FastIoCConstants.GLOBAL_SCOPE))
                    .Select(typeKvp => typeKvp.Key)
                    .ToList();

                foreach (Type type in sceneGlobalTypes.OrderBy(t => t.FullName ?? ""))
                {
                    if (typeIdMap.TryGetValue(type, out int typeId))
                    {
                        string methodName = NamingConventions.GetActivationMethodName(type.FullName ?? "Unknown");
                        Lifetime lifetime = typeLifetimes.TryGetValue(type, out Lifetime lt) ? lt : Lifetime.Transient;
                        sb.AppendLine($"            activators[{typeId}] = GeneratedActivators.Activate_{methodName};");
                        sb.AppendLine($"            lifetimes[{typeId}] = Lifetime.{lifetime};");
                    }
                }

                foreach (Type type in sceneTypes.OrderBy(t => t.FullName ?? ""))
                {
                    if (typeIdMap.TryGetValue(type, out int typeId))
                    {
                        string methodName = NamingConventions.GetActivationMethodName(type.FullName ?? "Unknown");
                        Lifetime lifetime = typeLifetimes.TryGetValue(type, out Lifetime lt) ? lt : Lifetime.Transient;
                        sb.AppendLine($"            activators[{typeId}] = GeneratedActivators.Activate_{methodName};");
                        sb.AppendLine($"            lifetimes[{typeId}] = Lifetime.{lifetime};");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("            return (activators, lifetimes);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }

        private static void GenerateHeader(StringBuilder sb)
        {
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Tools.FastIoC.Runtime.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace FastIoC.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Generated registry with recipes and activators.");
            sb.AppendLine("    /// Supports scene-specific dependency injection with scope-aware resolution.");
            sb.AppendLine("    /// Resolution order: Scene scope → Global scope");
            sb.AppendLine("    /// All scene runtimes are pre-compiled at edit-time for optimal performance.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class GeneratedRegistry");
            sb.AppendLine("    {");
        }

        private static void GenerateActivatorFields(StringBuilder sb, Dictionary<string, List<Type>> installersByScene)
        {
            sb.AppendLine("        // Pre-compiled activation arrays (readonly, reused across plays)");
            sb.AppendLine("        private static readonly RecipeActivator[] globalActivators;");
            sb.AppendLine("        private static readonly Lifetime[] globalLifetimes;");

            foreach (KeyValuePair<string, List<Type>> kvp in installersByScene.Where(kv => kv.Key != FastIoCConstants.GLOBAL_SCOPE))
            {
                string sceneNameSafe = NamingConventions.EscapeSceneName(kvp.Key);
                sb.AppendLine($"        private static readonly RecipeActivator[] {sceneNameSafe}Activators;");
                sb.AppendLine($"        private static readonly Lifetime[] {sceneNameSafe}Lifetimes;");
            }

            sb.AppendLine();
        }

        private static void GenerateStaticConstructor(StringBuilder sb, Dictionary<string, List<Type>> installersByScene)
        {
            sb.AppendLine("        // Static initializer: pre-compiles all activation recipes at edit-time");
            sb.AppendLine("        static GeneratedRegistry()");
            sb.AppendLine("        {");
            sb.AppendLine("            (globalActivators, globalLifetimes) = CreateGlobalRecipe();");

            foreach (KeyValuePair<string, List<Type>> kvp in installersByScene.Where(kv => kv.Key != FastIoCConstants.GLOBAL_SCOPE))
            {
                string sceneNameSafe = NamingConventions.EscapeSceneName(kvp.Key);
                sb.AppendLine($"            ({sceneNameSafe}Activators, {sceneNameSafe}Lifetimes) = Create{sceneNameSafe}Recipe();");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void GenerateEnsureInitializedMethod(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Ensure all recipes are pre-compiled. Call this at bootstrap to trigger static constructor.");
            sb.AppendLine("        /// Accessing a static field forces the class initializer to run (if not already run).");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static void EnsureInitialized()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Accessing static field triggers class constructor if not already run");
            sb.AppendLine("            _ = globalActivators;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void GenerateCreateSceneContextRuntimeMethod(StringBuilder sb, Dictionary<string, List<Type>> installersByScene)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Create runtime for a specific scene.");
            sb.AppendLine("        /// Returns fresh ContextRuntime wrapping pre-compiled arrays (Global + scene-specific bindings).");
            sb.AppendLine("        /// Each call creates a new instance to avoid disposal issues across plays.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static ContextRuntime CreateSceneContextRuntime(string sceneName)");
            sb.AppendLine("        {");
            sb.AppendLine("            return sceneName switch");
            sb.AppendLine("            {");

            foreach (KeyValuePair<string, List<Type>> kvp in installersByScene.Where(kv => kv.Key != FastIoCConstants.GLOBAL_SCOPE))
            {
                string sceneNameSafe = NamingConventions.EscapeSceneName(kvp.Key);
                sb.AppendLine($"                \"{kvp.Key}\" => new ContextRuntime(GeneratedTypeIds.MaxTypeId, {sceneNameSafe}Activators, {sceneNameSafe}Lifetimes),");
            }

            sb.AppendLine($"                _ => new ContextRuntime(GeneratedTypeIds.MaxTypeId, globalActivators, globalLifetimes) // Unknown scene, use Global");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void GenerateCreateProjectContextRuntimeMethod(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Create global runtime (all Global bindings).");
            sb.AppendLine("        /// Returns fresh ContextRuntime wrapping pre-compiled arrays.");
            sb.AppendLine("        /// Each call creates a new instance to avoid disposal issues across plays.");
            sb.AppendLine("        /// Scene-specific bindings are NOT included here.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static ContextRuntime CreateProjectContextRuntime()");
            sb.AppendLine("        {");
            sb.AppendLine("            return new ContextRuntime(GeneratedTypeIds.MaxTypeId, globalActivators, globalLifetimes);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void GenerateCreateSceneContextRuntimeFallbackMethod(StringBuilder sb)
        {
            sb.AppendLine("        public static ContextRuntime CreateSceneContextRuntime()");
            sb.AppendLine("        {");
            sb.AppendLine("            return new ContextRuntime(GeneratedTypeIds.MaxTypeId, globalActivators, globalLifetimes); // Fallback to global");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void GenerateFooter(StringBuilder sb)
        {
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }
    }
}
#endif
