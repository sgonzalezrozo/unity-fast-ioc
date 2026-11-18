using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor
{
    /// <summary>
    /// Assets > FastIoC > New MonoInstaller: asks for a name and writes a [MonoInstaller] script in the selected folder,
    /// ready for Bind calls. Fast IoC finds it on its own, so there is nothing to add to a scene.
    /// </summary>
    internal static class MonoInstallerCreator
    {
        private const string DEFAULT_NAME = "GameplayInstaller";

        [MenuItem("Assets/FastIoC/New MonoInstaller", false, 23)]
        private static void NewMonoInstaller()
        {
            string folder = SelectedFolder();
            NamePromptWindow.Show(
                "New MonoInstaller",
                "Class Name",
                DEFAULT_NAME,
                $"Creates {folder}/<Name>.cs, a global [MonoInstaller]. Add scene names to the attribute to scope it.",
                name => Create(folder, name));
        }

        /// <summary>Writes the installer script and selects it. Returns its path, or null when the file exists.</summary>
        internal static string Create(string folder, string className)
        {
            className = ToIdentifier(className);
            string path = $"{folder}/{className}.cs";
            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("FastIoC", $"'{path}' already exists.", "OK");
                return null;
            }

            File.WriteAllText(path, Template(className));
            AssetDatabase.ImportAsset(path);

            Object script = AssetDatabase.LoadAssetAtPath<Object>(path);
            Selection.activeObject = script;
            EditorGUIUtility.PingObject(script);
            return path;
        }

        internal static string Template(string className) =>
$@"using FastIoC.API;

// Fast IoC finds [MonoInstaller] classes on its own: nothing to add to a scene.
// [MonoInstaller] is global. Scope it to scenes with [MonoInstaller(""Gameplay"", ""Shop"")],
// and order installers with [MonoInstaller(Priority = 10)] (higher runs first).
[MonoInstaller]
public class {className} : BindingRecorder
{{
    public override void InstallBindings()
    {{
        // Bind<IInventoryService>().To<InventoryService>().AsSingle();
    }}
}}
";

        /// <summary>A valid C# class name: letters, digits and underscores, not starting with a digit.</summary>
        internal static string ToIdentifier(string name)
        {
            System.Text.StringBuilder builder = new();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    builder.Append(c);
            }

            if (builder.Length == 0)
                return DEFAULT_NAME;

            if (char.IsDigit(builder[0]))
                builder.Insert(0, '_');

            return builder.ToString();
        }

        /// <summary>The selected folder, the folder of the selected asset, or the folder open in the Project window.</summary>
        private static string SelectedFolder()
        {
            string path = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (!string.IsNullOrEmpty(path))
                return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/');

            // ProjectWindowUtil.TryGetActiveFolderPath is internal.
            MethodInfo method = typeof(ProjectWindowUtil).GetMethod("TryGetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            if (method != null)
            {
                object[] args = { null };
                if ((bool)method.Invoke(null, args) && args[0] is string active && AssetDatabase.IsValidFolder(active))
                    return active;
            }

            return "Assets";
        }
    }
}
