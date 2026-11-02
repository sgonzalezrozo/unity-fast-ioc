using System;
using System.Collections.Generic;
using System.Reflection;

namespace FastIoC.API
{
    public abstract class BindingRecorder : IBindingRecorder
    {
        protected readonly Dictionary<Type, BindingInfo> bindings = new();

        public virtual void InstallBindings()
        {
        }

        public void RecordBinding(Type interfaceType, object builder)
        {
            if (bindings.ContainsKey(interfaceType))
                return;

            Type implType = interfaceType;
            string lifetimeName = "Transient";
            object instanceValue = null;
            bool isInstance = false;
            string assetPath = null;
            bool isPathBinding = false;

            try
            {
                Type builderType = builder.GetType();
                PropertyInfo implProp = builderType.GetProperty("ImplementationType", BindingFlags.Instance | BindingFlags.NonPublic);
                if (implProp != null)
                    implType = implProp.GetValue(builder) as Type ?? interfaceType;

                PropertyInfo lifetimeProp = builderType.GetProperty("BindingLifetime", BindingFlags.Instance | BindingFlags.NonPublic);
                if (lifetimeProp != null)
                {
                    object lifeTimeObj = lifetimeProp.GetValue(builder);
                    lifetimeName = lifeTimeObj?.ToString() ?? "Transient";
                }

                PropertyInfo instanceProp = builderType.GetProperty("InstanceValue", BindingFlags.Instance | BindingFlags.NonPublic);
                PropertyInfo isInstanceProp = builderType.GetProperty("IsInstance", BindingFlags.Instance | BindingFlags.NonPublic);

                if (instanceProp != null && isInstanceProp != null)
                {
                    instanceValue = instanceProp.GetValue(builder);
                    isInstance = (bool)(isInstanceProp.GetValue(builder) ?? false);
                }

                PropertyInfo assetPathProp = builderType.GetProperty("AssetPath", BindingFlags.Instance | BindingFlags.NonPublic);
                PropertyInfo isPathBindingProp = builderType.GetProperty("IsPathBinding", BindingFlags.Instance | BindingFlags.NonPublic);

                if (assetPathProp != null && isPathBindingProp != null)
                {
                    assetPath = assetPathProp.GetValue(builder) as string;
                    isPathBinding = (bool)(isPathBindingProp.GetValue(builder) ?? false);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[FastIoC] RecordBinding extraction failed for {interfaceType.Name}: {ex.Message}");
            }

// #pragma warning disable CS0168
//             catch (Exception ex)
// #pragma warning restore CS0168
//             {
//                 // Silently ignore reflection errors when FAST_IoC is not defined
//             }


            BindingInfo info = new(interfaceType, implType, lifetimeName);
            info.InstanceValue = instanceValue;
            info.IsInstance = isInstance;
            info.AssetPath = assetPath;
            info.IsPathBinding = isPathBinding;
            bindings[interfaceType] = info;
        }

        public virtual BindingBuilder<T> Bind<T>() where T : class
        {
            return new BindingBuilder<T>(this);
        }

        public Dictionary<Type, BindingInfo> GetBindingsForGeneration()
        {
            return bindings;
        }
    }
}
