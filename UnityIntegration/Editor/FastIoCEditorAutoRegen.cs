#if UNITY_EDITOR
using System;
using System.Linq;
using Tools.FastIoC.Editor.Generation;
using Tools.FastIoC.Runtime.Core.Abstraction;
using Tools.FastIoC.UnityIntegration.Runtime;
using Tools.FastIoC.UnityIntegration.Runtime.Providers;
using UnityEditor;
using UnityEngine;

namespace Tools.FastIoC.UnityIntegration.Editor
{
    [InitializeOnLoad]
    public class FastIoCEditorAutoRegen
    {
        private const string LAST_HASH_KEY = "FastIoC_LastChangeHash";
        private const string IS_REGENERATING_KEY = "FastIoC_IsRegenerating";
        private const float CHECK_INTERVAL_SECONDS = 2f;

        private static float lastCheckTime;
        private static string lastKnownHash;

        static FastIoCEditorAutoRegen()
        {
            if (!FastIoCProviderRegistry.IsInitialized)
            {
                ITypeMetadataProvider typeProvider = new UnityAppDomainMetadataProvider();
                IContextLifecycleObserver lifecycleObserver = new UnityContextLifecycleObserver();

                FastIoCProviderRegistry.Initialize(typeProvider, lifecycleObserver);
                Debug.Log("[FastIoC] AutoRegen: Provider registry initialized");
            }

            lastKnownHash = EditorPrefs.GetString(LAST_HASH_KEY, "");
            EditorApplication.update += CheckForChangesAndRegenerate;
        }

        private static void CheckForChangesAndRegenerate()
        {
            if (EditorPrefs.GetBool(IS_REGENERATING_KEY, false))
                return;

            if (Time.realtimeSinceStartup - lastCheckTime < CHECK_INTERVAL_SECONDS)
                return;

            lastCheckTime = Time.realtimeSinceStartup;

            if (!FastIoCProviderRegistry.IsInitialized)
                return;
            
            string currentHash = ComputeHashUsingProvider();
            
            if (currentHash == lastKnownHash || string.IsNullOrEmpty(currentHash)) 
                return;
            
            Debug.Log("[FastIoC] Changes detected - regenerating code...");
            EditorPrefs.SetBool(IS_REGENERATING_KEY, true);
            try
            {
                GenerationCoordinator.GenerateAll();
                lastKnownHash = currentHash;
                EditorPrefs.SetString(LAST_HASH_KEY, currentHash);
            }
            finally
            {
                EditorPrefs.SetBool(IS_REGENERATING_KEY, false);
            }
        }

        private static string ComputeHashUsingProvider()
        {
            try
            {
                var typeProvider = FastIoCProviderRegistry.TypeProvider;

                var monoBehaviours = typeProvider.GetTypes(t =>
                    typeof(MonoBehaviour).IsAssignableFrom(t)
                    && !t.IsAbstract
                    && !t.IsGenericTypeDefinition);

                System.Text.StringBuilder sb = new();
                foreach (Type type in monoBehaviours.OrderBy(t => t.FullName ?? ""))
                {
                    var fields = type.GetFields(
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    foreach (var field in fields.Where(f =>
                        !f.IsStatic && f.FieldType.IsClass && !f.FieldType.IsGenericType))
                    {
                        sb.Append(type.FullName).Append("|")
                          .Append(field.Name).Append(":")
                          .Append(field.FieldType.FullName).AppendLine();
                    }
                }

                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
                    return Convert.ToBase64String(hash);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Error computing hash: {ex.Message}");
                return "";
            }
        }
    }
}
#endif

