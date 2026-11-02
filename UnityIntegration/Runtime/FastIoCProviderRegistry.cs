using System;
using FastIoC.Core.Abstraction;

namespace FastIoC.UnityIntegration.Runtime
{
    /// <summary>
    /// Global provider registry for FastIoC.
    ///
    /// This is the single point where all platform-specific providers are stored.
    /// All generators and runtime code access providers through this registry.
    ///
    /// Initialization sequence:
    /// 1. FastIoCUnityBootstrapper.Initialize() creates providers
    /// 2. Calls FastIoCProviderRegistry.Initialize(typeProvider, lifecycleObserver)
    /// 3. All generators now use FastIoCProviderRegistry.TypeProvider
    ///
    /// For portability:
    /// - Unity: Call Initialize with UnityAppDomainMetadataProvider
    /// - CLI: Call Initialize with FileSystemMetadataProvider
    /// - IDE: Call Initialize with RoslynMetadataProvider
    /// </summary>
    public static class FastIoCProviderRegistry
    {
        private static ITypeMetadataProvider s_typeProvider;
        private static IContextLifecycleObserver s_lifecycleObserver;
        private static bool s_initialized;

        /// <summary>
        /// Initialize the provider registry with platform-specific implementations.
        /// Call this ONCE during startup, before code generation or runtime.
        /// </summary>
        public static void Initialize(
            ITypeMetadataProvider typeProvider,
            IContextLifecycleObserver lifecycleObserver)
        {
            if (typeProvider == null)
                throw new ArgumentNullException(nameof(typeProvider), "[FastIoC] TypeProvider cannot be null");
            if (lifecycleObserver == null)
                throw new ArgumentNullException(nameof(lifecycleObserver), "[FastIoC] ContextLifecycleObserver cannot be null");

            s_typeProvider = typeProvider;
            s_lifecycleObserver = lifecycleObserver;
            s_initialized = true;
        }

        /// <summary>
        /// Get the type metadata provider.
        /// Throws if not initialized.
        /// </summary>
        public static ITypeMetadataProvider TypeProvider
        {
            get
            {
                if (!s_initialized || s_typeProvider == null)
                    throw new InvalidOperationException(
                        "[FastIoC] Provider registry not initialized. " +
                        "Call FastIoCProviderRegistry.Initialize(typeProvider, lifecycleObserver) during startup.");
                return s_typeProvider;
            }
        }

        /// <summary>
        /// Get the context lifecycle observer.
        /// Throws if not initialized.
        /// </summary>
        public static IContextLifecycleObserver LifecycleObserver
        {
            get
            {
                if (!s_initialized || s_lifecycleObserver == null)
                    throw new InvalidOperationException(
                        "[FastIoC] Lifecycle observer not initialized. " +
                        "Call FastIoCProviderRegistry.Initialize(...) during startup.");
                return s_lifecycleObserver;
            }
        }

        /// <summary>
        /// Check if registry is initialized (useful for defensive coding).
        /// </summary>
        public static bool IsInitialized => s_initialized && s_typeProvider != null && s_lifecycleObserver != null;

        /// <summary>
        /// Reset registry (testing only).
        /// </summary>
        internal static void Reset()
        {
            s_typeProvider = null;
            s_lifecycleObserver = null;
            s_initialized = false;
        }
    }
}

