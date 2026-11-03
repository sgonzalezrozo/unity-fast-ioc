using System;
using System.Collections.Generic;

namespace FastIoC.API
{
    /// <summary>Base class for installers. Bindings are read in the Editor during code generation, not at runtime.</summary>
    public abstract class BindingRecorder : IBindingRecorder
    {
        protected readonly Dictionary<Type, BindingInfo> bindings = new();

        /// <summary>Declares this installer's bindings. Runs in the Editor during code generation.</summary>
        public virtual void InstallBindings()
        {
        }

        /// <summary>Records a binding. Within one installer, the first binding for a type wins.</summary>
        public void RecordBinding(Type interfaceType, object builder)
        {
            if (bindings.ContainsKey(interfaceType))
                return;

            if (builder is not IBindingDescriptor descriptor)
                throw new ArgumentException($"[FastIoC] {builder?.GetType().Name} is not a binding builder.", nameof(builder));

            BindingInfo info = new(interfaceType, descriptor.ImplementationType ?? interfaceType, descriptor.BindingLifetime.ToString());
            info.InstanceValue = descriptor.InstanceValue;
            info.IsInstance = descriptor.IsInstance;
            info.AssetPath = descriptor.AssetPath;
            info.IsPathBinding = descriptor.IsPathBinding;
            bindings[interfaceType] = info;
        }

        /// <summary>Starts a binding for a type.</summary>
        public virtual BindingBuilder<T> Bind<T>() where T : class
        {
            return new BindingBuilder<T>(this);
        }

        /// <summary>Gets the recorded bindings for code generation.</summary>
        public Dictionary<Type, BindingInfo> GetBindingsForGeneration()
        {
            return bindings;
        }
    }
}
