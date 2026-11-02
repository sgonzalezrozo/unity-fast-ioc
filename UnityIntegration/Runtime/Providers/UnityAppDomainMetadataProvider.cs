using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FastIoC.Core.Abstraction;
using UnityEngine;

namespace FastIoC.UnityIntegration.Runtime.Providers
{
    /// <summary>
    /// Discovers types from currently-loaded assemblies in Unity's AppDomain.
    /// Used during Unity Editor sessions.
    /// </summary>
    public class UnityAppDomainMetadataProvider : ITypeMetadataProvider
    {
        public IEnumerable<Type> GetTypes(Func<Type, bool> predicate)
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .Where(predicate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Error enumerating types: {ex.Message}");
                return Enumerable.Empty<Type>();
            }
        }

        public Type FindType(string fullName)
        {
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType(fullName, throwOnError: false);
                    if (type != null)
                        return type;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Error finding type '{fullName}': {ex.Message}");
                return null;
            }
        }

        public IEnumerable<(Type Type, T Attribute)> GetTypesWithAttribute<T>(bool inherit = false)
            where T : Attribute
        {
            List<(Type, T)> results = new();

            try
            {
                foreach (Type type in GetTypes(_ => true))
                {
                    T attr = type.GetCustomAttribute<T>(inherit);
                    if (attr != null)
                        results.Add((type, attr));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Error scanning for attribute {typeof(T).Name}: {ex.Message}");
            }

            return results;
        }

        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch
            {
                return Type.EmptyTypes;
            }
        }
    }
}

