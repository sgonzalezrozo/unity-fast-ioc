using System;

namespace FastIoC.API
{
    public class BindingInfo
    {
        public Type InterfaceType { get; internal set; }
        public Type ImplementationType { get; internal set; }
        public string LifetimeName { get; internal set; }
        public object InstanceValue { get; internal set; }
        public bool IsInstance { get; internal set; }
        public string AssetPath { get; internal set; }
        public bool IsPathBinding { get; internal set; }
        public bool IsNonLazy { get; internal set; }
        public int DeclarationOrder { get; internal set; }
        /// <summary>True when this binding is provided by a component that lives on ProjectContext.HostedRoot.</summary>
        public bool IsProjectContextBinding { get; internal set; }

        public BindingInfo(Type interfaceType, Type implementationType, string lifetimeName)
        {
            InterfaceType = interfaceType ?? throw new ArgumentNullException(nameof(interfaceType));
            ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
            LifetimeName = lifetimeName ?? "Transient";
            InstanceValue = null;
            IsInstance = false;
            AssetPath = null;
            IsPathBinding = false;
            IsNonLazy = false;
            DeclarationOrder = 0;
        }
    }
}

