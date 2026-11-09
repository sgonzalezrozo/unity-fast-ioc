using UnityEngine;

namespace FastIoC.API
{
    /// <summary>
    /// Holds direct asset references for <c>FromInstance</c> path bindings whose assets are outside
    /// any <c>Resources/</c> folder and are not marked as Addressables.
    /// Generated at edit time by <c>AssetManifestWriter</c> and stored at
    /// <c>Assets/Resources/Tools/FastIoC/AssetManifest.asset</c> so it can be loaded at runtime
    /// with <c>Resources.Load</c> without any path gymnastics.
    /// </summary>
    public class FastIoCAssetManifest : ScriptableObject
    {
#pragma warning disable CS0649
        [SerializeField] private Object[] assets;
#pragma warning restore CS0649

        /// <summary>Returns the asset at <paramref name="index"/>, or null if the index is out of range.</summary>
        public Object Get(int index)
        {
            if (assets != null && (uint)index < (uint)assets.Length)
                return assets[index];
            return null;
        }

        /// <summary>Number of assets stored in the manifest.</summary>
        public int Count => assets != null ? assets.Length : 0;
    }
}
