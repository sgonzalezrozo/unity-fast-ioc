using System;
using System.IO;
using FastIoC.Core;
using UnityEngine;

namespace FastIoC.API
{
    public static class GenerationStatusReader
    {
        public static GenerationStatus TryReadStatus()
        {
#if UNITY_EDITOR
            try
            {
                string statusPath = Path.Combine(
                    FastIoCConstants.GENERATED_PATH,
                    "GenerationStatus.json");

                if (!File.Exists(statusPath))
                    return null;

                string json = File.ReadAllText(statusPath);
                GenerationStatus status = JsonUtility.FromJson<GenerationStatus>(json);
                return status;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Failed to read generation status: {ex.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        public static bool WasLastGenerationFailed(out GenerationStatus status)
        {
            status = TryReadStatus();
            return status != null && status.status == GenerationStatus.Status.Failed;
        }
    }
}

