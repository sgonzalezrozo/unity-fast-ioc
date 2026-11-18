#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FastIoC.Core;
using UnityEngine;

namespace FastIoC.Editor
{
    public static class GenerationStatusWriter
    {
        private const string STATUS_FILE_NAME = "GenerationStatus.json";

        private static readonly string STATUS_FILE_PATH = Path.Combine(
            FastIoCConstants.GENERATED_PATH,
            STATUS_FILE_NAME);

        // Git reads a .gitignore in any folder, and Unity doesn't import files that start with a dot.
        private static readonly string GIT_IGNORE_PATH = Path.Combine(FastIoCConstants.GENERATED_PATH, ".gitignore");

        private const string GIT_IGNORE_CONTENT =
            "# Written by Fast IoC. The generation status changes on every generation and is only read in the Editor.\n" +
            STATUS_FILE_NAME + "\n" +
            STATUS_FILE_NAME + ".meta\n";

        public static void WriteSuccessStatus(List<string> warnings = null)
        {
            GenerationStatus status = new(GenerationStatus.Status.Success) { warnings = new StringListWrapper(warnings) };
            WriteStatus(status);
        }

        public static void WriteFailureStatus(string errorReason, List<string> errorDetails = null)
        {
            GenerationStatus status = new(GenerationStatus.Status.Failed, errorReason, errorDetails);
            WriteStatus(status);
        }

        private static void WriteStatus(GenerationStatus status)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(STATUS_FILE_PATH);
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                string json = JsonUtility.ToJson(status, prettyPrint: true);
                File.WriteAllText(STATUS_FILE_PATH, json);
                EnsureStatusIsGitIgnored();
                Debug.Log($"[FastIoC] Generation status written: {status}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Failed to write generation status file: {ex.Message}");
            }
        }

        /// <summary>
        /// Keeps the status file out of version control, so a regeneration with the same result changes nothing to
        /// commit. Creates the .gitignore when it's missing, and adds the entries to one that doesn't have them.
        /// </summary>
        private static void EnsureStatusIsGitIgnored()
        {
            try
            {
                if (!File.Exists(GIT_IGNORE_PATH))
                {
                    File.WriteAllText(GIT_IGNORE_PATH, GIT_IGNORE_CONTENT);
                    return;
                }

                string existing = File.ReadAllText(GIT_IGNORE_PATH);
                if (existing.Contains(STATUS_FILE_NAME))
                    return;

                string separator = existing.Length == 0 || existing.EndsWith("\n") ? "" : "\n";
                File.AppendAllText(GIT_IGNORE_PATH, separator + GIT_IGNORE_CONTENT);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Couldn't write {GIT_IGNORE_PATH}: {ex.Message}");
            }
        }
    }
}
#endif
