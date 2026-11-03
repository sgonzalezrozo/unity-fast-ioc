using System;
using FastIoC.Core;

namespace FastIoC.API
{
    /// <summary>Fluent API for declaring how a type is provided. Finish a binding with a lifetime method.</summary>
    public class BindingBuilder<T> : IBindingDescriptor where T : class
    {
        private readonly IBindingRecorder installer;
        internal Type ImplementationType { get; private set; } = typeof(T);
        internal Lifetime BindingLifetime { get; private set; } = Lifetime.Transient;
        internal T InstanceValue { get; private set; }
        internal bool IsInstance { get; private set; }
        internal string AssetPath { get; private set; }
        internal bool IsPathBinding { get; private set; }

        Type IBindingDescriptor.ImplementationType => ImplementationType;
        Lifetime IBindingDescriptor.BindingLifetime => BindingLifetime;
        object IBindingDescriptor.InstanceValue => InstanceValue;
        bool IBindingDescriptor.IsInstance => IsInstance;
        string IBindingDescriptor.AssetPath => AssetPath;
        bool IBindingDescriptor.IsPathBinding => IsPathBinding;

        /// <summary>Creates a builder that records its binding on the given installer.</summary>
        public BindingBuilder(IBindingRecorder installer)
        {
            this.installer = installer ?? throw new ArgumentNullException(nameof(installer));
        }

        /// <summary>Sets the concrete type to create for this binding.</summary>
        public BindingBuilder<T> To<TImpl>() where TImpl : class, T
        {
            ImplementationType = typeof(TImpl);
            return this;
        }

        /// <summary>Binds an existing object. The installer also runs once at startup in the game to create this value, so build it from code that works outside the Editor.</summary>
        public void FromInstance(T instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            InstanceValue = instance;
            IsInstance = true;
            IsPathBinding = false;
            ImplementationType = instance.GetType();
            BindingLifetime = Lifetime.Singleton;
            installer.RecordBinding(typeof(T), this);
        }

        /// <summary>Binds a prefab or ScriptableObject by asset name or full path. Finish with AsSingle(). The asset must be in Resources or marked as Addressable.</summary>
        public BindingBuilder<T> FromInstance(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentNullException(nameof(assetPath), "Asset path cannot be null or empty");

            AssetPath = assetPath;
            IsInstance = true;
            IsPathBinding = true;
            BindingLifetime = Lifetime.Singleton;
            return this;
        }

        /// <summary>Gets the concrete type for this binding.</summary>
        public Type GetImplementationType() => ImplementationType;

        /// <summary>Records one shared instance, created on first resolve.</summary>
        public void AsSingle()
        {
            BindingLifetime = Lifetime.Singleton;
            installer.RecordBinding(typeof(T), this);
        }

        /// <summary>Records one cached instance, cleared when a scene unloads.</summary>
        public void AsCached()
        {
            BindingLifetime = Lifetime.Cached;
            installer.RecordBinding(typeof(T), this);
        }

        /// <summary>Records a new instance for each resolve.</summary>
        public void AsTransient()
        {
            BindingLifetime = Lifetime.Transient;
            installer.RecordBinding(typeof(T), this);
        }
    }
}
