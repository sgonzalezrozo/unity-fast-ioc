using System;

namespace FastIoC.API
{
    public interface ISceneLifecycleProvider
    {
        event Action<string> OnSceneLoaded;
        event Action<string> OnSceneUnloaded;

        string GetActiveSceneName();
    }
}

