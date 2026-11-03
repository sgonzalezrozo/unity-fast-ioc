#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FastIoC.Core;
using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor.Generation
{
    /// <summary>
    /// Emits GeneratedMonoInjectors: direct field assignments for MonoBehaviour injection,
    /// an injector index per type (used by the baked scene manifests), IInjectableForChildren&lt;T&gt;
    /// data injection and local (same hierarchy) references. The generated code uses no reflection,
    /// except for fields generated code can't reach (private fields, or internal fields of a package
    /// that doesn't expose its internals). Those are listed in the generation log.
    /// </summary>
    public static class MonoInjectorEmitter
    {
        private const string MONO_BEHAVIOUR = "global::UnityEngine.MonoBehaviour";
        private const string CONTEXT_RUNTIME = "global::FastIoC.Core.ContextRuntime";
        private const string LOCAL_SOURCE = "global::FastIoC.Core.ILocalComponentSource";
        private const string INJECTABLE_FOR_CHILDREN = "global::FastIoC.Core.IInjectableForChildren";

        internal sealed class Result
        {
            public string Code;
            public int InjectorCount;
            public int LocalInjectorCount;
            public int DataInjectorCount;
            public readonly List<string> ReflectionFallbackFields = new();
            public readonly List<string> SkippedTypes = new();
            public readonly List<string> IgnoredPrivateFields = new();
        }

        private sealed class FieldInjection
        {
            public FieldInfo Field;
            public string TypeIdMember;
            public bool Direct;
            public int FallbackIndex = -1;
        }

        private sealed class TypeInjector
        {
            public Type Type;
            public int Id;
            public string MethodName;
            public readonly List<FieldInjection> Fields = new();
        }

        private sealed class LocalInjector
        {
            public Type Type;
            public string MethodName;
            public readonly List<FieldInfo> Fields = new();
        }

        private sealed class DataInjector
        {
            public Type Type;
            public string MethodName;
            public readonly List<Type> DataTypes = new();
        }

        private static bool IsTestType(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name;
            return assemblyName.Contains("Test") || assemblyName.Contains("test");
        }

        private static Type FindTypeByFullName<TValue>(Type searchType, Dictionary<Type, TValue> map)
        {
            if (searchType == null)
                return null;

            string fullName = searchType.FullName;
            foreach (Type key in map.Keys)
            {
                if (key != null && key.FullName == fullName)
                    return key;
            }

            return null;
        }

        private static Type FindKey<TValue>(Type type, Dictionary<Type, TValue> map)
            => type != null && map.ContainsKey(type) ? type : FindTypeByFullName(type, map);

        /// <summary>
        /// Only `internal` and `protected internal` fields are injected. Earlier versions also matched
        /// `private` fields by mistake (a bit mask test), which the documentation never allowed.
        /// </summary>
        private static bool IsInjectionCandidateAccess(FieldInfo field)
            => CodeGenTypeUtility.IsInternal(field);

        private static bool IsPrivateAccess(FieldInfo field)
        {
            FieldAttributes access = CodeGenTypeUtility.GetAccess(field);
            return access == FieldAttributes.Private || access == FieldAttributes.FamANDAssem;
        }

        internal static Result GenerateMonoInjectors(
            HashSet<Type> monoBehaviourTypes,
            Dictionary<Type, int> typeIdMap,
            Dictionary<Type, Type> interfaceImplementations,
            Dictionary<Type, bool> instanceBindings,
            InternalsAccessPlan access)
        {
            instanceBindings ??= new();
            Result result = new();

            List<TypeInjector> injectors = BuildContainerInjectors(monoBehaviourTypes, typeIdMap, interfaceImplementations, instanceBindings, access, result);
            List<LocalInjector> localInjectors = new();
            List<DataInjector> dataInjectors = new();
            BuildHierarchyInjectors(access, localInjectors, dataInjectors);

            result.InjectorCount = injectors.Count;
            result.LocalInjectorCount = localInjectors.Count;
            result.DataInjectorCount = dataInjectors.Count;
            result.Code = Emit(injectors, localInjectors, dataInjectors, result.ReflectionFallbackFields.Count);

            return result;
        }

        private static List<TypeInjector> BuildContainerInjectors(
            HashSet<Type> monoBehaviourTypes,
            Dictionary<Type, int> typeIdMap,
            Dictionary<Type, Type> interfaceImplementations,
            Dictionary<Type, bool> instanceBindings,
            InternalsAccessPlan access,
            Result result)
        {
            List<Type> sortedTypes = new();
            foreach (Type type in monoBehaviourTypes)
            {
                if (IsTypeValid(type) && typeof(MonoBehaviour).IsAssignableFrom(type) && !type.IsAbstract)
                    sortedTypes.Add(type);
            }
            sortedTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            List<TypeInjector> injectors = new();
            int fallbackCount = 0;

            foreach (Type type in sortedTypes)
            {
                TypeInjector injector = new() { Type = type };
                HashSet<string> seenNames = new();

                foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (!seenNames.Add(field.Name))
                        continue;

                    bool isPrivate = IsPrivateAccess(field);
                    if (!isPrivate && !IsInjectionCandidateAccess(field))
                        continue;

                    Type fieldType = field.FieldType;
                    Type resolveType = fieldType;

                    Type interfaceKey = FindKey(fieldType, interfaceImplementations);
                    if (interfaceKey != null)
                        resolveType = interfaceImplementations[interfaceKey];

                    Type typeIdKey = FindKey(resolveType, typeIdMap);
                    Type instanceKey = FindKey(fieldType, instanceBindings);

                    if (typeIdKey == null && instanceKey == null)
                        continue;

                    if (isPrivate)
                    {
                        // Not injected. Reported so a field that used to be injected by mistake doesn't go null silently.
                        if (field.Name.IndexOf('<') < 0)
                            result.IgnoredPrivateFields.Add($"{type.FullName}.{field.Name}");
                        continue;
                    }

                    injector.Fields.Add(new FieldInjection
                    {
                        Field = field,
                        TypeIdMember = CodeGenTypeUtility.GetTypeIdMemberName(typeIdKey != null ? resolveType : fieldType),
                        Direct = CodeGenTypeUtility.CanAssignDirectly(field, access.Grants) && CodeGenTypeUtility.IsTypeAccessible(fieldType, access.Grants)
                    });
                }

                if (injector.Fields.Count == 0)
                    continue;

                if (!CodeGenTypeUtility.IsTypeAccessible(type, access.Grants))
                {
                    result.SkippedTypes.Add(type.FullName);
                    continue;
                }

                access.RequireForType(type);

                foreach (FieldInjection injection in injector.Fields)
                {
                    if (injection.Direct)
                    {
                        if (CodeGenTypeUtility.IsInternal(injection.Field))
                            access.Require(injection.Field.DeclaringType.Assembly);

                        access.RequireForType(injection.Field.FieldType);
                        continue;
                    }

                    injection.FallbackIndex = fallbackCount++;
                    result.ReflectionFallbackFields.Add($"{type.FullName}.{injection.Field.Name} ({DescribeAccess(injection.Field)})");
                }

                injector.Id = injectors.Count;
                injector.MethodName = "Inject_" + CodeGenTypeUtility.GetIdentifier(type);
                injectors.Add(injector);
            }

            return injectors;
        }

        private static void BuildHierarchyInjectors(InternalsAccessPlan access, List<LocalInjector> localInjectors, List<DataInjector> dataInjectors)
        {
            List<Type> candidates = new();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (type.IsAbstract || type.ContainsGenericParameters || type == typeof(InstantiationScope))
                    continue;

                if (!IsTypeValid(type))
                    continue;

                string assemblyName = type.Assembly.GetName().Name;
                bool isPlayerAssembly = false;
                foreach (string name in access.PlayerAssemblyNames)
                {
                    if (name == assemblyName)
                    {
                        isPlayerAssembly = true;
                        break;
                    }
                }

                if (!isPlayerAssembly || !CodeGenTypeUtility.IsTypeAccessible(type, access.Grants))
                    continue;

                candidates.Add(type);
            }
            candidates.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            foreach (Type type in candidates)
            {
                string identifier = CodeGenTypeUtility.GetIdentifier(type);

                LocalInjector local = new() { Type = type, MethodName = "InjectLocal_" + identifier };
                HashSet<string> seenNames = new();
                foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (!seenNames.Add(field.Name))
                        continue;

                    if (!typeof(MonoBehaviour).IsAssignableFrom(field.FieldType) || !CodeGenTypeUtility.IsInternal(field))
                        continue;

                    if (!CodeGenTypeUtility.CanAssignDirectly(field, access.Grants) || !CodeGenTypeUtility.IsTypeAccessible(field.FieldType, access.Grants))
                        continue;

                    local.Fields.Add(field);
                }

                if (local.Fields.Count > 0)
                {
                    access.RequireForType(type);
                    foreach (FieldInfo field in local.Fields)
                    {
                        access.Require(field.DeclaringType.Assembly);
                        access.RequireForType(field.FieldType);
                    }
                    localInjectors.Add(local);
                }

                DataInjector data = new() { Type = type, MethodName = "InjectData_" + identifier };
                foreach (Type implemented in type.GetInterfaces())
                {
                    if (!implemented.IsGenericType || implemented.GetGenericTypeDefinition() != typeof(IInjectableForChildren<>))
                        continue;

                    Type dataType = implemented.GetGenericArguments()[0];
                    if (CodeGenTypeUtility.IsTypeAccessible(dataType, access.Grants))
                        data.DataTypes.Add(dataType);
                }

                if (data.DataTypes.Count > 0)
                {
                    access.RequireForType(type);
                    foreach (Type dataType in data.DataTypes)
                        access.RequireForType(dataType);
                    dataInjectors.Add(data);
                }
            }
        }

        private static string Emit(List<TypeInjector> injectors, List<LocalInjector> localInjectors, List<DataInjector> dataInjectors, int fallbackCount)
        {
            StringBuilder sb = new();

            sb.AppendLine("// <auto-generated> FastIoC. Do not edit: regenerate with Tools > FastIoC > Regenerate Code. </auto-generated>");
            sb.AppendLine("namespace FastIoCGenerated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Generated MonoBehaviour injectors. Fields are assigned directly (no reflection).");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class GeneratedMonoInjectors");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const int InjectorCount = {injectors.Count};");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Fields that can't be assigned directly and still use reflection. 0 means no reflection at all.</summary>");
            sb.AppendLine($"        public const int ReflectionFallbackFieldCount = {fallbackCount};");
            sb.AppendLine();

            sb.AppendLine($"        private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> injectorIds = new({injectors.Count})");
            sb.AppendLine("        {");
            foreach (TypeInjector injector in injectors)
                sb.AppendLine($"            {{ typeof({CodeGenTypeUtility.GetCSharpName(injector.Type)}), {injector.Id} }},");
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine($"        private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Action<{MONO_BEHAVIOUR}, {LOCAL_SOURCE}>> localInjectors = new({localInjectors.Count})");
            sb.AppendLine("        {");
            foreach (LocalInjector local in localInjectors)
            {
                string typeName = CodeGenTypeUtility.GetCSharpName(local.Type);
                sb.AppendLine($"            {{ typeof({typeName}), (mb, source) => {local.MethodName}(({typeName})mb, source) }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine($"        private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Func<{MONO_BEHAVIOUR}, object, bool>> dataInjectors = new({dataInjectors.Count})");
            sb.AppendLine("        {");
            foreach (DataInjector data in dataInjectors)
            {
                string typeName = CodeGenTypeUtility.GetCSharpName(data.Type);
                sb.AppendLine($"            {{ typeof({typeName}), (mb, data) => {data.MethodName}(({typeName})mb, data) }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            if (fallbackCount > 0)
            {
                sb.AppendLine($"        private static readonly global::System.Reflection.FieldInfo[] fallbackFields = new global::System.Reflection.FieldInfo[{fallbackCount}];");
                sb.AppendLine();
            }

            sb.AppendLine("        /// <summary>Injector index for a MonoBehaviour type, or -1.</summary>");
            sb.AppendLine("        public static int GetInjectorId(global::System.Type type)");
            sb.AppendLine("        {");
            sb.AppendLine("            return type != null && injectorIds.TryGetValue(type, out int id) ? id : -1;");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        public static void InjectAt(int injectorId, {MONO_BEHAVIOUR} target, {CONTEXT_RUNTIME} context)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (injectorId)");
            sb.AppendLine("            {");
            foreach (TypeInjector injector in injectors)
                sb.AppendLine($"                case {injector.Id}: {injector.MethodName}(({CodeGenTypeUtility.GetCSharpName(injector.Type)})target, context); return;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        public static void Inject({MONO_BEHAVIOUR} target, {CONTEXT_RUNTIME} context)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (target == null)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            sb.AppendLine("            int id = GetInjectorId(target.GetType());");
            sb.AppendLine("            if (id >= 0)");
            sb.AppendLine("                InjectAt(id, target, context);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        public static void InjectLocal({MONO_BEHAVIOUR} target, {LOCAL_SOURCE} source)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (target != null && localInjectors.TryGetValue(target.GetType(), out var injector))");
            sb.AppendLine("                injector(target, source);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        public static bool InjectChildrenData({MONO_BEHAVIOUR} target, object data)");
            sb.AppendLine("        {");
            sb.AppendLine("            return target != null && data != null && dataInjectors.TryGetValue(target.GetType(), out var injector) && injector(target, data);");
            sb.AppendLine("        }");

            if (fallbackCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("        // Only for fields generated code can't reach. Make them `internal` to remove this.");
                sb.AppendLine("        private static void SetFieldByReflection(int index, global::System.Type type, string fieldName, object target, object value)");
                sb.AppendLine("        {");
                sb.AppendLine("            global::System.Reflection.FieldInfo field = fallbackFields[index];");
                sb.AppendLine("            if (field == null)");
                sb.AppendLine("            {");
                sb.AppendLine("                field = type.GetField(fieldName, global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Public);");
                sb.AppendLine("                fallbackFields[index] = field;");
                sb.AppendLine("            }");
                sb.AppendLine();
                sb.AppendLine("            field?.SetValue(target, value);");
                sb.AppendLine("        }");
            }

            foreach (TypeInjector injector in injectors)
            {
                string typeName = CodeGenTypeUtility.GetCSharpName(injector.Type);
                sb.AppendLine();
                sb.AppendLine($"        private static void {injector.MethodName}({typeName} target, {CONTEXT_RUNTIME} context)");
                sb.AppendLine("        {");
                foreach (FieldInjection injection in injector.Fields)
                {
                    string resolve = $"context.Resolve(GeneratedTypeIds.{injection.TypeIdMember})";
                    if (injection.Direct)
                    {
                        string fieldTypeName = CodeGenTypeUtility.GetCSharpName(injection.Field.FieldType);
                        sb.AppendLine($"            target.{CodeGenTypeUtility.EscapeIdentifier(injection.Field.Name)} = ({fieldTypeName}){resolve};");
                    }
                    else
                    {
                        sb.AppendLine($"            SetFieldByReflection({injection.FallbackIndex}, typeof({typeName}), \"{injection.Field.Name}\", target, {resolve});");
                    }
                }
                sb.AppendLine("        }");
            }

            foreach (LocalInjector local in localInjectors)
            {
                string typeName = CodeGenTypeUtility.GetCSharpName(local.Type);
                sb.AppendLine();
                sb.AppendLine($"        private static void {local.MethodName}({typeName} target, {LOCAL_SOURCE} source)");
                sb.AppendLine("        {");
                foreach (FieldInfo field in local.Fields)
                {
                    string fieldTypeName = CodeGenTypeUtility.GetCSharpName(field.FieldType);
                    string fieldName = CodeGenTypeUtility.EscapeIdentifier(field.Name);
                    sb.AppendLine($"            if (target.{fieldName} == null)");
                    sb.AppendLine($"                target.{fieldName} = source.ResolveLocal(typeof({fieldTypeName})) as {fieldTypeName};");
                }
                sb.AppendLine("        }");
            }

            foreach (DataInjector data in dataInjectors)
            {
                string typeName = CodeGenTypeUtility.GetCSharpName(data.Type);
                sb.AppendLine();
                sb.AppendLine($"        private static bool {data.MethodName}({typeName} target, object data)");
                sb.AppendLine("        {");
                for (int i = 0; i < data.DataTypes.Count; i++)
                {
                    string dataTypeName = CodeGenTypeUtility.GetCSharpName(data.DataTypes[i]);
                    sb.AppendLine($"            if (data is {dataTypeName} data{i})");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                (({INJECTABLE_FOR_CHILDREN}<{dataTypeName}>)target).Data = data{i};");
                    sb.AppendLine("                return true;");
                    sb.AppendLine("            }");
                    sb.AppendLine();
                }
                sb.AppendLine("            return false;");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string DescribeAccess(FieldInfo field)
        {
            if (field.IsInitOnly)
                return "readonly";

            return CodeGenTypeUtility.GetAccess(field) switch
            {
                FieldAttributes.Assembly => "internal, assembly doesn't expose internals to Assembly-CSharp",
                FieldAttributes.FamORAssem => "protected internal, assembly doesn't expose internals to Assembly-CSharp",
                _ => "not accessible"
            };
        }

        private static bool IsTypeValid(Type type)
        {
            if (type == null)
                return false;

            if (IsTestType(type))
                return false;

            try
            {
                string fullName = type.FullName;
                if (string.IsNullOrEmpty(fullName))
                    return false;

                _ = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
