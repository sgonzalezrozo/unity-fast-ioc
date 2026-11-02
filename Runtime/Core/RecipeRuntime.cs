using System;

namespace FastIoC.Core
{
    /// <summary>
    /// Delegate type for all object creation in FastIoC.
    /// Takes a context and returns a newly created or cached instance.
    /// Generated at editor time for AOT compatibility.
    /// </summary>
    public delegate object RecipeActivator(ContextRuntime context);

    /// <summary>
    /// Stores the fixed recipe for creating/resolving an instance.
    /// Created during editor-time code generation and never modified at runtime.
    /// </summary>
    public readonly struct RecipeRuntime
    {
        public readonly TypeId TypeId;
        public readonly Lifetime Lifetime;
        public readonly RecipeActivator Activator;

        public RecipeRuntime(TypeId typeId, Lifetime lifetime, RecipeActivator activator)
        {
            TypeId = typeId;
            Lifetime = lifetime;
            Activator = activator ?? throw new ArgumentNullException(nameof(activator));
        }
    }
}

