using System;

namespace FastIoC.Core.Abstraction
{
    /// <summary>
    /// Platform-agnostic interface for observing context (scene/scope) lifecycle.
    ///
    /// Implementations:
    /// - Unity: Listens to SceneManager.sceneLoaded/sceneUnloaded
    /// - CLI: Listens to folder changes
    /// - IDE: Listens to project structure changes
    ///
    /// </summary>
    public interface IContextLifecycleObserver
    {
        /// <summary>
        /// Fired when a new context scope is created.
        /// scopeId = scene name (Unity), folder name (CLI), project area (IDE), etc.
        /// </summary>
        event Action<string> OnContextCreated;

        /// <summary>
        /// Fired when a context scope is destroyed/unloaded.
        /// </summary>
        event Action<string> OnContextDestroyed;

        /// <summary>
        /// Start observing for context changes.
        /// </summary>
        void StartObserving();

        /// <summary>
        /// Stop observing for context changes.
        /// </summary>
        void StopObserving();
    }
}

