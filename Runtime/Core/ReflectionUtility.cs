using System;
using System.Reflection;

namespace Tools.FastIoC.Runtime.Core
{
    public static class ReflectionUtility
    {
        private const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags PUBLIC_INSTANCE = BindingFlags.Public | BindingFlags.Instance;

        public static Type ResolveType(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return null;

            Type type = Type.GetType(fullTypeName);
            if (type != null)
                return type;

            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(fullTypeName);
                    if (type != null)
                        return type;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public static MethodInfo GetPublicStaticMethod(Type type, string methodName, Type[] parameterTypes = null)
        {
            if (type == null)
                return null;

            try
            {
                if (parameterTypes == null)
                {
                    return type.GetMethod(methodName, PUBLIC_STATIC);
                }

                MethodInfo method = type.GetMethod(methodName, PUBLIC_STATIC, null, parameterTypes, null);
                if (method != null)
                    return method;

                return type.GetMethod(methodName, PUBLIC_STATIC);
            }
            catch
            {
                return null;
            }
        }

        public static MethodInfo GetPublicInstanceMethod(Type type, string methodName, Type[] parameterTypes = null)
        {
            if (type == null)
                return null;

            try
            {
                if (parameterTypes == null)
                {
                    return type.GetMethod(methodName, PUBLIC_INSTANCE);
                }

                MethodInfo method = type.GetMethod(methodName, PUBLIC_INSTANCE, null, parameterTypes, null);
                if (method != null)
                    return method;

                return type.GetMethod(methodName, PUBLIC_INSTANCE);
            }
            catch
            {
                return null;
            }
        }

        public static MethodInfo GetGenericMethod(Type type, string methodName, int genericArgCount, Type[] parameterTypes = null)
        {
            if (type == null)
                return null;

            MethodInfo[] methods = type.GetMethods(PUBLIC_INSTANCE);
            foreach (MethodInfo method in methods)
            {
                if (method.Name != methodName)
                    continue;

                if (!method.IsGenericMethodDefinition)
                    continue;

                if (method.GetGenericArguments().Length != genericArgCount)
                    continue;

                if (parameterTypes != null)
                {
                    ParameterInfo[] pars = method.GetParameters();
                    if (pars.Length != parameterTypes.Length)
                        continue;
                }

                return method;
            }

            return null;
        }

        public static PropertyInfo GetPublicStaticProperty(Type type, string propertyName)
        {
            if (type == null)
                return null;

            return type.GetProperty(propertyName, PUBLIC_STATIC);
        }

        public static PropertyInfo GetPublicInstanceProperty(Type type, string propertyName)
        {
            if (type == null)
                return null;

            return type.GetProperty(propertyName, PUBLIC_INSTANCE);
        }

        public static object InvokeStaticMethod(MethodInfo method, object[] parameters, string context)
        {
            if (method == null)
                return null;

            try
            {
                return method.Invoke(null, parameters);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static object InvokeInstanceMethod(MethodInfo method, object instance, object[] parameters, string context)
        {
            if (method == null || instance == null)
                return null;

            try
            {
                return method.Invoke(instance, parameters);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static object GetStaticPropertyValue(PropertyInfo property, string context)
        {
            if (property == null)
                return null;

            try
            {
                return property.GetValue(null);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

