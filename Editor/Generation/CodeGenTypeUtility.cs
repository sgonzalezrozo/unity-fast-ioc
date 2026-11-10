#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace FastIoC.Editor.Generation
{
    /// <summary>
    /// Helpers to emit C# that references user types directly from the generated code (Assembly-CSharp).
    /// </summary>
    internal static class CodeGenTypeUtility
    {
        public const string GENERATED_ASSEMBLY_NAME = "Assembly-CSharp";

        private static readonly Dictionary<Assembly, bool> s_grantsCache = new();

        /// <summary>Fully qualified C# name, e.g. global::Ns.Outer.Inner&lt;global::System.Int32&gt;.</summary>
        public static string GetCSharpName(Type type)
        {
            if (type.IsArray)
                return GetCSharpName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";

            if (type.IsGenericParameter)
                return type.Name;

            List<Type> chain = new();
            for (Type current = type; current != null; current = current.DeclaringType)
                chain.Insert(0, current);

            Type[] genericArguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            int argumentIndex = 0;

            StringBuilder sb = new("global::");
            if (!string.IsNullOrEmpty(chain[0].Namespace))
                sb.Append(chain[0].Namespace).Append('.');

            for (int i = 0; i < chain.Count; i++)
            {
                string name = chain[i].Name;
                int tick = name.IndexOf('`');
                int count = 0;

                if (tick >= 0)
                {
                    count = int.Parse(name.Substring(tick + 1));
                    name = name.Substring(0, tick);
                }

                sb.Append(name);

                if (count > 0)
                {
                    sb.Append('<');
                    for (int a = 0; a < count; a++)
                    {
                        if (a > 0)
                            sb.Append(", ");

                        sb.Append(argumentIndex < genericArguments.Length ? GetCSharpName(genericArguments[argumentIndex]) : "object");
                        argumentIndex++;
                    }
                    sb.Append('>');
                }

                if (i < chain.Count - 1)
                    sb.Append('.');
            }

            return sb.ToString();
        }

        /// <summary>
        /// C# expression for a constructor parameter whose type is not bound in any installer.
        /// Emits the parameter's compile-time default value when it has one, so optional primitives keep
        /// their intended value instead of collapsing to <c>default(T)</c> (e.g. an <c>int limit = 12</c>
        /// stays 12 rather than becoming 0). Falls back to <c>default(T)</c> when there is no default value
        /// or the literal cannot be represented.
        /// </summary>
        public static string FormatUnboundParameter(ParameterInfo parameter)
        {
            string typeName = GetCSharpName(parameter.ParameterType);

            if (!parameter.HasDefaultValue)
                return $"default({typeName})";

            object value = parameter.DefaultValue;
            if (value == null)
                return $"default({typeName})";

            Type underlying = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            if (underlying.IsEnum)
            {
                object numeric = Convert.ChangeType(value, Enum.GetUnderlyingType(underlying), CultureInfo.InvariantCulture);
                return $"({GetCSharpName(underlying)})({Convert.ToString(numeric, CultureInfo.InvariantCulture)})";
            }

            switch (value)
            {
                case bool b: return b ? "true" : "false";
                case char c: return "'" + EscapeChar(c) + "'";
                case string s: return "\"" + EscapeString(s) + "\"";
                case float f: return f.ToString("R", CultureInfo.InvariantCulture) + "f";
                case double d: return d.ToString("R", CultureInfo.InvariantCulture) + "d";
                case decimal m: return m.ToString(CultureInfo.InvariantCulture) + "m";
                case long l: return l.ToString(CultureInfo.InvariantCulture) + "L";
                case ulong ul: return ul.ToString(CultureInfo.InvariantCulture) + "UL";
                case uint ui: return ui.ToString(CultureInfo.InvariantCulture) + "U";
                case byte or sbyte or short or ushort or int:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                default:
                    return $"default({typeName})";
            }
        }

        private static string EscapeChar(char c) => c switch
        {
            '\'' => "\\'",
            '\\' => "\\\\",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\0' => "\\0",
            _ => c.ToString()
        };

        private static string EscapeString(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

        /// <summary>A valid C# identifier built from the type's full name.</summary>
        public static string GetIdentifier(Type type)
        {
            string fullName = type.FullName ?? type.Name;
            StringBuilder sb = new(fullName.Length);
            foreach (char c in fullName)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');

            return sb.ToString();
        }

        private static readonly HashSet<string> s_keywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        /// <summary>Prefixes C# keywords with @ so a member name can be used in code.</summary>
        public static string EscapeIdentifier(string name)
            => s_keywords.Contains(name) ? "@" + name : name;

        /// <summary>Same naming as TypeIdEmitter, so generated code can reference GeneratedTypeIds members.</summary>
        public static string GetTypeIdMemberName(Type type)
            => type.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";

        /// <summary>
        /// True if code in Assembly-CSharp can name <paramref name="type"/>.
        /// <paramref name="grantsInternals"/> says whether an assembly exposes its internals to Assembly-CSharp.
        /// </summary>
        public static bool IsTypeAccessible(Type type, Func<Assembly, bool> grantsInternals)
        {
            if (type == null)
                return false;

            if (type.IsArray || type.IsByRef || type.IsPointer)
                return IsTypeAccessible(type.GetElementType(), grantsInternals);

            if (type.IsGenericParameter)
                return false;

            if (type.Name.IndexOf('<') >= 0)
                return false; // compiler generated

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    if (!IsTypeAccessible(argument, grantsInternals))
                        return false;
                }
            }

            for (Type current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsPublic || current.IsNestedPublic)
                    continue;

                if (current.IsNotPublic || current.IsNestedAssembly || current.IsNestedFamORAssem)
                {
                    if (!grantsInternals(current.Assembly))
                        return false;

                    continue;
                }

                return false;
            }

            return true;
        }

        /// <summary>Assemblies whose internals must be visible for code to name <paramref name="type"/>.</summary>
        public static void CollectInternalAssemblies(Type type, HashSet<Assembly> result)
        {
            if (type == null)
                return;

            if (type.IsArray || type.IsByRef || type.IsPointer)
            {
                CollectInternalAssemblies(type.GetElementType(), result);
                return;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (Type argument in type.GetGenericArguments())
                    CollectInternalAssemblies(argument, result);
            }

            for (Type current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsNotPublic || current.IsNestedAssembly || current.IsNestedFamORAssem)
                    result.Add(current.Assembly);
            }
        }

        /// <summary>The field's declared access (Private, Assembly, FamORAssem, ...).</summary>
        public static FieldAttributes GetAccess(FieldInfo field)
            => field.Attributes & FieldAttributes.FieldAccessMask;

        /// <summary>internal or protected internal.</summary>
        public static bool IsInternal(FieldInfo field)
        {
            FieldAttributes access = GetAccess(field);
            return access == FieldAttributes.Assembly || access == FieldAttributes.FamORAssem;
        }

        /// <summary>True if code in Assembly-CSharp can assign the field directly.</summary>
        public static bool CanAssignDirectly(FieldInfo field, Func<Assembly, bool> grantsInternals)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral)
                return false;

            FieldAttributes access = GetAccess(field);
            if (access == FieldAttributes.Public)
                return true;

            if (access == FieldAttributes.Assembly || access == FieldAttributes.FamORAssem)
                return grantsInternals(field.DeclaringType.Assembly);

            return false;
        }

        /// <summary>True if <paramref name="assembly"/> is Assembly-CSharp or declares InternalsVisibleTo("Assembly-CSharp").</summary>
        public static bool AlreadyGrantsInternals(Assembly assembly)
        {
            if (assembly == null)
                return false;

            if (s_grantsCache.TryGetValue(assembly, out bool cached))
                return cached;

            bool grants = assembly.GetName().Name == GENERATED_ASSEMBLY_NAME;

            if (!grants)
            {
                try
                {
                    foreach (InternalsVisibleToAttribute attribute in assembly.GetCustomAttributes<InternalsVisibleToAttribute>())
                    {
                        string friend = attribute.AssemblyName ?? "";
                        int comma = friend.IndexOf(',');
                        if (comma >= 0)
                            friend = friend.Substring(0, comma);

                        if (friend.Trim() == GENERATED_ASSEMBLY_NAME)
                        {
                            grants = true;
                            break;
                        }
                    }
                }
                catch
                {
                    grants = false;
                }
            }

            s_grantsCache[assembly] = grants;
            return grants;
        }

        public static void ClearCache()
        {
            s_grantsCache.Clear();
        }
    }
}
#endif
