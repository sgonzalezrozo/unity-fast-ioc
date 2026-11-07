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
        private static readonly string STATUS_FILE_PATH = Path.Combine(
            FastIoCConstants.GENERATED_PATH,
            "GenerationStatus.json");

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
                Debug.Log($"[FastIoC] Generation status written: {status}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Failed to write generation status file: {ex.Message}");
            }
        }
    }
}
#endif
