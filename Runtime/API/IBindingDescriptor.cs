using System;
using FastIoC.Core;

namespace FastIoC.API
{
    /// <summary>Exposes what a BindingBuilder recorded, without using reflection.</summary>
    internal interface IBindingDescriptor
    {
        Type ImplementationType { get; }
        Lifetime BindingLifetime { get; }
        object InstanceValue { get; }
        bool IsInstance { get; }
        string AssetPath { get; }
        bool IsPathBinding { get; }
        bool IsNonLazy { get; }
        bool IsProjectContextBinding { get; }
    }
}
