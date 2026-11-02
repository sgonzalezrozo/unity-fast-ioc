using System;
using System.Reflection;
using UnityEngine;

namespace Tools.FastIoC.Runtime.Core
{
    public static class FastIoCReflectionCache
    {
        public static Type GeneratedRegistryType { get; private set; }
        public static Func<string, object> CreateSceneContextRuntime { get; private set; }
        public static Func<object> CreateProjectContextRuntime { get; private set; }
        public static Action EnsureInitialized { get; private set; }


        public static Action<object, object> InjectMonoBehaviour { get; private set; }

        public static Type[] InjectableMonoBehaviourTypes { get; private set; }
        public static Func<Type, bool> IsInjectableType { get; private set; }

        public static Func<int, object> GetInstanceFromRegistry { get; private set; }

        public static Action InitializeInstanceRegistry { get; private set; }

        public static bool IsInitialized { get; private set; }

        public static void Initialize()
        {
            if (IsInitialized)
                return;

            try
            {
                GeneratedRegistryType = LoadType("FastIoC.Generated.GeneratedRegistry");
                if (GeneratedRegistryType != null)
                    CacheGeneratedRegistryMethods();

                Type generatedInjectorsType = LoadType("FastIoC.Generated.GeneratedMonoInjectors");
                if (generatedInjectorsType != null)
                    CacheGeneratedInjectorMethods(generatedInjectorsType);

                Type generatedInjectableType = LoadType("FastIoC.Generated.GeneratedInjectableMonoBehaviours");
                if (generatedInjectableType != null)
                    CacheInjectableTypes(generatedInjectableType);

                Type generatedInstanceRegistryType = LoadType("FastIoC.Generated.GeneratedInstanceRegistry");
                if (generatedInstanceRegistryType != null)
                    CacheInstanceRegistryMethod(generatedInstanceRegistryType);

                Type instanceInitializerType = LoadType("FastIoC.Generated.InstanceRegistryInitializer");
                if (instanceInitializerType != null)
                    CacheInstanceInitializerMethod(instanceInitializerType);

                IsInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Failed to initialize reflection cache: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void CacheGeneratedRegistryMethods()
        {
            try
            {
                MethodInfo ensureInitMethod = GeneratedRegistryType.GetMethod("EnsureInitialized",
                    BindingFlags.Public | BindingFlags.Static);
                if (ensureInitMethod != null)
                {
                    EnsureInitialized = () => ensureInitMethod.Invoke(null, null);
                }

                MethodInfo createSceneMethod = GeneratedRegistryType.GetMethod("CreateSceneContextRuntime",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);
                if (createSceneMethod != null)
                {
                    CreateSceneContextRuntime = (sceneName) => createSceneMethod.Invoke(null, new object[] { sceneName });
                }

                MethodInfo createProjectMethod = GeneratedRegistryType.GetMethod("CreateProjectContextRuntime",
                    BindingFlags.Public | BindingFlags.Static);
                if (createProjectMethod != null)
                {
                    CreateProjectContextRuntime = () => createProjectMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error caching GeneratedRegistry methods: {ex.Message}");
            }
        }

        private static void CacheGeneratedInjectorMethods(Type generatedInjectorsType)
        {
            try
            {
                MethodInfo injectMethod = generatedInjectorsType.GetMethod("Inject", BindingFlags.Public | BindingFlags.Static);

                if (injectMethod != null)
                {
                    InjectMonoBehaviour = (instance, context) =>
                    {
                        try
                        {
                            injectMethod.Invoke(null, new[] { instance, context });
                        }
                        catch (TargetInvocationException ex)
                        {
                            Debug.LogError($"[FastIoC] ❌ Injection failed for {instance?.GetType().Name ?? "null"}: {ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[FastIoC] ❌ Injection failed for {instance?.GetType().Name ?? "null"}: {ex.Message}\n{ex.StackTrace}");
                            throw;
                        }
                    };
                }
                else
                {
                    Debug.LogError("[FastIoC] ❌ Inject method not found on GeneratedMonoInjectors");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error caching GeneratedMonoInjectors methods: {ex.Message}\n{ex.StackTrace}");
            }
        }


        private static void CacheInjectableTypes(Type generatedInjectableType)
        {
            try
            {
                FieldInfo typesField = generatedInjectableType.GetField("Types",
                    BindingFlags.Public | BindingFlags.Static);
                if (typesField != null)
                {
                    InjectableMonoBehaviourTypes = (Type[])typesField.GetValue(null);
                }

                MethodInfo isInjectableMethod = generatedInjectableType.GetMethod("IsInjectableType",
                    BindingFlags.Public | BindingFlags.Static);
                if (isInjectableMethod != null)
                {
                    IsInjectableType = (type) => (bool)isInjectableMethod.Invoke(null, new object[] { type });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error caching GeneratedInjectableMonoBehaviours: {ex.Message}");
            }
        }

        private static void CacheInstanceRegistryMethod(Type generatedInstanceRegistryType)
        {
            try
            {
                MethodInfo getInstanceMethod = generatedInstanceRegistryType.GetMethod("GetInstance",
                    BindingFlags.Public | BindingFlags.Static);
                if (getInstanceMethod != null)
                {
                    GetInstanceFromRegistry = (typeId) => getInstanceMethod.Invoke(null, new object[] { typeId });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error caching GeneratedInstanceRegistry: {ex.Message}");
            }
        }

        private static void CacheInstanceInitializerMethod(Type instanceInitializerType)
        {
            try
            {
                MethodInfo initMethod = instanceInitializerType.GetMethod("InitializeInstances", BindingFlags.Public | BindingFlags.Static);
                if (initMethod != null)
                    InitializeInstanceRegistry = () => initMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error caching InstanceRegistryInitializer: {ex.Message}");
            }
        }

        private static Type LoadType(string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            type = Type.GetType($"{typeName}, Assembly-CSharp");
            if (type != null)
                return type;

            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(typeName);
                    if (type != null)
                        return type;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Error scanning assemblies for {typeName}: {ex.Message}");
            }

            Debug.LogWarning($"[FastIoC] Failed to load type: {typeName}");
            return null;
        }

        public static void Clear()
        {
            GeneratedRegistryType = null;
            CreateSceneContextRuntime = null;
            CreateProjectContextRuntime = null;
            EnsureInitialized = null;
            InjectMonoBehaviour = null;
            InjectableMonoBehaviourTypes = null;
            IsInjectableType = null;
            GetInstanceFromRegistry = null;
            InitializeInstanceRegistry = null;
            IsInitialized = false;
        }
    }
}
