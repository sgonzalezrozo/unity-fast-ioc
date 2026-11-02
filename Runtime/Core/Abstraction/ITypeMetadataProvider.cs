using System;
using System.Collections.Generic;

namespace Tools.FastIoC.Runtime.Core.Abstraction
{
    /// <summary>
    /// Platform-agnostic interface for discovering and resolving types.
    /// Implementations: Unity AppDomain, CLI FileSystem, Roslyn source, etc.
    ///
    /// This interface abstracts away the differences between:
    /// - Unity Editor (AppDomain.CurrentDomain)
    /// - Standalone .NET app (Assembly.LoadFrom)
    /// - IDE integration (Roslyn source analysis)
    /// </summary>
    public interface ITypeMetadataProvider
    {
        /// <summary>
        /// Get all types matching a predicate.
        /// Used to filter types by criteria (e.g., "is MonoBehaviour", "has [Inject]", etc.)
        /// </summary>
        IEnumerable<Type> GetTypes(Func<Type, bool> predicate);

        /// <summary>
        /// Find a specific type by fully-qualified name.
        /// Returns null if not found.
        /// Used during type resolution and validation.
        /// </summary>
        Type FindType(string fullName);

        /// <summary>
        /// Get all types decorated with a custom attribute.
        /// Used to discover installers ([MonoInstaller]), etc.
        /// </summary>
        IEnumerable<(Type Type, T Attribute)> GetTypesWithAttribute<T>(bool inherit = false)
            where T : Attribute;
    }
}

