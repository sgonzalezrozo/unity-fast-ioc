using FastIoC.API;
using UnityEngine;

namespace FastIoC.UnityIntegration.Runtime.Bootstrap
{
    public class UnityLogger : IFastIoCLogger
    {
        public void Log(string message)
        {
            Debug.Log(message);
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        public void LogError(string message)
        {
            Debug.LogError(message);
        }
    }
}

