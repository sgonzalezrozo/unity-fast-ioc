using System;
using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor
{
    /// <summary>A small popup that asks for a name, with Create and Cancel. Enter creates, Escape cancels.</summary>
    internal class NamePromptWindow : EditorWindow
    {
        private const string FIELD = "NamePromptField";

        private string label;
        private string help;
        private string value;
        private Action<string> onAccept;
        private bool focused;

        public static void Show(string title, string label, string defaultValue, string help, Action<string> onAccept)
        {
            NamePromptWindow window = CreateInstance<NamePromptWindow>();
            window.titleContent = new GUIContent(title);
            window.label = label;
            window.help = help;
            window.value = defaultValue;
            window.onAccept = onAccept;

            Vector2 size = new(380f, string.IsNullOrEmpty(help) ? 92f : 132f);
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(main.center - size / 2f, size);
            window.minSize = size;
            window.maxSize = size;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown)
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    Accept();
                    evt.Use();
                    return;
                }

                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.Use();
                    return;
                }
            }

            EditorGUILayout.Space(8);
            GUI.SetNextControlName(FIELD);
            value = EditorGUILayout.TextField(label, value);
            if (!focused)
            {
                EditorGUI.FocusTextInControl(FIELD);
                focused = true;
            }

            if (!string.IsNullOrEmpty(help))
                EditorGUILayout.HelpBox(help, MessageType.None);

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(90)))
                Close();

            GUI.enabled = IsValid(value);
            if (GUILayout.Button("Create", GUILayout.Width(90)))
                Accept();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
        }

        private void Accept()
        {
            if (!IsValid(value))
                return;

            Action<string> callback = onAccept;
            string accepted = Sanitize(value);
            Close();
            callback?.Invoke(accepted);
        }

        private static bool IsValid(string name)
            => !string.IsNullOrEmpty(Sanitize(name));

        /// <summary>The name without spaces or characters a file name can't have.</summary>
        public static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string trimmed = name.Replace(" ", "");
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(c.ToString(), "");
            return trimmed;
        }
    }
}
