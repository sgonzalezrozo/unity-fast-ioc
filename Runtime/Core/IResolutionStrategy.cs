namespace FastIoC.Core
{
    /// <summary>
    /// Strategy interface for resolving type instances from DI container.
    /// Abstracts resolution logic from ContextRuntime, allowing multiple strategies.
    /// </summary>
    public interface IResolutionStrategy
    {
        /// <summary>
        /// Attempt to resolve an instance by TypeId.
        /// </summary>
        /// <returns>True if resolution succeeded, false otherwise.</returns>
        bool TryResolve(
            TypeId typeId,
            int typeIdValue,
            RecipeActivator[] globalActivators,
            Lifetime[] globalLifetimes,
            InstanceCacheArray cache,
            ContextRuntime contextRuntime,
            out object instance);
    }
}

