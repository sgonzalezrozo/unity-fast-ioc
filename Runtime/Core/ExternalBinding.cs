namespace FastIoC.Core
{
    /// <summary>
    /// A single global binding handed to another DI container through <see cref="IGlobalBindingSink"/>.
    /// <see cref="Resolve"/> goes straight to the runtime, bypassing the <c>ServiceLocatorEnforcer</c> that
    /// guards <c>ProjectContext.Runtime</c>. This exists only to bridge FastIoC into a second container;
    /// it is not a general-purpose resolve path.
    /// </summary>
    public readonly struct ExternalBinding
    {
        private readonly ContextRuntime runtime;
        private readonly TypeId typeId;

        public ExternalBinding(ContextRuntime runtime, TypeId typeId)
        {
            this.runtime = runtime;
            this.typeId = typeId;
        }

        /// <summary>Resolves the bound instance from the FastIoC runtime.</summary>
        public object Resolve() => runtime.ResolveForExternalContainer(typeId);
    }
}
