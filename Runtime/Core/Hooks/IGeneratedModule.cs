using System;
using UnityEngine;

namespace FastIoC.Core
{
    /// <summary>
    /// Entry point to the code FastIoC generates at edit time.
    /// The generated module registers itself through <see cref="GeneratedModuleRegistry.Register"/>
    /// from a [RuntimeInitializeOnLoadMethod], so the runtime never searches for generated types.
    /// </summary>
    public interface IGeneratedModule
    {
        /// <summary>Runs the static initializer of the generated registry.</summary>
        void EnsureInitialized();

        /// <summary>Creates a runtime with the global bindings plus the bindings of the given scene.</summary>
        ContextRuntime CreateSceneContextRuntime(string sceneName);

        /// <summary>Creates a runtime with the global bindings only.</summary>
        ContextRuntime CreateProjectContextRuntime();

        /// <summary>Loads the instance bindings (FromInstance).</summary>
        void InitializeInstances();

        /// <summary>Returns a registered instance binding, or null.</summary>
        object GetInstance(int typeId);

        /// <summary>Returns the injector index for a MonoBehaviour type, or -1 if the type has no injector.</summary>
        int GetInjectorId(Type monoBehaviourType);

        /// <summary>Fills the container fields of <paramref name="target"/> with the injector at <paramref name="injectorId"/>.</summary>
        void InjectAt(int injectorId, MonoBehaviour target, ContextRuntime context);

        /// <summary>Sets IInjectableForChildren&lt;T&gt;.Data when <paramref name="data"/> matches T. Returns true if it was set.</summary>
        bool InjectChildrenData(MonoBehaviour target, object data);

        /// <summary>Fills null MonoBehaviour fields of <paramref name="target"/> from components in the same instantiated hierarchy.</summary>
        void InjectLocal(MonoBehaviour target, ILocalComponentSource source);
    }
}
