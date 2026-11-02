namespace Tools.FastIoC.UnityIntegration.Runtime
{
    /// <summary>
    /// Helper for FastIoC provider initialization.
    ///
    /// FastIoC providers are initialized automatically via FastIoCUnityBootstrapper
    /// using [RuntimeInitializeOnLoadMethod]. This class is provided for reference.
    /// </summary>
    public static class FastIoCIntegrationInjections
    {
        /// <summary>
        /// Get access to the initialized provider registry.
        /// Providers are auto-initialized by FastIoCUnityBootstrapper at startup.
        /// </summary>
        public static void EnsureInitialized()
        {
            // Providers are managed globally via FastIoCProviderRegistry static methods
            // This method is a no-op but ensures FastIoCIntegration namespace is available
        }
    }
}



