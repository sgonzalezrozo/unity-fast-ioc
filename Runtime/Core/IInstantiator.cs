namespace FastIoC.Core
{
    /// <summary>
    /// Marks a type as instantiable via the injection factory.
    /// Used with IInstantiator.Instantiate.
    /// </summary>
    public interface IInstanciable
    {
    }

    /// <summary>
    /// Marks a MonoBehaviour as available for injection into its children.
    /// The component will be registered in the instantiation scope and can be resolved by child components.
    /// Also marks the component as instantiable via FastIoC factory.
    /// Use this when a parent component should be available to children but doesn't carry initialization data.
    /// </summary>
    /// <remarks>
    /// Example: A parent controller that manages child UI elements can implement this interface
    /// so children can have it injected in their constructors.
    /// Inherits from IInstanciable to indicate this should be instantiated via IInstantiator.
    /// </remarks>
    public interface IInjectableForChildren : IInstanciable
    {
    }

    /// <summary>
    /// Marks a MonoBehaviour as injectable for its children with data.
    /// The component will be injected into child MonoBehaviours AND initialization data will be set.
    /// Also marks the component as instantiable via FastIoC factory.
    /// Implementing type must have a Data property.
    /// </summary>
    /// <typeparam name="TData">The data type passed during instantiation</typeparam>
    /// <remarks>
    /// Example: A parent data provider that passes configuration to child components.
    /// Use IInjectableForChildren (non-generic) if you don't need to pass data.
    /// Inherits from IInstanciable to indicate this should be instantiated via IInstantiator.
    /// </remarks>
    public interface IInjectableForChildren<TData> : IInstanciable
    {
        /// <summary>
        /// Data set during instantiation by the FastIoC factory.
        /// Called before child component injection, so data is available in their constructors.
        /// </summary>
        TData Data { get; set; }
    }

    /// <summary>
    /// Factory for instantiating prefabs with full dependency injection.
    /// Supports injecting the root component and all children.
    /// </summary>
    public interface IInstantiator
    {
        /// <summary>
        /// Instantiate a prefab with dependency injection using a GameObject reference.
        /// Injects dependencies into the component and all children.
        /// </summary>
        /// <typeparam name="T">The component type to instantiate (must implement IInstanciable)</typeparam>
        /// <param name="prefab">The prefab GameObject to instantiate</param>
        /// <param name="parent">Parent transform (optional)</param>
        /// <param name="data">Data to pass to IInjectableForChildren (optional)</param>
        /// <returns>The instantiated component with all dependencies injected</returns>
        T Instantiate<T>(UnityEngine.GameObject prefab, UnityEngine.Transform parent = null, object data = null) where T : UnityEngine.MonoBehaviour, IInstanciable;

        /// <summary>
        /// Instantiate a prefab with dependency injection using a component reference.
        /// More ergonomic than passing GameObject - you can serialize the component directly.
        /// Injects dependencies into the component and all children.
        /// </summary>
        /// <typeparam name="T">The component type to instantiate (must implement IInstanciable)</typeparam>
        /// <param name="prefabComponent">The prefab component to instantiate (will use its GameObject)</param>
        /// <param name="parent">Parent transform (optional)</param>
        /// <param name="data">Data to pass to IInjectableForChildren (optional)</param>
        /// <returns>The instantiated component with all dependencies injected</returns>
        T Instantiate<T>(T prefabComponent, UnityEngine.Transform parent = null, object data = null) where T : UnityEngine.MonoBehaviour, IInstanciable;
    }
}

