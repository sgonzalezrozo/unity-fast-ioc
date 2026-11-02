using System;

namespace FastIoC.API.Injection
{
    public interface ITypeResolver
    {
        Type ResolveInjectableMonoBehaviourRegistry();
        Type ResolveGeneratedMonoInjectors();
        Type[] GetInjectableTypes();
        bool IsInjectableType(Type type);
    }
}

