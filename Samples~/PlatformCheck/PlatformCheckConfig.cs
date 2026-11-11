using UnityEngine;

namespace FastIoC.Samples.PlatformCheck
{
    /// <summary>
    /// An asset outside any Resources folder, bound by name. Fast IoC loads it through its generated asset manifest,
    /// the path most exposed to code stripping: nothing in code references this asset.
    /// </summary>
    public class PlatformCheckConfig : ScriptableObject
    {
        public int answer;
    }
}
