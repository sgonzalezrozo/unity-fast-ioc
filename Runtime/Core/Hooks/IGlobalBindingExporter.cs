namespace FastIoC.Core
{
    /// <summary>
    /// Optional capability of the generated module: exports FastIoC's global contracts to a second
    /// DI container through <see cref="IGlobalBindingSink"/>. Kept separate from <see cref="IGeneratedModule"/>
    /// so a module generated before this feature existed still compiles; callers probe with
    /// <c>module is IGlobalBindingExporter</c>.
    /// </summary>
    public interface IGlobalBindingExporter
    {
        /// <summary>
        /// Hands every global contract to <paramref name="sink"/>. <paramref name="runtime"/> is the live
        /// ProjectContext runtime the bindings resolve from. Scene-scoped and framework bindings are excluded.
        /// </summary>
        void ExportGlobalBindings(ContextRuntime runtime, IGlobalBindingSink sink);
    }
}
