using ToolsCore;
using FastIoC.API;
using UnityEngine;

namespace FastIoC.Adapters
{
    public sealed class FastIoCMonoBehaviourInjector : IMonoBehaviourInjector
    {
        public void InjectIfNeeded(MonoBehaviour component)
        {
            SceneInjector.InjectIfNeeded(component);
        }
    }
}
