using System;
using System.Reflection;
using Tools.FastIoC.Runtime.Core;
using UnityEngine;

namespace Tools.FastIoC.Runtime.API.Injection
{
    public class ReflectionInjectionStrategy : IInjectionStrategy
    {
        private readonly ITypeResolver typeResolver;

        public string StrategyName => "Reflection";

        public ReflectionInjectionStrategy(ITypeResolver typeResolver)
        {
            this.typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
        }

        public bool CanExecute()
        {
            return typeResolver.ResolveGeneratedMonoInjectors() != null;
        }

        public void Inject(System.Type[] injectableTypes, ContextRuntime context)
        {
            Type generatedInjectorsType = typeResolver.ResolveGeneratedMonoInjectors();
            if (generatedInjectorsType == null)
            {
                Debug.LogError("[FastIoC] GeneratedMonoInjectors not found");
                return;
            }

            MethodInfo injectMethod = generatedInjectorsType.GetMethod("Inject", BindingFlags.Public | BindingFlags.Static);

            if (injectMethod == null)
            {
                Debug.LogError("[FastIoC] GeneratedMonoInjectors.Inject method not found");
                return;
            }

            int totalInjectionsAttempted = 0;
            int totalInjectionsSucceeded = 0;

            foreach (Type injectableType in injectableTypes)
            {
                if (injectableType.Name == "ProjectContext")
                    continue;

#if UNITY_2023_1_OR_NEWER
                MonoBehaviour[] instances = MonoBehaviour.FindObjectsByType(injectableType, FindObjectsSortMode.None) as MonoBehaviour[];
#else
                MonoBehaviour[] instances = MonoBehaviour.FindObjectsOfType(injectableType, includeInactive: true) as MonoBehaviour[];
#endif

                foreach (MonoBehaviour instance in instances)
                {
                    totalInjectionsAttempted++;
                    try
                    {
                        injectMethod.Invoke(null, new object[] { instance, context });
                        totalInjectionsSucceeded++;
                        Debug.Log($"[FastIoC] Injected {instance.name} ({injectableType.Name})");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[FastIoC] Error injecting {instance.name} ({injectableType.Name}): {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            Debug.Log($"[FastIoC] Reflection injection complete: {totalInjectionsSucceeded}/{totalInjectionsAttempted} instances injected");
        }
    }
}

