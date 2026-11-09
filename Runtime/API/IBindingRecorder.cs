using System;
using System.Collections.Generic;

namespace FastIoC.API
{
    public interface IBindingRecorder
    {
        void RecordBinding(Type interfaceType, object builder);
        void MarkNonLazy(Type interfaceType);
        Dictionary<Type, BindingInfo> GetBindingsForGeneration();
    }
}

