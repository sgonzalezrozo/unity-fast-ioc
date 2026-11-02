using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FastIoC.Guards
{
    /// <summary>
    /// Runtime detection for Service Locator anti-pattern.
    /// Throws in development builds if ContextRuntime.Resolve() is called from unauthorized code.
    ///
    /// Active only in: DEVELOPMENT_BUILD or UNITY_EDITOR
    /// Production builds: Zero overhead (compiled out)
    ///
    /// This prevents misuse vectors like:
    /// - Direct reflection-based type→TypeId lookup
    /// - Manual Resolve() calls outside FastIoC framework
    /// - Accidental Service Locator wrapper creation
    /// </summary>
    internal static class ServiceLocatorDetection
    {
        /// <summary>
        /// Approved callers that may invoke ContextRuntime.Resolve().
        /// Only FastIoC internal code and generated code are allowed.
        /// </summary>
        private static readonly HashSet<string> AllowedCallers = new()
        {
            // ===== Framework Core =====
            "FastIoC.Core.ContextRuntime",
            "FastIoC.Core.InstantiationFactory",

            // ===== Framework API =====
            "FastIoC.API.ProjectContext",
            "FastIoC.API.SceneContext",
            "FastIoC.API.SceneInjector",

            // ===== Generated Code =====
            "FastIoCGenerated.GeneratedRegistry",
            "FastIoCGenerated.GeneratedMonoInjectors",
            "FastIoCGenerated.GeneratedActivators",
        };

        /// <summary>
        /// Assert that the caller is authorized to invoke ContextRuntime.Resolve().
        /// Throws InvalidOperationException if an unauthorized caller is detected.
        /// Only active in development builds (compiled out in production).
        /// </summary>
        /// <param name="stackTrace">Stack trace to inspect caller. Use new StackTrace(skipFrames)</param>
        /// <param name="skipFrames">Number of frames to skip (usually 2-3)</param>
        internal static void AssertSafeResolution(StackTrace stackTrace, int skipFrames = 2)
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            try
            {
                var frame = stackTrace.GetFrame(skipFrames);
                if (frame == null)
                    return;

                var method = frame.GetMethod();
                if (method == null)
                    return;

                var callerType = method.DeclaringType;
                if (callerType == null)
                    return;

                var callerFullName = callerType.FullName;

                // Check if caller is in approved list
                if (!AllowedCallers.Contains(callerFullName))
                {
                    throw new InvalidOperationException(
                        $"[FastIoC] ❌ SECURITY VIOLATION: Service Locator Anti-Pattern Detected!\n\n" +
                        $"Unauthorized Caller: {callerFullName}.{method.Name}\n\n" +
                        $"You attempted to call ContextRuntime.Resolve() from outside the FastIoC framework.\n" +
                        $"This is the Service Locator anti-pattern, which violates Dependency Injection principles.\n\n" +
                        $"❌ WRONG - Service Locator:\n" +
                        $"   var runtime = ProjectContext.Instance.Runtime;\n" +
                        $"   var service = runtime.Resolve(typeId);\n\n" +
                        $"✅ RIGHT - Dependency Injection:\n" +
                        $"   public MyClass(IService service) {{ /* constructor injection */ }}\n" +
                        $"   var instance = instantiator.Instantiate<T>(prefab);\n\n" +
                        $"Allowed callers (FastIoC framework only):\n" +
                        $"   • FastIoC.Core.ContextRuntime\n" +
                        $"   • FastIoC.Core.InstantiationFactory\n" +
                        $"   • FastIoC.API.ProjectContext\n" +
                        $"   • FastIoC.API.SceneContext\n" +
                        $"   • FastIoC.API.SceneInjector\n" +
                        $"   • FastIoCGenerated.* (generated code)"
                    );
                }
            }
            catch (InvalidOperationException)
            {
                throw;  // Re-throw security violation
            }
            catch (Exception ex)
            {
                // Don't crash on unexpected errors in detection logic
                UnityEngine.Debug.LogWarning($"[FastIoC] Error in ServiceLocatorDetection: {ex.Message}");
            }
            #endif
        }
    }
}
