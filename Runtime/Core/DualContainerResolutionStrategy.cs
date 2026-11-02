using System;

namespace FastIoC.Core
{
    public class DualContainerResolutionStrategy : IResolutionStrategy
    {
        private readonly SceneContainerStack sceneStack;

        public DualContainerResolutionStrategy(SceneContainerStack sceneStack)
        {
            this.sceneStack = sceneStack ?? throw new ArgumentNullException(nameof(sceneStack));
        }

        public bool TryResolve(
            TypeId typeId,
            int typeIdValue,
            RecipeActivator[] globalActivators,
            Lifetime[] globalLifetimes,
            InstanceCacheArray cache,
            ContextRuntime contextRuntime,
            out object instance)
        {
            instance = null;

            if (FastIoCReflectionCache.IsInitialized &&
                FastIoCReflectionCache.GetInstanceFromRegistry != null)
            {
                try
                {
                    object registeredInstance = FastIoCReflectionCache.GetInstanceFromRegistry(typeIdValue);
                    if (registeredInstance != null)
                    {
                        instance = registeredInstance;
                        return true;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (sceneStack.TryResolveFromScene(typeIdValue, out RecipeActivator sceneActivator, out Lifetime sceneLifetime))
            {
                if (cache.TryGetCached(typeId, sceneLifetime, out object sceneCached))
                {
                    instance = sceneCached;
                    return true;
                }

                object sceneInstance = sceneActivator(contextRuntime);

                cache.SetCached(typeId, sceneLifetime, sceneInstance);

                instance = sceneInstance;
                return true;
            }

            if (typeIdValue < 0 || typeIdValue >= globalActivators.Length)
                return false;

            if (globalActivators[typeIdValue] == null)
                return false;

            Lifetime globalLifetime = globalLifetimes[typeIdValue];

            if (cache.TryGetCached(typeId, globalLifetime, out object globalCached))
            {
                instance = globalCached;
                return true;
            }

            RecipeActivator globalActivator = globalActivators[typeIdValue];
            object globalInstance = globalActivator(contextRuntime);

            cache.SetCached(typeId, globalLifetime, globalInstance);

            instance = globalInstance;
            return true;
        }
    }
}

