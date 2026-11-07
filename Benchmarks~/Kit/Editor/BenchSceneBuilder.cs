using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FastIoCBench.Editor
{
    /// <summary>
    /// Builds the prefabs, the Zenject ProjectContext, the VContainer root scope and the scenes
    /// for a generated graph. Runs after the generated code has compiled. Third-party types are
    /// found by name, so this assembly doesn't reference Zenject or VContainer.
    /// </summary>
    public static class BenchSceneBuilder
    {
        public static readonly string[] Variants = { "none", "fastioc", "zenject", "vcontainer" };
        private const int GROUP_SIZE = 50;

        public static List<string> Build(BenchGraph g)
        {
            Directory.CreateDirectory(BenchPaths.PREFABS);
            Directory.CreateDirectory(BenchPaths.SCENES);
            Directory.CreateDirectory(BenchPaths.RESOURCES + "/FastIoCBench");

            GameObject[] prefabs = BuildPrefabs(g);
            BuildZenjectProjectContext();
            BuildVContainerRoot();

            List<string> scenes = new() { BuildBootScene() };
            foreach (string variant in Variants)
                scenes.Add(BuildInjectScene(g, variant, prefabs));

            EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
            AssetDatabase.SaveAssets();
            return scenes;
        }

        public static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static Type Required(string fullName)
        {
            return FindType(fullName) ?? throw new InvalidOperationException(
                $"[FastIoCBench] Type {fullName} not found. Check the Console for compile errors, and that Zenject and VContainer are installed.");
        }

        private static Type Gen(string name) => Required(BenchCodeEmitter.NAMESPACE + "." + name);

        private static GameObject[] BuildPrefabs(BenchGraph g)
        {
            List<GameObject> assets = new();
            foreach (BenchGraph.PrefabType prefab in g.Prefabs)
            {
                GameObject root = new(prefab.Name);
                root.AddComponent(Gen(prefab.Name));
                if (prefab.HasChild)
                {
                    GameObject child = new("Child");
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent(Gen(prefab.Name + "Child"));
                }

                string path = $"{BenchPaths.PREFABS}/{prefab.Name}.prefab";
                assets.Add(PrefabUtility.SaveAsPrefabAsset(root, path));
                Object.DestroyImmediate(root);
            }

            return assets.ToArray();
        }

        private static void BuildZenjectProjectContext()
        {
            GameObject go = new("ProjectContext");
            Component context = go.AddComponent(Required("Zenject.ProjectContext"));
            Component installer = go.AddComponent(Gen("ZenjectRootInstaller"));
            SetObjects(context, "_monoInstallers", new Object[] { installer });
            PrefabUtility.SaveAsPrefabAsset(go, $"{BenchPaths.RESOURCES}/ProjectContext.prefab");
            Object.DestroyImmediate(go);
        }

        private static void BuildVContainerRoot()
        {
            GameObject go = new("VContainerRoot");
            go.AddComponent(Gen("VContainerRootScope"));
            PrefabUtility.SaveAsPrefabAsset(go, $"{BenchPaths.RESOURCES}/FastIoCBench/VContainerRoot.prefab");
            Object.DestroyImmediate(go);
        }

        private static string BuildBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            new GameObject("Boot").AddComponent(Gen("BenchBoot"));

            string path = $"{BenchPaths.SCENES}/Boot.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static string BuildInjectScene(BenchGraph g, string variant, GameObject[] prefabs)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject("BenchSceneMarker").AddComponent(Gen("BenchSceneMarker"));

            GameObject probes = new("Probes");
            for (int f = 0; f < g.Probes.Count; f++)
            {
                GameObject go = new(g.Probes[f].Name);
                go.transform.SetParent(probes.transform, false);
                SetInt(go.AddComponent(Gen(g.Probes[f].Name)), "benchIndex", f);
            }

            // Scene objects grouped like a real hierarchy, not one flat list.
            GameObject injectables = new("Injectables");
            Transform group = null;
            for (int i = 0; i < g.MonoInstanceTypes.Count; i++)
            {
                if (i % GROUP_SIZE == 0)
                {
                    group = new GameObject($"Group{i / GROUP_SIZE:000}").transform;
                    group.SetParent(injectables.transform, false);
                }

                BenchGraph.MonoType type = g.MonoTypes[g.MonoInstanceTypes[i]];
                GameObject go = new($"{type.Name}_{i}");
                go.transform.SetParent(group, false);
                SetInt(go.AddComponent(Gen(type.Name)), "benchIndex", g.Probes.Count + i);
            }

            GameObject spawner = new("Spawner");
            string spawnerType = variant switch
            {
                "fastioc" => "FastIoCSpawner",
                "zenject" => "ZenjectSpawner",
                "vcontainer" => "VContainerSpawner",
                _ => "NoneSpawner",
            };
            SetObjects(spawner.AddComponent(Gen(spawnerType)), "prefabs", prefabs);

            if (variant == "zenject")
            {
                new GameObject("SceneContext").AddComponent(Required("Zenject.SceneContext"));
            }
            else if (variant == "vcontainer")
            {
                Component scope = new GameObject("LifetimeScope").AddComponent(Gen("VContainerSceneScope"));
                SerializedObject so = new(scope);
                SerializedProperty typeName = so.FindProperty("parentReference.TypeName");
                if (typeName == null)
                    throw new InvalidOperationException("[FastIoCBench] VContainer LifetimeScope has no parentReference.TypeName. Check the VContainer version.");
                typeName.stringValue = Gen("VContainerRootScope").FullName;
                so.ApplyModifiedPropertiesWithoutUndo();
                SetObjects(scope, "autoInjectGameObjects", new Object[] { probes, injectables, spawner });
            }

            string path = $"{BenchPaths.SCENES}/Inject_{variant}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void SetInt(Component component, string property, int value)
        {
            SerializedObject so = new(component);
            SerializedProperty p = so.FindProperty(property)
                ?? throw new InvalidOperationException($"[FastIoCBench] {component.GetType().Name} has no serialized field {property}.");
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjects(Component component, string property, Object[] values)
        {
            SerializedObject so = new(component);
            SerializedProperty p = so.FindProperty(property)
                ?? throw new InvalidOperationException($"[FastIoCBench] {component.GetType().Name} has no serialized field {property}. Check the {component.GetType().Namespace} version.");
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
