using System;
using FastIoC.Core;

namespace FastIoC.API
{
    public class BindingBuilder<T> where T : class
    {
        private readonly IBindingRecorder installer;
        internal Type ImplementationType { get; private set; } = typeof(T);
        internal Lifetime BindingLifetime { get; private set; } = Lifetime.Transient;
        internal T InstanceValue { get; private set; }
        internal bool IsInstance { get; private set; }
        internal string AssetPath { get; private set; }
        internal bool IsPathBinding { get; private set; }

        public BindingBuilder(IBindingRecorder installer)
        {
            this.installer = installer ?? throw new ArgumentNullException(nameof(installer));
        }

        public BindingBuilder<T> To<TImpl>() where TImpl : class, T
        {
            ImplementationType = typeof(TImpl);
            return this;
        }

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

        public Type GetImplementationType() => ImplementationType;

        public void AsSingle()
        {
            BindingLifetime = Lifetime.Singleton;
            installer.RecordBinding(typeof(T), this);
        }

        public void AsCached()
        {
            BindingLifetime = Lifetime.Cached;
            installer.RecordBinding(typeof(T), this);
        }

        public void AsTransient()
        {
            BindingLifetime = Lifetime.Transient;
            installer.RecordBinding(typeof(T), this);
        }
    }
}
