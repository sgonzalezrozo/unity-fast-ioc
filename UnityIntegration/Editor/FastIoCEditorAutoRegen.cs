#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FastIoC.API;
using FastIoC.Core.Abstraction;
using FastIoC.Editor.Graph;
using FastIoC.Editor.Generation;
using FastIoC.UnityIntegration.Runtime;
using FastIoC.UnityIntegration.Runtime.Providers;
using UnityEditor;
using UnityEngine;

namespace FastIoC.UnityIntegration.Editor
{
    /// <summary>
    /// Regenerates code after a script compile, when installers, bindings, constructors
    /// or injected fields have changed. These can only change with a recompile, so one
    /// check after each domain reload is enough.
    /// </summary>
    [InitializeOnLoad]
    public class FastIoCEditorAutoRegen
    {
        private const string LAST_HASH_KEY = "FastIoC_LastChangeHash";

        private static bool isRegenerating;

        // EditorPrefs is shared between projects, so the key includes this project's path.
        private static string ProjectHashKey => $"{LAST_HASH_KEY}_{Application.dataPath}";

        static FastIoCEditorAutoRegen()
        {
            if (!FastIoCProviderRegistry.IsInitialized)
            {
                ITypeMetadataProvider typeProvider = new UnityAppDomainMetadataProvider();
                IContextLifecycleObserver lifecycleObserver = new UnityContextLifecycleObserver();

                FastIoCProviderRegistry.Initialize(typeProvider, lifecycleObserver);
                Debug.Log("[FastIoC] AutoRegen: Provider registry initialized");
            }

            // CI runs generation explicitly, so it can check the result.
            if (Application.isBatchMode)
                return;

            EditorApplication.delayCall += CheckForChangesAndRegenerate;
        }

        /// <summary>Forgets the last generated state, so the next check regenerates.</summary>
        public static void ClearLastHash()
        {
            EditorPrefs.DeleteKey(ProjectHashKey);
        }

        private static void CheckForChangesAndRegenerate()
        {
            if (isRegenerating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!FastIoCProviderRegistry.IsInitialized)
                return;

            string currentHash = ComputeHash();

            if (string.IsNullOrEmpty(currentHash) || currentHash == EditorPrefs.GetString(ProjectHashKey, ""))
                return;

            Debug.Log("[FastIoC] Changes detected - regenerating code...");
            isRegenerating = true;
            try
            {
                GenerationCoordinator.GenerateAll();

                // After a failure, the next compile tries again.
                if (!GenerationStatusReader.WasLastGenerationFailed(out _))
                    EditorPrefs.SetString(ProjectHashKey, currentHash);
            }
            finally
            {
                isRegenerating = false;
            }
        }

        private static string ComputeHash()
        {
            try
            {
                StringBuilder sb = new();
                AppendInjectedFields(sb);
                AppendInstallers(sb);

                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                    return Convert.ToBase64String(hash);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Error computing hash: {ex.Message}");
                return "";
            }
        }

        private static void AppendInjectedFields(StringBuilder sb)
        {
            var monoBehaviours = FastIoCProviderRegistry.TypeProvider.GetTypes(t =>
                typeof(MonoBehaviour).IsAssignableFrom(t)
                && !t.IsAbstract
                && !t.IsGenericTypeDefinition);

            foreach (Type type in monoBehaviours.OrderBy(t => t.FullName ?? ""))
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (FieldInfo field in fields.Where(f => f.IsAssembly).OrderBy(f => f.Name))
                {
                    sb.Append(type.FullName).Append('|')
                      .Append(field.Name).Append(':')
                      .Append(field.FieldType.FullName).AppendLine();
                }
            }
        }

        private static void AppendInstallers(StringBuilder sb)
        {
            Dictionary<string, List<Type>> installersByScene = InstallerGraphCollector.CollectInstallersByScene();

            foreach (KeyValuePair<string, List<Type>> scene in installersByScene.OrderBy(kvp => kvp.Key))
            {
                sb.Append(scene.Key).Append('=');
                foreach (Type installer in scene.Value)
                    sb.Append(installer.FullName).Append(',');
                sb.AppendLine();
            }

            InstallerGraphCollector.ClearBindingInfoCache();
            InstallerGraphCollector.AnalyzeInstallerBindings(InstallerGraphCollector.CollectInstallers());

            foreach (KeyValuePair<Type, BindingInfo> binding in InstallerGraphCollector.GetCachedBindingInfo().OrderBy(kvp => kvp.Key.FullName))
            {
                BindingInfo info = binding.Value;
                sb.Append(binding.Key.FullName).Append('>')
                  .Append(info.ImplementationType.FullName).Append('|')
                  .Append(info.LifetimeName).Append('|')
                  .Append(info.IsInstance).Append('|')
                  .Append(info.AssetPath).Append('|');

                ConstructorInfo[] constructors = info.ImplementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                if (constructors.Length > 0)
                {
                    foreach (ParameterInfo parameter in constructors[0].GetParameters())
                        sb.Append(parameter.ParameterType.FullName).Append(',');
                }

                sb.AppendLine();
            }
        }
    }
}
#endif
