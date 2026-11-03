namespace FastIoC.Core
{
    /// <summary>
    /// Holds the generated module. The generated code calls <see cref="Register"/> on startup
    /// (and in the editor on load), so the runtime gets it without reflection.
    /// </summary>
    public static class GeneratedModuleRegistry
    {
        private static IGeneratedModule s_module;

        public static IGeneratedModule Module => s_module;

        public static bool IsRegistered => s_module != null;

        /// <summary>Called by the generated GeneratedModule. Not meant for user code.</summary>
        public static void Register(IGeneratedModule module)
        {
            s_module = module;
        }

        /// <summary>Testing only.</summary>
        public static void Clear()
        {
            s_module = null;
        }
    }
}
