#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FastIoC.Editor.Generation;
using FastIoC.Core.Abstraction;
using FastIoC.UnityIntegration.Runtime;
using FastIoC.UnityIntegration.Runtime.Providers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FastIoC.UnityIntegration.Editor
{
    /// <summary>
    /// Setup integration for FastIoC in Unity Editor.
    ///
    /// Provides menu items for:
    /// - Initial setup
    /// - Manual code regeneration
    /// - Force regeneration (clear caches)
    /// </summary>
    [InitializeOnLoad]
    public static class FastIoCEditorSetup
    {
        static FastIoCEditorSetup()
        {
            try
            {
                // Initialize FastIoC provider registry for editor operations
                if (!FastIoCProviderRegistry.IsInitialized)
                {
                    ITypeMetadataProvider typeProvider = new UnityAppDomainMetadataProvider();
                    IContextLifecycleObserver lifecycleObserver = new UnityContextLifecycleObserver();

                    FastIoCProviderRegistry.Initialize(typeProvider, lifecycleObserver);
                    Debug.Log("[FastIoC] Editor: Provider registry initialized via [InitializeOnLoad]");
                }
                else
                {
                    Debug.Log("[FastIoC] Editor: Provider registry already initialized");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Failed to initialize provider registry in [InitializeOnLoad]: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void EnsureEditorProviderInitialized()
        {
            try
            {
                if (!FastIoCProviderRegistry.IsInitialized)
                {
                    ITypeMetadataProvider typeProvider = new UnityAppDomainMetadataProvider();
                    IContextLifecycleObserver lifecycleObserver = new UnityContextLifecycleObserver();

                    FastIoCProviderRegistry.Initialize(typeProvider, lifecycleObserver);
                    Debug.Log("[FastIoC] Editor: Provider registry initialized via EnsureEditorProviderInitialized");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Failed to ensure provider initialization: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [MenuItem("Tools/FastIoC/Setup", priority = 1)]
        public static void SetupProject()
        {
            EnsureEditorProviderInitialized();
            FastIoC.Editor.FastIoCSymbolEnabler.EnsureFastIoCSymbolEnabled();
            bool hasGeneratedCode = Directory.Exists("Assets/FastIoC/Generated");

            if (hasGeneratedCode)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "FastIoC Setup",
                    "FastIoC has already been set up.\n\nDo you want to regenerate the code?",
                    "Regenerate",
                    "Cancel");

                if (!overwrite)
                    return;
            }

            // Add core components to ProjectContext prefab BEFORE code generation
            AddCoreComponentsToPrefab();

            // Generate code
            GenerationCoordinator.GenerateAllRetryingFailedCode();

            EditorUtility.DisplayDialog(
                "FastIoC Setup Complete!",
                "FastIoC code generation finished successfully.\n\n" +
                "Generated files location:\n" +
                "• Assets/FastIoC/Generated/\n\n" +
                "ProjectContext prefab location:\n" +
                "• Assets/Resources/Tools/FastIoC/ProjectContext.prefab\n\n" +
                "Next steps:\n" +
                "1. Create a MonoInstaller subclass\n" +
                "2. Override InstallBindings() to configure bindings\n" +
                "3. Regenerate code when bindings change\n" +
                "4. Use ProjectContext and scene contexts in your scenes",
                "OK");

            Debug.Log("[FastIoC] Setup complete!");
        }

        [MenuItem("Tools/FastIoC/Regenerate Code", priority = 2)]
        public static void RegenerateCode()
        {
            EnsureEditorProviderInitialized();
            GenerationCoordinator.GenerateAllRetryingFailedCode();
            EditorUtility.DisplayDialog("FastIoC", "Code regeneration complete!", "OK");
        }

        public static void ForceRegenerateCode()
        {
            EnsureEditorProviderInitialized();
            Debug.Log("[FastIoC] Force regenerating... (clearing auto-detection cache)");

            // Clear the hash cache to force regeneration
            FastIoCEditorAutoRegen.ClearLastHash();

            // Regenerate code
            GenerationCoordinator.GenerateAllRetryingFailedCode();


            EditorUtility.DisplayDialog(
                "FastIoC Force Regenerate",
                "Code regeneration complete!\n\n" +
                "The auto-detection cache has been cleared.\n" +
                "Deleted types should now be detected and removed.",
                "OK");
            Debug.Log("[FastIoC] Force regeneration complete!");
        }

        private static void AddCoreComponentsToPrefab()
        {
            string prefabPath = "Assets/Resources/Tools/FastIoC/ProjectContext.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.Log($"[FastIoC] ProjectContext prefab not found at {prefabPath}. Creating it...");

                string directory = Path.GetDirectoryName(prefabPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                GameObject newGo = new GameObject("ProjectContext");

                Type projectContextType = FindTypeInLoadedAssemblies("FastIoC.API.ProjectContext");
                if (projectContextType == null)
                {
                    Debug.LogError("[FastIoC] Could not find FastIoC.API.ProjectContext type. Make sure the FastIoC runtime assembly is compiled.");
                    Object.DestroyImmediate(newGo);
                    return;
                }

                newGo.AddComponent(projectContextType);

                Type sceneInjectorType = FindTypeInLoadedAssemblies("FastIoC.API.SceneInjector");
                if (sceneInjectorType != null)
                    newGo.AddComponent(sceneInjectorType);

                prefab = PrefabUtility.SaveAsPrefabAsset(newGo, prefabPath);
                Object.DestroyImmediate(newGo);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (prefab == null)
                {
                    Debug.LogError($"[FastIoC] Failed to create ProjectContext prefab at {prefabPath}");
                    return;
                }

                Debug.Log($"[FastIoC] Created ProjectContext prefab at {prefabPath} with ProjectContext and SceneInjector components.");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                Debug.LogError("[FastIoC] Failed to instantiate ProjectContext prefab for modification.");
                return;
            }

            bool modified = false;

            try
            {
                // Ensure ProjectContext component is present
                Type projectContextType = FindTypeInLoadedAssemblies("FastIoC.API.ProjectContext");
                if (projectContextType != null && instance.GetComponent(projectContextType) == null)
                {
                    instance.AddComponent(projectContextType);
                    Debug.Log("[FastIoC] Added ProjectContext to prefab.");
                    modified = true;
                }

                // Ensure SceneInjector component is present
                Type sceneInjectorType = FindTypeInLoadedAssemblies("FastIoC.API.SceneInjector");
                if (sceneInjectorType != null && instance.GetComponent(sceneInjectorType) == null)
                {
                    instance.AddComponent(sceneInjectorType);
                    Debug.Log("[FastIoC] Added SceneInjector to prefab.");
                    modified = true;
                }
            }
            finally
            {
                if (modified)
                {
                    PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
                    Debug.Log("[FastIoC] ProjectContext prefab updated and saved.");
                }

                Object.DestroyImmediate(instance);
                AssetDatabase.Refresh();
            }
        }

        private static Type FindTypeInLoadedAssemblies(string fullTypeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullTypeName, throwOnError: false);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
}
#endif
