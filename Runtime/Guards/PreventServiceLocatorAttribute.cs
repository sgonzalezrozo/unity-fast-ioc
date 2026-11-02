using System;

namespace FastIoC.Guards
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = true)]
    public class PreventServiceLocatorAttribute : Attribute
    {
        public string Message { get; set; } =
            "This method/property is only for FastIoC framework use and cannot be called directly. " +
            "Use dependency injection via internal fields or constructor injection instead.";

        public string[] AllowedCallers { get; set; } = Array.Empty<string>();

        public PreventServiceLocatorAttribute(string message = "")
        {
            if (!string.IsNullOrEmpty(message))
                Message = message;
        }
    }
}

