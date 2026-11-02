namespace Tools.FastIoC.Runtime.API
{
    public interface IFastIoCLogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}

