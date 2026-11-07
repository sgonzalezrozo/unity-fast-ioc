using System;

namespace FastIoC.Core
{
    public class InstanceCacheArray
    {
        private readonly int length;
        private readonly object[] singletonCache;
        // Allocated the first time an instance with that lifetime is cached: most projects use singletons only.
        private object[] sceneSingletonCache;
        private object[] cachedCache;

        public InstanceCacheArray(int maxTypeId)
        {
            length = maxTypeId + 1;
            singletonCache = new object[length];
        }

        public bool TryGetCached(TypeId typeId, Lifetime lifetime, out object instance)
        {
            instance = null;

            int index = typeId.Id;
            if (index < 0 || index >= length)
                return false;

            object[] cache = lifetime switch
            {
                Lifetime.Singleton => singletonCache,
                Lifetime.SceneSingleton => sceneSingletonCache,
                Lifetime.Cached => cachedCache,
                _ => null
            };

            if (cache == null)
                return false;

            instance = cache[index];
            return instance != null;
        }

        public void SetCached(TypeId typeId, Lifetime lifetime, object instance)
        {
            int index = typeId.Id;
            if (index < 0 || index >= length)
                return;

            switch (lifetime)
            {
                case Lifetime.Singleton:
                    singletonCache[index] = instance;
                    break;
                case Lifetime.SceneSingleton:
                    (sceneSingletonCache ??= new object[length])[index] = instance;
                    break;
                case Lifetime.Cached:
                    (cachedCache ??= new object[length])[index] = instance;
                    break;
            }
        }

        public void ClearCached(TypeId typeId)
        {
            int index = typeId.Id;
            if (cachedCache != null && index >= 0 && index < length)
                cachedCache[index] = null;
        }

        public void ClearAllCached()
        {
            if (cachedCache != null)
                Array.Clear(cachedCache, 0, length);
        }

        public void ClearSceneSingletons()
        {
            if (sceneSingletonCache != null)
                Array.Clear(sceneSingletonCache, 0, length);
        }

        public void ClearAllSingletons()
        {
            Array.Clear(singletonCache, 0, length);
        }
    }
}
