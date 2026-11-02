using System.Reflection;

namespace FastIoC.API.Injection
{
    public class CachedTypeResolver : ITypeResolver
    {
        private System.Type cachedInjectableMonoBehaviourRegistry;
        private System.Type cachedGeneratedMonoInjectors;
        private System.Type[] cachedInjectableTypes;

        public System.Type ResolveInjectableMonoBehaviourRegistry()
        {
            if (cachedInjectableMonoBehaviourRegistry != null)
                return cachedInjectableMonoBehaviourRegistry;

            if (FastIoCReflectionCache.IsInitialized)
                return cachedInjectableMonoBehaviourRegistry;

            cachedInjectableMonoBehaviourRegistry = SearchTypeByName("FastIoCGenerated.GeneratedInjectableMonoBehaviours");
            return cachedInjectableMonoBehaviourRegistry;
        }

        public System.Type ResolveGeneratedMonoInjectors()
        {
            if (cachedGeneratedMonoInjectors != null)
                return cachedGeneratedMonoInjectors;

            cachedGeneratedMonoInjectors = SearchTypeByName("FastIoCGenerated.GeneratedMonoInjectors");
            return cachedGeneratedMonoInjectors;
        }

        public System.Type[] GetInjectableTypes()
        {
            if (cachedInjectableTypes != null)
                return cachedInjectableTypes;

            if (FastIoCReflectionCache.IsInitialized &&
                FastIoCReflectionCache.InjectableMonoBehaviourTypes != null)
            {
                cachedInjectableTypes = FastIoCReflectionCache.InjectableMonoBehaviourTypes;
                return cachedInjectableTypes;
            }

            System.Type registryType = ResolveInjectableMonoBehaviourRegistry();
            if (registryType == null)
                return null;

            FieldInfo typesField = registryType.GetField("Types", BindingFlags.Public | BindingFlags.Static);

            if (typesField == null)
                return null;

            cachedInjectableTypes = typesField.GetValue(null) as System.Type[];
            return cachedInjectableTypes;
        }

        public bool IsInjectableType(System.Type type)
        {
            if (type == null)
                return false;

            if (FastIoCReflectionCache.IsInitialized && FastIoCReflectionCache.IsInjectableType != null)
                return FastIoCReflectionCache.IsInjectableType(type);

            System.Type registryType = ResolveInjectableMonoBehaviourRegistry();
            if (registryType == null)
                return false;

            MethodInfo isInjectableMethod = registryType.GetMethod("IsInjectableType", BindingFlags.Public | BindingFlags.Static);

            if (isInjectableMethod == null)
                return false;

            try
            {
                return (bool)isInjectableMethod.Invoke(null, new object[] { type });
            }
            catch
            {
                return false;
            }
        }

        private System.Type SearchTypeByName(string fullTypeName)
        {
            System.Type type = System.Type.GetType(fullTypeName);
            if (type != null)
                return type;

            type = System.Type.GetType($"{fullTypeName}, Assembly-CSharp");
            if (type != null)
                return type;

            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullTypeName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}

