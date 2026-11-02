using System;
using System.Collections.Generic;

namespace Tools.FastIoC.Runtime.Abstraction
{
    public interface ITypeMetadataProvider
    {
        IEnumerable<Type> GetTypes(Func<Type, bool> predicate);

        Type FindType(string fullName);

        IEnumerable<(Type Type, T Attribute)> GetTypesWithAttribute<T>(bool inherit = false)
            where T : Attribute;
    }
}

