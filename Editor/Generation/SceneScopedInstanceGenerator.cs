#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Tools.FastIoC.Editor.Discovery;
using Tools.FastIoC.Editor.Validation;
using Tools.FastIoC.Runtime.API;

namespace Tools.FastIoC.Editor.Generation
{
    public static class SceneScopedInstanceGenerator
    {
        public static Dictionary<string, string> GenerateSceneScopedInitializers(
            Dictionary<Type, BindingInfo> bindings,
            Dictionary<Type, int> typeIdMap)
        {
            Dictionary<string, string> sceneInitializers = new();

            Dictionary<string, List<Type>> installersByScene = MonoInstallerScanner.ScanAllByScene();

            foreach (KeyValuePair<string, List<Type>> sceneEntry in installersByScene)
            {
                string sceneName = sceneEntry.Key;
                List<Type> installersForScene = sceneEntry.Value;

                List<KeyValuePair<Type, BindingInfo>> sceneBindings = new();

                foreach (Type installerType in installersForScene)
                {
                    try
                    {
                        object installerInstance = Activator.CreateInstance(installerType);
                        MethodInfo getBindingsMethod = installerType.GetMethod("GetBindingsForGeneration",
                            BindingFlags.Public | BindingFlags.Instance);

                        if (getBindingsMethod != null)
                        {
                            Dictionary<Type, BindingInfo> installerBindings = getBindingsMethod.Invoke(installerInstance, null)
                                as Dictionary<Type, BindingInfo>;

                            if (installerBindings != null)
                            {
                                foreach (KeyValuePair<Type, BindingInfo> binding in installerBindings.Where(b => b.Value.IsPathBinding))
                                {
                                    sceneBindings.Add(new KeyValuePair<Type, BindingInfo>(binding.Key, binding.Value));
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (sceneBindings.Count <= 0)
                    continue;

                string code = GenerateSceneInitializer(sceneName, sceneBindings, typeIdMap);
                sceneInitializers[sceneName] = code;
            }

            return sceneInitializers;
        }

        private static string GenerateSceneInitializer(
            string sceneName,
            List<KeyValuePair<Type, BindingInfo>> bindings,
            Dictionary<Type, int> typeIdMap)
        {
            StringBuilder sb = new();

            HashSet<string> namespaces = new();
            foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
            {
                if (!string.IsNullOrEmpty(binding.Key.Namespace))
                    namespaces.Add(binding.Key.Namespace);
            }

            sb.AppendLine("using System;");
            sb.AppendLine("using Tools.FastIoC.Runtime.Core;");
            sb.AppendLine("using Tools.FastIoC.Runtime.API;");
            sb.AppendLine("using UnityEngine;");

            foreach (string ns in namespaces)
                sb.AppendLine($"using {ns};");

            sb.AppendLine();
            sb.AppendLine("namespace FastIoC.Generated");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class Scene{SanitizeSceneName(sceneName)}InstanceInitializer");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void InitializeInstances()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");

            foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
            {
                Type interfaceType = binding.Key;
                BindingInfo bindingInfo = binding.Value;

                if (typeIdMap.TryGetValue(interfaceType, out int typeId))
                {
                    string typeIdName = interfaceType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";

                    Type validatorType = typeof(AssetPathValidator);
                    if (validatorType != null)
                    {
                        sb.AppendLine($"                // Load asset: {interfaceType.Name}");
                        sb.AppendLine($"                // Path: {bindingInfo.AssetPath}");
                    }
                }
            }

            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                Debug.LogError($\"[FastIoC] Scene '{sceneName}' instance initialization failed: {{ex.Message}}\\n{{ex.StackTrace}}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string SanitizeSceneName(string sceneName)
        {
            return System.Text.RegularExpressions.Regex.Replace(sceneName, @"[^a-zA-Z0-9_]", "_");
        }
    }
}
#endif
