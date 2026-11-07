#if UNITY_EDITOR
using System;

namespace FastIoC.Editor.Generation
{
    /// <summary>
    /// <c>Lazy&lt;T&gt;</c> and <c>Func&lt;T&gt;</c> dependencies. The generated code passes a wrapper that resolves
    /// <c>T</c> when it's used, so the dependency isn't created with the class that asks for it.
    /// </summary>
    internal static class DeferredDependency
    {
        public enum Kind
        {
            None,
            Lazy,
            Func
        }

        /// <summary>Returns the wrapper kind and the wrapped type, or <see cref="Kind.None"/> and the type itself.</summary>
        public static Kind Unwrap(Type type, out Type inner)
        {
            inner = type;
            if (type == null || !type.IsGenericType || type.IsGenericTypeDefinition)
                return Kind.None;

            Type definition = type.GetGenericTypeDefinition();
            Kind kind = definition == typeof(Lazy<>) ? Kind.Lazy : definition == typeof(Func<>) ? Kind.Func : Kind.None;
            if (kind != Kind.None)
                inner = type.GetGenericArguments()[0];

            return kind;
        }

        /// <summary>
        /// C# expression that creates the wrapper. <paramref name="context"/> is the ContextRuntime variable
        /// and <paramref name="typeIdMember"/> the GeneratedTypeIds member to resolve.
        /// </summary>
        public static string Expression(Kind kind, Type inner, string context, string typeIdMember)
        {
            string innerName = CodeGenTypeUtility.GetCSharpName(inner);
            string factory = $"() => ({innerName}){context}.Resolve(GeneratedTypeIds.{typeIdMember})";
            return kind == Kind.Lazy
                ? $"new global::System.Lazy<{innerName}>({factory})"
                : $"new global::System.Func<{innerName}>({factory})";
        }
    }
}
#endif
