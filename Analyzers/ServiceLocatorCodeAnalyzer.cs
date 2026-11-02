using System;
using System.Collections.Generic;
using System.Linq;

namespace Tools.FastIoC.Analyzers
{
    internal static class ServiceLocatorCodeAnalyzer
    {
        internal static class ErrorCodes
        {
            public const string DirectResolveCall = "FASTIOC001";
            public const string ServiceLocatorClass = "FASTIOC002";
            public const string ReflectionTypeLookup = "FASTIOC003";
            public const string ManualResolution = "FASTIOC004";
        }

        internal static class DetectionPatterns
        {
            public static readonly string[] DirectResolvePatterns = new[]
            {
                "runtime.Resolve",
                "Runtime.Resolve",
                "ContextRuntime.Resolve",
                ".Resolve(",
            };

            public static readonly string[] ServiceLocatorPatterns = new[]
            {
                "public static class ServiceLocator",
                "public class ServiceLocator",
                "private static IContainer container",
                "public static T Get<T>",
                "public static T Resolve<T>",
            };

            public static readonly string[] ReflectionPatterns = new[]
            {
                "GeneratedTypeIds",
                "GetField",
                "GetProperty",
                "GetValue",
                ".Invoke(null",
                "Type.GetType(\"FastIoC.Generated",
            };
        }

        public static readonly HashSet<string> AllowedResolveCallers = new()
        {
            // Core
            "Tools.FastIoC.Runtime.Core.ContextRuntime.Resolve",
            "Tools.FastIoC.Runtime.Core.InstantiationFactory.",

            // API
            "Tools.FastIoC.Runtime.API.ProjectContext.",
            "Tools.FastIoC.Runtime.API.SceneContext.",
            "Tools.FastIoC.Runtime.API.SceneInjector.",

            // Generated
            "FastIoC.Generated.",
        };

        public static bool IsAllowedResolveCall(string callerFullName)
        {
            return AllowedResolveCallers.Any(allowed =>
                callerFullName.StartsWith(allowed, StringComparison.Ordinal));
        }

        public static bool ContainsServiceLocatorPattern(string code)
        {
            return DetectionPatterns.ServiceLocatorPatterns.Any(pattern =>
                code.Contains(pattern, StringComparison.Ordinal));
        }

        public static bool ContainsReflectionTypeLookup(string code)
        {
            bool hasGeneratedTypeIds = code.Contains("GeneratedTypeIds", StringComparison.Ordinal);
            bool hasReflection = DetectionPatterns.ReflectionPatterns.Any(pattern =>
                code.Contains(pattern, StringComparison.Ordinal));

            return hasGeneratedTypeIds && hasReflection;
        }

        public static bool ContainsDirectResolveCalls(string code)
        {
            return DetectionPatterns.DirectResolvePatterns.Any(pattern =>
                code.Contains(pattern, StringComparison.Ordinal));
        }
    }
}
