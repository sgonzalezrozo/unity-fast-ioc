using System;
using UnityEngine;

namespace FastIoC.Fixtures
{
    /// <summary>A Unity object that implements IDisposable: the container must never dispose it.</summary>
    public sealed class DisposableAsset : ScriptableObject, IDisposable
    {
        public void Dispose() { }
    }
}
