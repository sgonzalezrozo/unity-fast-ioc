using System;
using Tools.FastIoC.Runtime.API;

namespace Tools.FastIoC.UnityIntegration.Runtime.Bootstrap
{
    public class ConsoleLogger : IFastIoCLogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }

        public void LogWarning(string message)
        {
            Console.Out.WriteLine($"[WARN] {message}");
        }

        public void LogError(string message)
        {
            Console.Error.WriteLine($"[ERROR] {message}");
        }
    }
}

