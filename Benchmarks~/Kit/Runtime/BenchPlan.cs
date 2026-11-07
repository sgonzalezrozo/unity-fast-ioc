using System;
using System.IO;
using UnityEngine;

namespace FastIoCBench
{
    /// <summary>
    /// Which variant each launch runs. One variant per launch, so every measurement is a cold start.
    /// The order rotates every round. Stored in persistentDataPath and reset when a new build is installed.
    /// </summary>
    [Serializable]
    public sealed class BenchPlan
    {
        public string buildId;
        public string profile;
        public int rounds;
        public string[] variants;
        public int launchesStarted;
        public int launchesCompleted;

        public static string Folder => Path.Combine(Application.persistentDataPath, "FastIoCBenchmarks");
        private static string FilePath => Path.Combine(Folder, "plan.json");

        public int TotalLaunches => rounds * variants.Length;
        public bool IsDone => launchesStarted >= TotalLaunches;

        public int RoundOf(int launch) => launch / variants.Length;

        public string VariantOf(int launch)
        {
            int round = RoundOf(launch);
            int slot = launch % variants.Length;
            return variants[(slot + round) % variants.Length];
        }

        public static BenchPlan LoadOrCreate(string buildId, string profile, int rounds, string[] variants)
        {
            Directory.CreateDirectory(Folder);

            if (File.Exists(FilePath))
            {
                try
                {
                    BenchPlan existing = JsonUtility.FromJson<BenchPlan>(File.ReadAllText(FilePath));
                    if (existing != null && existing.buildId == buildId && existing.profile == profile)
                        return existing;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FastIoCBench] Plan unreadable, starting a new one: {ex.Message}");
                }
            }

            BenchPlan plan = new()
            {
                buildId = buildId,
                profile = profile,
                rounds = Math.Max(1, rounds),
                variants = variants,
            };
            plan.Save();
            return plan;
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
        }
    }
}
