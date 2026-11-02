using System;

namespace Tools.FastIoC.Runtime.Core
{
    public class ServiceLocatorViolation
    {
        public string CallerType { get; set; }
        public string CallerMethod { get; set; }
        public DateTime Timestamp { get; set; }
        public string StackTrace { get; set; }

        public override string ToString()
        {
            return $"{CallerType}.{CallerMethod} at {Timestamp:HH:mm:ss.fff}";
        }
    }
}
