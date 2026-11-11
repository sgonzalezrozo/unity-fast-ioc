using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Scripting;

namespace FastIoC.Tests.Editor
{
    /// <summary>
    /// Player builds strip an assembly that no code references, along with its [RuntimeInitializeOnLoadMethod]
    /// methods. Fast IoC starts from those methods, so every runtime assembly that has one must be always linked.
    /// </summary>
    public class AssemblyLinkingTests
    {
        private const BindingFlags ALL_STATIC = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly string[] RuntimeAssemblies = { "FastIoC", "FastIoC.Core", "FastIoC.Integration" };

        [Test]
        public void RuntimeAssembliesWithStartupMethods_AreAlwaysLinked()
        {
            List<string> missing = new();
            foreach (string name in RuntimeAssemblies)
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
                Assert.IsNotNull(assembly, $"Assembly '{name}' isn't loaded.");

                bool hasStartupMethod = assembly.GetTypes()
                    .SelectMany(t => t.GetMethods(ALL_STATIC))
                    .Any(m => m.IsDefined(typeof(RuntimeInitializeOnLoadMethodAttribute), false));

                if (hasStartupMethod && !assembly.IsDefined(typeof(AlwaysLinkAssemblyAttribute), false))
                    missing.Add(name);
            }

            Assert.IsEmpty(missing, "These assemblies have [RuntimeInitializeOnLoadMethod] methods but no " +
                                    "[assembly: AlwaysLinkAssembly], so a player build can strip them: " + string.Join(", ", missing));
        }
    }
}
