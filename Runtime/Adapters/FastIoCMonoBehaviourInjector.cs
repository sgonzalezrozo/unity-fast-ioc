using ToolsCore;
using Tools.FastIoC.Runtime.API;
using UnityEngine;

namespace Tools.FastIoC.Runtime.Adapters
{
    public sealed class FastIoCMonoBehaviourInjector : IMonoBehaviourInjector
    {
        public void InjectIfNeeded(MonoBehaviour component)
        {
            SceneInjector.InjectIfNeeded(component);
        }
    }
}
