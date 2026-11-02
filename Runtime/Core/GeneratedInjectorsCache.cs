using System;
using System.Reflection;
using UnityEngine;

namespace Tools.FastIoC.Runtime.Core
{
    public static class GeneratedInjectorsCache
    {
        private const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;
        private static Type cachedGeneratedInjectorsType;
        private static MethodInfo cachedInjectMethod;
        private static bool isInitialized;

        public static void Initialize()
        {
            if (isInitialized)
                return;

            cachedGeneratedInjectorsType = ReflectionUtility.ResolveType(FastIoCConstants.GENERATED_MONO_INJECTORS_TYPE);

            if (cachedGeneratedInjectorsType != null)
            {
                cachedInjectMethod = ReflectionUtility.GetPublicStaticMethod(
                    cachedGeneratedInjectorsType,
                    FastIoCConstants.METHOD_INJECT,
                    new[] { typeof(object), typeof(ContextRuntime) });

                if (cachedInjectMethod == null)
                {
                    cachedInjectMethod = cachedGeneratedInjectorsType.GetMethod(
                        FastIoCConstants.METHOD_INJECT,
                        PUBLIC_STATIC);
                }
            }
            else
            {
                Debug.LogWarning("[FastIoC] GeneratedMonoInjectors type not found during cache initialization. Injection will fail.");
            }

            isInitialized = true;
        }

        public static MethodInfo GetInjectMethod()
        {
            if (!isInitialized)
                Initialize();

            return cachedInjectMethod;
        }

        public static bool IsReady => isInitialized && cachedInjectMethod != null;

        public static void Reset()
        {
            cachedGeneratedInjectorsType = null;
            cachedInjectMethod = null;
            isInitialized = false;
        }
    }
}

