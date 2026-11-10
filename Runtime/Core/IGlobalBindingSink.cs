using System;

namespace FastIoC.Core
{
    /// <summary>
    /// Receives FastIoC's global contracts so another DI container can expose them.
    /// Call <see cref="FastIoC.API.ProjectContext.ExportGlobalBindings"/> once, after the external
    /// container has registered its own bindings, so a single-owner check can detect duplicates.
    /// </summary>
    public interface IGlobalBindingSink
    {
        /// <summary>Registers one global contract and the binding that resolves it from FastIoC.</summary>
        void Add(Type contract, ExternalBinding binding);
    }
}
