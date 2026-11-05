namespace FastIoC.Guards
{
    [System.AttributeUsage(
        System.AttributeTargets.Property | System.AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = false)]
    public class ServiceLocatorGuardAttribute : System.Attribute
    {
        public string Message { get; }

        public ServiceLocatorGuardAttribute(string message =
            "Direct access to Runtime exposes container and may indicate Service Locator anti-pattern. " +
            "Use constructor injection or IInstantiator factory instead. " +
            "This is only for FastIoC framework use.")
        {
            Message = message;
        }
    }
}

