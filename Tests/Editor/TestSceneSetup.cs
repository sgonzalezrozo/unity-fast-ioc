using UnityEditor;
using UnityEngine;

namespace FastIoC.Tests.Editor
{
    /// <summary>
    /// Automatically adds test scenes to Build Settings.
    /// Runs on first import to ensure test infrastructure is ready.
    /// </summary>
    public class TestSceneSetup
    {
        private const string TEST_SCENE_PATH = "Assets/Tools/FastIoC/Tests/Scenes/TestScene.unity";
        private const string TEST_SCENE_2_PATH = "Assets/Tools/FastIoC/Tests/Scenes/TestScene2.unity";

        [InitializeOnLoadMethod]
        private static void SetupTestScenes()
        {
            // Add scenes to Build Settings if not already there
            AddScenesToBuildSettings(TEST_SCENE_PATH);
            AddScenesToBuildSettings(TEST_SCENE_2_PATH);

            Debug.Log("[FastIoC Tests] Test scenes added to Build Settings");
        }

        private static void AddScenesToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            // Check if scene already exists
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == scenePath)
                    return; // Already added
            }

            // Add scene to build settings
            System.Collections.Generic.List<EditorBuildSettingsScene> sceneList =
                new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);

            sceneList.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = sceneList.ToArray();

            Debug.Log($"[FastIoC Tests] Added scene to Build Settings: {scenePath}");
        }
    }
}
