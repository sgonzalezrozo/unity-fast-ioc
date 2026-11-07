using System;
using UnityEngine;

namespace FastIoC.Core
{
    /// <summary>
    /// Baked at build time (and when a scene loads in Play Mode in the editor) by the FastIoC scene baker.
    /// Holds serialized references to every injectable MonoBehaviour in the scene and the index of
    /// its generated injector, so scene injection needs no FindObjectsByType and no type lookups.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class SceneInjectionManifest : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour[] targets = Array.Empty<MonoBehaviour>();
        [SerializeField] private int[] injectorIds = Array.Empty<int>();

        public int Count => targets?.Length ?? 0;

        /// <summary>Called by the editor baker.</summary>
        public void SetEntries(MonoBehaviour[] bakedTargets, int[] bakedInjectorIds)
        {
            if (bakedTargets == null || bakedInjectorIds == null || bakedTargets.Length != bakedInjectorIds.Length)
                throw new ArgumentException("Targets and injector ids must have the same length.");

            targets = bakedTargets;
            injectorIds = bakedInjectorIds;
        }

        public void InjectAll(ContextRuntime context) => InjectAll(context, null, null);

        /// <summary>
        /// Injects every target except those in <paramref name="skip"/> (instance IDs).
        /// Each injected target's instance ID is added to <paramref name="injected"/> when it isn't null.
        /// </summary>
        public void InjectAll(ContextRuntime context, System.Collections.Generic.HashSet<int> skip, System.Collections.Generic.HashSet<int> injected)
        {
            if (targets == null || injectorIds == null)
                return;

            int count = Math.Min(targets.Length, injectorIds.Length);
            for (int i = 0; i < count; i++)
            {
                MonoBehaviour target = targets[i];
                if (target == null)
                    continue;

                int id = target.GetInstanceID();
                if (skip != null && skip.Contains(id))
                    continue;

                InjectionRunner.InjectAt(injectorIds[i], target, context);
                injected?.Add(id);
            }
        }
    }
}
