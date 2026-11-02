using System;

namespace Tools.FastIoC.Runtime.API
{
    public interface ISceneLifecycleProvider
    {
        event Action<string> OnSceneLoaded;
        event Action<string> OnSceneUnloaded;

        string GetActiveSceneName();
    }
}

