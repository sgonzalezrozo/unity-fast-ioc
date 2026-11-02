namespace Tools.FastIoC.Runtime.Core
{
    public static class FastIoCConstants
    {
        // Framework type names
        public const string IINSTANTIATOR_TYPE = "Tools.FastIoC.Runtime.Core.IInstantiator";
        public const string INSTANTIATION_FACTORY_TYPE = "Tools.FastIoC.Runtime.Core.InstantiationFactory";
        public const string IPREFAB_INSTANTIATOR_TYPE = "Tools.Core.IPrefabInstantiator";
        public const string FAST_IOC_PREFAB_INSTANTIATOR_TYPE = "Tools.FastIoC.Runtime.Adapters.FastIoCPrefabInstantiator";
        public const string CONTEXT_RUNTIME_TYPE = "Tools.FastIoC.Runtime.Core.ContextRuntime";
        public const string GENERATED_MONO_INJECTORS_TYPE = "FastIoC.Generated.GeneratedMonoInjectors";
        public const string PROJECT_CONTEXT_TYPE = "Tools.FastIoC.Runtime.API.ProjectContext";

        // Method names
        public const string METHOD_INJECT = "Inject";
        public const string METHOD_INSTALL_BINDINGS = "InstallBindings";
        public const string METHOD_GET_BINDINGS_FOR_GENERATION = "GetBindingsForGeneration";
        public const string METHOD_INSTANCE = "Instance";
        public const string METHOD_RUNTIME = "Runtime";

        // Paths
        public const string GENERATED_PATH = "Assets/FastIoC/Generated";
        public const string GENERATED_NAMESPACE = "FastIoC.Generated";

        // Lifetimes
        public const string LIFETIME_TRANSIENT = "Transient";
        public const string LIFETIME_SINGLETON = "Singleton";

        // Global scope marker
        public const string GLOBAL_SCOPE = "*";

        // Installer naming
        public const string INSTALLER_SUFFIX = "Installer";
    }
}
