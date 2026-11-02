using System;

namespace Tools.FastIoC.Runtime.Core
{
    public class InstanceCacheArray
    {
        private readonly object[] singletonCache;
        private readonly object[] sceneSingletonCache;
        private readonly object[] cachedCache;

        public InstanceCacheArray(int maxTypeId)
        {
            singletonCache = new object[maxTypeId + 1];
            sceneSingletonCache = new object[maxTypeId + 1];
            cachedCache = new object[maxTypeId + 1];
        }

        public bool TryGetCached(TypeId typeId, Lifetime lifetime, out object instance)
        {
            instance = null;

            if (lifetime == Lifetime.Transient)
                return false;

            int index = typeId.Id;
            if (index < 0 || index >= singletonCache.Length)
                return false;

            object[] cache = lifetime switch
            {
                Lifetime.Singleton => singletonCache,
                Lifetime.SceneSingleton => sceneSingletonCache,
                Lifetime.Cached => cachedCache,
                _ => null
            };

            if (cache != null)
            {
                instance = cache[index];
                return instance != null;
            }

            return false;
        }

        public void SetCached(TypeId typeId, Lifetime lifetime, object instance)
        {
            if (lifetime == Lifetime.Transient)
                return;

            int index = typeId.Id;
            if (index < 0 || index >= singletonCache.Length)
                return;

            object[] cache = lifetime switch
            {
                Lifetime.Singleton => singletonCache,
                Lifetime.SceneSingleton => sceneSingletonCache,
                Lifetime.Cached => cachedCache,
                _ => null
            };

            if (cache != null)
                cache[index] = instance;
        }

        public void ClearCached(TypeId typeId)
        {
            int index = typeId.Id;
            if (index >= 0 && index < cachedCache.Length)
                cachedCache[index] = null;
        }

        public void ClearAllCached()
        {
            Array.Clear(cachedCache, 0, cachedCache.Length);
        }

        public void ClearSceneSingletons()
        {
            Array.Clear(sceneSingletonCache, 0, sceneSingletonCache.Length);
        }

        public void ClearAllSingletons()
        {
            Array.Clear(singletonCache, 0, singletonCache.Length);
        }
    }
}
