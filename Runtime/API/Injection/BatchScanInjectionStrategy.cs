using System.Collections.Generic;
using Tools.FastIoC.Runtime.Core;
using UnityEngine;

namespace Tools.FastIoC.Runtime.API.Injection
{
    public class BatchScanInjectionStrategy : IInjectionStrategy
    {
        public string StrategyName => "Batch Scan";

        public bool CanExecute()
            => FastIoCReflectionCache.InjectMonoBehaviour != null;

        public void Inject(System.Type[] injectableTypes, ContextRuntime context)
        {
            #if UNITY_2023_1_OR_NEWER
            MonoBehaviour[] allMonoBehaviours = MonoBehaviour.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            #else
            MonoBehaviour[] allMonoBehaviours = MonoBehaviour.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
            #endif

            // Step 2: Group MonoBehaviours by type for fast lookup
            Dictionary<System.Type, List<MonoBehaviour>> typeGroups = new();
            foreach (MonoBehaviour mb in allMonoBehaviours)
            {
                System.Type mbType = mb.GetType();

                if (System.Array.Exists(injectableTypes, t => t == mbType))
                {
                    if (!typeGroups.ContainsKey(mbType))
                        typeGroups[mbType] = new();

                    typeGroups[mbType].Add(mb);
                }
            }

            foreach (System.Type injectableType in injectableTypes)
            {
                if (injectableType.Name == "ProjectContext")
                    continue;

                if (!typeGroups.TryGetValue(injectableType, out var instances))
                    continue;

                foreach (MonoBehaviour instance in instances)
                {
                    try
                    {
                        FastIoCReflectionCache.InjectMonoBehaviour(instance, context);
                    }
                    catch (System.Exception ex)
                    {
                        if (ex.Message.Contains("not registered") || ex.InnerException?.Message.Contains("not registered") == true)
                            continue;

                        Debug.LogError($"[FastIoC] Error injecting {instance.name} ({injectableType.Name}): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }
    }
}

