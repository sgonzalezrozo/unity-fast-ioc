using System;
using UnityEngine;

namespace FastIoC.Core
{
    /// <summary>
    /// Runs the generated injectors. Every call is a direct call into generated code: no reflection.
    /// </summary>
    public static class InjectionRunner
    {
        /// <summary>
        /// Injects <paramref name="target"/> if its type has a generated injector.
        /// Returns false if there is no generated module or no injector for the type.
        /// </summary>
        public static bool TryInject(MonoBehaviour target, ContextRuntime context)
        {
            if (target == null || context == null)
                return false;

            IGeneratedModule module = GeneratedModuleRegistry.Module;
            if (module == null)
                return false;

            int injectorId = module.GetInjectorId(target.GetType());
            if (injectorId < 0)
                return false;

            InjectAt(module, injectorId, target, context);
            return true;
        }

        /// <summary>
        /// Injects <paramref name="target"/> with a known injector index (from a baked scene manifest).
        /// </summary>
        public static void InjectAt(int injectorId, MonoBehaviour target, ContextRuntime context)
        {
            IGeneratedModule module = GeneratedModuleRegistry.Module;
            if (module == null || target == null || injectorId < 0)
                return;

            InjectAt(module, injectorId, target, context);
        }

        /// <summary>
        /// Injects a freshly instantiated hierarchy:
        /// 1. IInjectableForChildren&lt;T&gt;.Data, 2. container fields, 3. local references.
        /// </summary>
        public static void InjectHierarchy(MonoBehaviour[] monoBehaviours, ContextRuntime context, object data, ILocalComponentSource localSource)
        {
            if (monoBehaviours == null)
                return;

            IGeneratedModule module = GeneratedModuleRegistry.Module;
            if (module == null)
            {
                Debug.LogWarning("[FastIoC] Generated code is not registered. Run 'Tools > FastIoC > Regenerate Code'. Injection skipped.");
                return;
            }

            if (data != null)
            {
                for (int i = 0; i < monoBehaviours.Length; i++)
                {
                    MonoBehaviour mb = monoBehaviours[i];
                    if (mb == null)
                        continue;

                    try
                    {
                        module.InjectChildrenData(mb, data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FastIoC] Data injection failed for {mb.GetType().Name}: {ex.Message}");
                    }
                }
            }

            if (context != null)
            {
                for (int i = 0; i < monoBehaviours.Length; i++)
                {
                    MonoBehaviour mb = monoBehaviours[i];
                    if (mb == null)
                        continue;

                    int injectorId = module.GetInjectorId(mb.GetType());
                    if (injectorId >= 0)
                        InjectAt(module, injectorId, mb, context);
                }
            }

            if (localSource != null)
            {
                for (int i = 0; i < monoBehaviours.Length; i++)
                {
                    MonoBehaviour mb = monoBehaviours[i];
                    if (mb == null || mb is InstantiationScope)
                        continue;

                    try
                    {
                        module.InjectLocal(mb, localSource);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[FastIoC] Error resolving local references for {mb.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        private static void InjectAt(IGeneratedModule module, int injectorId, MonoBehaviour target, ContextRuntime context)
        {
            try
            {
                module.InjectAt(injectorId, target, context);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not registered"))
            {
                // A scene-scoped binding that isn't available in the current scene: leave the field null.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Injection failed for {target.GetType().Name} ({target.name}): {ex.Message}\n{ex.StackTrace}", target);
            }
        }
    }
}
