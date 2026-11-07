using System;
using UnityEngine;

namespace FastIoCBench
{
    /// <summary>Written by the Editor before each build (and before Play Mode) to Resources/FastIoCBenchBuildInfo.json.</summary>
    [Serializable]
    public sealed class BenchBuildInfo
    {
        public const string RESOURCE_NAME = "FastIoCBenchBuildInfo";

        public string buildId = "editor";
        public string profile = "";
        public string gitCommit = "";
        public bool gitDirty;
        public string fastIocVersion = "";
        public string zenjectVersion = "";
        public string vcontainerVersion = "";
        public string strippingLevel = "";
        public bool incrementalGc;
        public string builtAtUtc = "";
        public int rounds = 5;
        public double fastIocGenerationSeconds;

        public static BenchBuildInfo Load()
        {
            TextAsset asset = Resources.Load<TextAsset>(RESOURCE_NAME);
            if (asset == null)
                return null;

            try
            {
                return JsonUtility.FromJson<BenchBuildInfo>(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoCBench] Can't read build info: {ex.Message}");
                return null;
            }
        }
    }
}
