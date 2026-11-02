using System;

namespace FastIoC.Core
{
    /// <summary>
    /// Phase 3: Compile-Time Guard - Attribute to prevent Service Locator wrapping.
    ///
    /// Apply this to internal framework methods to prevent external access.
    /// The Roslyn analyzer will reject any calls from user code.
    ///
    /// Example:
    ///   [PreventServiceLocator]
    ///   internal object Resolve(TypeId typeId) { }
    ///
    /// Violations:
    ///   - Calling from outside FastIoC framework → Compile error
    ///   - Wrapping in ServiceLocator class → Compile error
    ///   - Reflection invocation → Runtime error
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = true)]
    public class PreventServiceLocatorAttribute : Attribute
    {
        /// <summary>
        /// Error message to show when violated.
        /// </summary>
        public string Message { get; set; } =
            "This method/property is only for FastIoC framework use and cannot be called directly. " +
            "Use dependency injection via internal fields fields or constructor injection instead.";

        /// <summary>
        /// Whitelist of allowed caller types (full names).
        /// Leave empty to use default whitelist.
        /// </summary>
        public string[] AllowedCallers { get; set; } = Array.Empty<string>();

        public PreventServiceLocatorAttribute(string message = "")
        {
            if (!string.IsNullOrEmpty(message))
                Message = message;
        }
    }
}

