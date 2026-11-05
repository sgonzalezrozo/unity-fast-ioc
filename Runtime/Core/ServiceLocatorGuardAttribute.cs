namespace FastIoC.Core
{
    /// <summary>
    /// Marks properties/fields that should not be used as Service Locators.
    /// Applied to properties that expose internal container access.
    /// If you find yourself accessing this property directly, you're probably using the anti-pattern.
    ///
    /// Service Locator Anti-Pattern:
    /// Bad:
    ///    var service = ProjectContext.Instance.Runtime.Resolve(typeId);
    ///
    /// Proper Dependency Injection:
    /// Good:
    ///    Constructor injection: public MyClass(IService service) { ... }
    ///    Field injection: Use IInstantiator for factories
    /// </summary>
    [System.AttributeUsage(
        System.AttributeTargets.Property | System.AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = false)]
    public class ServiceLocatorGuardAttribute : System.Attribute
    {
        /// <summary>
        /// Message to display when this property is accessed inappropriately.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Create a guard with custom message.
        /// </summary>
        public ServiceLocatorGuardAttribute(string message =
            "Direct access to Runtime exposes container and may indicate Service Locator anti-pattern. " +
            "Use constructor injection or IInstantiator factory instead. " +
            "This is only for FastIoC framework use.")
        {
            Message = message;
        }
    }
}

