using System;
using UnityEngine;

namespace Tools.FastIoC.Runtime.Core
{
    public static class InjectableDataHandler
    {
        public static bool TrySetInjectableData(object obj, object data)
        {
            if (obj == null || data == null)
                return false;

            Type objType = obj.GetType();
            foreach (Type interfaceType in objType.GetInterfaces())
            {
                if (!interfaceType.IsGenericType)
                    continue;

                if (interfaceType.GetGenericTypeDefinition() != typeof(IInjectableForChildren<>))
                    continue;

                Type dataType = interfaceType.GetGenericArguments()[0];

                if (!dataType.IsInstanceOfType(data))
                    continue;

                System.Reflection.MethodInfo injectMethod = interfaceType.GetMethod("Inject");
                if (injectMethod == null)
                    continue;

                try
                {
                    injectMethod.Invoke(obj, new object[] { data });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FastIoC] Runtime data injection failed for {objType.Name} with data type {dataType.Name}: {ex.InnerException?.Message ?? ex.Message}");
                    return false;
                }
            }

            return false;
        }
    }
}

