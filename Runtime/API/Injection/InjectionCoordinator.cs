using System;
using System.Collections.Generic;
using System.Reflection;
using Tools.FastIoC.Runtime.Core;
using UnityEngine;

namespace Tools.FastIoC.Runtime.API.Injection
{
    public class InjectionCoordinator
    {
        private readonly ITypeResolver typeResolver;
        private readonly List<IInjectionStrategy> strategies;

        public InjectionCoordinator(ITypeResolver typeResolver)
        {
            this.typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));

            strategies = new List<IInjectionStrategy>
            {
                new BatchScanInjectionStrategy(),
                new ReflectionInjectionStrategy(typeResolver),
            };
        }

        public void InjectScene(ContextRuntime context)
        {
            Type[] injectableTypes = typeResolver.GetInjectableTypes();
            if (injectableTypes == null || injectableTypes.Length == 0)
            {
                Debug.LogWarning("[FastIoC] No injectable MonoBehaviour types found in registry.");
                return;
            }

            foreach (IInjectionStrategy strategy in strategies)
            {
                if (!strategy.CanExecute())
                    continue;

                strategy.Inject(injectableTypes, context);
                return;
            }

            Debug.LogError("[FastIoC] No viable injection strategy available.");
        }

        public void InjectInstance(MonoBehaviour instance, ContextRuntime context)
        {
            if (instance == null)
            {
                Debug.LogError("[FastIoC] InjectInstance: Instance is null.");
                return;
            }

            if (!typeResolver.IsInjectableType(instance.GetType()))
                return;

            if (FastIoCReflectionCache.InjectMonoBehaviour != null)
            {
                try
                {
                    FastIoCReflectionCache.InjectMonoBehaviour(instance, context);
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("not registered") || ex.InnerException?.Message.Contains("not registered") == true)
                        return;

                    Debug.LogError($"[FastIoC] Error injecting {instance.name}: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("[FastIoC] Delegate not cached, using reflection to inject");
                InjectWithReflection(instance, context);
            }
        }

        private void InjectWithReflection(MonoBehaviour instance, ContextRuntime context)
        {
            System.Type generatedInjectorsType = typeResolver.ResolveGeneratedMonoInjectors();
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

            try
            {
                injectMethod.Invoke(null, new object[] { instance, context });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error injecting {instance.name}: {ex.Message}");
            }
        }
    }
}

