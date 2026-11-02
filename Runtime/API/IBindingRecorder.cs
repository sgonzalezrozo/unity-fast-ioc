using System;
using System.Collections.Generic;

namespace Tools.FastIoC.Runtime.API
{
    public interface IBindingRecorder
    {
        void RecordBinding(Type interfaceType, object builder);
        Dictionary<Type, BindingInfo> GetBindingsForGeneration();
    }
}

