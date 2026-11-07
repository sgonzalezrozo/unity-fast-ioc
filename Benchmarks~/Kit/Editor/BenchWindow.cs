using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FastIoCBench.Editor
{
    public sealed class BenchWindow : EditorWindow
    {
        private List<BenchProfile> profiles;
        private int selected;

        [MenuItem("Tools/FastIoC Benchmark")]
        private static void Open() => GetWindow<BenchWindow>("FastIoC Benchmark");

        private void OnEnable()
        {
            profiles = BenchProfile.LoadAll();
            string current = BenchPipeline.CurrentProfile;
            selected = Mathf.Max(0, profiles.FindIndex(p => p.profileName == current));
        }

        private void OnGUI()
        {
            if (profiles == null || profiles.Count == 0)
            {
                EditorGUILayout.HelpBox($"No profiles found in {BenchProfile.ProfilesFolder ?? "(kit package not found)"}.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Current fixture", BenchPipeline.CurrentProfile ?? "none");
            EditorGUILayout.Space();

            selected = EditorGUILayout.Popup("Profile", selected, profiles.Select(p => $"{p.profileName}  ({p.bindingCount} bindings, {p.monoInjectableCount} scene objects)").ToArray());
            BenchProfile profile = profiles[selected];
            EditorGUILayout.LabelField(profile.description ?? "", EditorStyles.wordWrappedMiniLabel);
            BenchPipeline.Rounds = EditorGUILayout.IntField("Runs per variant", BenchPipeline.Rounds);
            EditorGUILayout.LabelField("Launches per device", $"{BenchPipeline.Rounds * BenchSceneBuilder.Variants.Length} ({string.Join(", ", BenchSceneBuilder.Variants)})");
            EditorGUILayout.LabelField("Stripping (Android / iOS)", $"{BenchBuild.StrippingLevel(BuildTarget.Android)} / {BenchBuild.StrippingLevel(BuildTarget.iOS)}");

            EditorGUILayout.Space();
            if (GUILayout.Button("1. Apply player settings"))
                BenchBuild.ApplyPlayerSettings();
            if (GUILayout.Button($"2. Generate {profile.profileName}"))
                BenchPipeline.Generate(profile);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(BenchPipeline.CurrentProfile) || EditorApplication.isCompiling))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("3. Build Android"))
                    BenchBuild.BuildPlayer(BuildTarget.Android);
                if (GUILayout.Button("3. Build iOS"))
                    BenchBuild.BuildPlayer(BuildTarget.iOS);
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(BenchPipeline.Status))
                EditorGUILayout.HelpBox(BenchPipeline.Status, MessageType.Info);

            if (EditorApplication.isCompiling)
                EditorGUILayout.HelpBox("Compiling...", MessageType.None);
        }

        private void OnInspectorUpdate() => Repaint();
    }
}
