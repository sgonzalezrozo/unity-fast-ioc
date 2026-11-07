using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FastIoCBench.Editor
{
    /// <summary>A size profile from Benchmarks~/profiles/*.json.</summary>
    [Serializable]
    public sealed class BenchProfile
    {
        public string profileName;
        public int seed;
        public int bindingCount;
        public int monoInjectableCount;
        public int featureCount;
        public int maxDepth;
        public float crossFeatureDependencyRatio;
        public int runtimePrefabInjectionCount;
        public int multiInterfaceTypeCount;
        public string description;

        public static string ProfilesFolder
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BenchProfile).Assembly);
                // The kit lives in unity-fast-ioc/Benchmarks~/Kit; profiles are in Benchmarks~/profiles.
                return package == null ? null : Path.GetFullPath(Path.Combine(package.resolvedPath, "..", "profiles"));
            }
        }

        public static List<BenchProfile> LoadAll()
        {
            List<BenchProfile> profiles = new();
            string folder = ProfilesFolder;
            if (folder == null || !Directory.Exists(folder))
                return profiles;

            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                BenchProfile profile = JsonUtility.FromJson<BenchProfile>(File.ReadAllText(file));
                if (profile != null && !string.IsNullOrEmpty(profile.profileName))
                    profiles.Add(profile);
            }

            profiles.Sort((a, b) => a.bindingCount.CompareTo(b.bindingCount));
            return profiles;
        }
    }
}
