using System;

namespace Tools.FastIoC.Runtime.API.Injection
{
    public interface ITypeResolver
    {
        Type ResolveInjectableMonoBehaviourRegistry();
        Type ResolveGeneratedMonoInjectors();
        Type[] GetInjectableTypes();
        bool IsInjectableType(Type type);
    }
}

