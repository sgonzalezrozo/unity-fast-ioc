#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using FastIoC.API;
using FastIoC.Core;
using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor.Validation
{
    /// <summary>
    /// Validates <c>FromProjectContext()</c> bindings before code generation.
    /// Generation fails with a clear message when any of these conditions is true:
    /// <list type="bullet">
    ///   <item>The binding is in a scene-specific installer (not global).</item>
    ///   <item>No FastIoC ProjectContext prefab can be found in the project.</item>
    ///   <item><c>ProjectContext.hostedRoot</c> is null or is active.</item>
    ///   <item>Zero or more than one component of the bound implementation type exists under <c>hostedRoot</c>.</item>
    /// </list>
    /// </summary>
    public static class HostedComponentValidator
    {
        /// <summary>
        /// Validates all <c>IsProjectContextBinding</c> entries in <paramref name="allBindings"/>.
        /// Throws <see cref="InvalidOperationException"/> on the first violation found.
        /// </summary>
        public static void Validate(
            Dictionary<Type, FastIoC.API.BindingInfo> allBindings,
            Dictionary<string, List<Type>> installersByScene)
        {
            // Collect hosted bindings first; if none exist skip all checks.
            List<(Type interfaceType, FastIoC.API.BindingInfo info)> hosted = new();
            foreach (KeyValuePair<Type, FastIoC.API.BindingInfo> kvp in allBindings)
            {
                if (kvp.Value.IsProjectContextBinding)
                    hosted.Add((kvp.Key, kvp.Value));
            }

            if (hosted.Count == 0)
                return;

            // Build the set of global installer types.
            HashSet<Type> globalInstallers = new();
            if (installersByScene.TryGetValue(FastIoCConstants.GLOBAL_SCOPE, out List<Type> globals))
                foreach (Type t in globals) globalInstallers.Add(t);

            // Validate each hosted binding.
            foreach ((Type interfaceType, FastIoC.API.BindingInfo info) in hosted)
            {
                // 1. Must be in a global installer.
                Type owner = FastIoC.Editor.Graph.InstallerGraphCollector.GetBindingOwner(interfaceType);
                if (owner != null && !globalInstallers.Contains(owner))
                {
                    throw new InvalidOperationException(
                        $"[FastIoC] FromProjectContext() is only allowed in global installers. " +
                        $"Type '{interfaceType.FullName}' is hosted but its installer '{owner.Name}' is scene-specific.");
                }
            }

            // 2–4. Prefab checks (load the FastIoC ProjectContext prefab once).
            (ProjectContext ctx, string prefabPath) = FindProjectContextPrefab();

            if (ctx == null)
            {
                throw new InvalidOperationException(
                    "[FastIoC] FromProjectContext() bindings require a FastIoC ProjectContext prefab. " +
                    "Run Tools > FastIoC > Setup Project to create it.");
            }

            // Read hostedRoot via serialized field (it's [SerializeField] private).
            SerializedObject so = new SerializedObject(ctx);
            SerializedProperty hostedRootProp = so.FindProperty("hostedRoot");
            GameObject hostedRoot = hostedRootProp?.objectReferenceValue as GameObject;

            if (hostedRoot == null)
            {
                throw new InvalidOperationException(
                    $"[FastIoC] ProjectContext prefab '{prefabPath}' has no hostedRoot assigned. " +
                    $"Create an inactive child GameObject on the prefab and assign it to the hostedRoot field.");
            }

            if (hostedRoot.activeSelf)
            {
                throw new InvalidOperationException(
                    $"[FastIoC] ProjectContext.hostedRoot '{hostedRoot.name}' must be inactive. " +
                    $"FastIoC activates it after injection. Disable it in the prefab.");
            }

            // 4. Each bound impl type must have exactly one component under hostedRoot.
            foreach ((Type interfaceType, FastIoC.API.BindingInfo info) in hosted)
            {
                Type implType = info.ImplementationType;
                Component[] found = hostedRoot.GetComponentsInChildren(implType, true);

                if (found.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"[FastIoC] No component of type '{implType.FullName}' found under " +
                        $"'{hostedRoot.name}' in prefab '{prefabPath}'. " +
                        $"Add the component to a child of hostedRoot.");
                }

                if (found.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"[FastIoC] {found.Length} components of type '{implType.FullName}' found under " +
                        $"'{hostedRoot.name}' in prefab '{prefabPath}'. " +
                        $"FromProjectContext() requires exactly one.");
                }
            }
        }

        private static (ProjectContext ctx, string path) FindProjectContextPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("ProjectContext t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                ProjectContext ctx = prefab.GetComponent<ProjectContext>();
                if (ctx != null)
                    return (ctx, path);
            }
            return (null, null);
        }
    }
}
#endif
