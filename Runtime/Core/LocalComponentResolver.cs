using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tools.FastIoC.Runtime.Core
{
    public static class LocalComponentResolver
    {
        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new();
        private static readonly Dictionary<Type, MethodInfo> resolveMethodCache = new();

        public static void InjectLocalReferencesWithScope(GameObject root, MonoBehaviour[] allMonoBehaviours)
        {
            InstantiationScope scope = root.GetComponent<InstantiationScope>();
            if (scope == null)
                return;

            foreach (MonoBehaviour monoBehaviour in allMonoBehaviours)
            {
                if (monoBehaviour == null || monoBehaviour is InstantiationScope)
                    continue;

                InjectFieldsFromScope(monoBehaviour, scope);
            }
        }

        public static void InjectLocalReferencesFromHierarchy(GameObject root, MonoBehaviour[] allMonoBehaviours)
        {
            foreach (MonoBehaviour monoBehaviour in allMonoBehaviours)
            {
                if (monoBehaviour == null)
                    continue;

                InjectFieldsFromHierarchy(monoBehaviour, root);
            }
        }

        private static void InjectFieldsFromScope(MonoBehaviour monoBehaviour, InstantiationScope scope)
        {
            Type mbType = monoBehaviour.GetType();
            FieldInfo[] injectableFields = GetCachedInjectableFields(mbType);

            for (int i = 0; i < injectableFields.Length; i++)
            {
                FieldInfo field = injectableFields[i];
                if (field.GetValue(monoBehaviour) != null)
                    continue;

                TryResolveFromScope(monoBehaviour, field, scope);
            }
        }

        private static void InjectFieldsFromHierarchy(MonoBehaviour monoBehaviour, GameObject root)
        {
            Type mbType = monoBehaviour.GetType();
            FieldInfo[] injectableFields = GetCachedInjectableFields(mbType);

            for (int i = 0; i < injectableFields.Length; i++)
            {
                FieldInfo field = injectableFields[i];
                if (field.GetValue(monoBehaviour) != null)
                    continue;

                TryResolveFromHierarchy(monoBehaviour, field, root);
            }
        }

        private static void TryResolveFromScope(MonoBehaviour monoBehaviour, FieldInfo field, InstantiationScope scope)
        {
            Type fieldType = field.FieldType;
            MethodInfo tryResolveMethod = GetCachedResolveMethod(fieldType);

            if (tryResolveMethod == null)
                return;

            object[] parameters = new object[] { null };
            try
            {
                bool resolved = (bool)tryResolveMethod.Invoke(scope, parameters);
                if (resolved && parameters[0] != null)
                    field.SetValue(monoBehaviour, parameters[0]);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error resolving {fieldType.Name} for {monoBehaviour.GetType().Name}: {ex.Message}");
            }
        }

        private static void TryResolveFromHierarchy(MonoBehaviour monoBehaviour, FieldInfo field, GameObject root)
        {
            Type fieldType = field.FieldType;
            MonoBehaviour resolvedComponent = root.GetComponentInChildren(fieldType, includeInactive: true) as MonoBehaviour;

            if (resolvedComponent != null)
                field.SetValue(monoBehaviour, resolvedComponent);
        }

        private static FieldInfo[] GetCachedInjectableFields(Type type)
        {
            if (fieldCache.TryGetValue(type, out FieldInfo[] cachedFields))
                return cachedFields;

            FieldInfo[] allFields = type.GetFields(FLAGS);
            List<FieldInfo> injectableFields = new(allFields.Length);

            for (int i = 0; i < allFields.Length; i++)
            {
                FieldInfo field = allFields[i];
                if (typeof(MonoBehaviour).IsAssignableFrom(field.FieldType))
                    injectableFields.Add(field);
            }

            FieldInfo[] result = injectableFields.ToArray();
            fieldCache[type] = result;
            return result;
        }

        private static MethodInfo GetCachedResolveMethod(Type fieldType)
        {
            if (resolveMethodCache.TryGetValue(fieldType, out MethodInfo cachedMethod))
                return cachedMethod;

            MethodInfo method = typeof(InstantiationScope)
                .GetMethod("TryResolveLocal", BindingFlags.Public | BindingFlags.Instance)
                ?.MakeGenericMethod(fieldType);

            resolveMethodCache[fieldType] = method;
            return method;
        }
    }
}

