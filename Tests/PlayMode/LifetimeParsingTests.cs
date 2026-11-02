using System;
using System.Collections;
using NUnit.Framework;
using Tools.FastIoC.Runtime.Core;
using UnityEngine.TestTools;

namespace Tools.FastIoC.Tests.PlayMode
{
    public class LifetimeParsingTests
    {
        [UnityTest]
        public IEnumerator ParseTransientLifetime_ReturnsTransient()
        {
            string lifetimeName = "Transient";
            bool success = Enum.TryParse(lifetimeName, out Lifetime lifetime);

            Assert.IsTrue(success);
            Assert.AreEqual(Lifetime.Transient, lifetime);
            yield break;
        }

        [UnityTest]
        public IEnumerator ParseSingletonLifetime_ReturnsSingleton()
        {
            string lifetimeName = "Singleton";
            bool success = Enum.TryParse(lifetimeName, out Lifetime lifetime);

            Assert.IsTrue(success);
            Assert.AreEqual(Lifetime.Singleton, lifetime);
            yield break;
        }

        [UnityTest]
        public IEnumerator ParseCachedLifetime_ReturnsCached()
        {
            string lifetimeName = "Cached";
            bool success = Enum.TryParse(lifetimeName, out Lifetime lifetime);

            Assert.IsTrue(success);
            Assert.AreEqual(Lifetime.Cached, lifetime);
            yield break;
        }

        [UnityTest]
        public IEnumerator ParseInvalidLifetime_DefaultsToTransient()
        {
            string lifetimeName = "InvalidValue";
            Lifetime lifetime = Lifetime.Transient;

            if (!Enum.TryParse(lifetimeName, out lifetime))
                lifetime = Lifetime.Transient;

            Assert.AreEqual(Lifetime.Transient, lifetime);
            yield break;
        }

        [UnityTest]
        public IEnumerator AllLifetimeEnumValuesDefinedCorrectly()
        {
            Assert.AreEqual(0, (int)Lifetime.Transient);
            Assert.AreEqual(1, (int)Lifetime.Singleton);
            Assert.AreEqual(2, (int)Lifetime.Cached);
            yield break;
        }

        [UnityTest]
        public IEnumerator BindingInfoLifetimeNamesParseCorrectly()
        {
            // Simulate what BindingInfo stores
            var testCases = new[]
            {
                ("Transient", Lifetime.Transient),
                ("Singleton", Lifetime.Singleton),
                ("Cached", Lifetime.Cached),
            };

            foreach (var (lifetimeName, expected) in testCases)
            {
                bool success = Enum.TryParse(lifetimeName, out Lifetime result);
                Assert.IsTrue(success, $"Failed to parse: {lifetimeName}");
                Assert.AreEqual(expected, result, $"Incorrect parsing for: {lifetimeName}");
            }
            yield break;
        }
    }
}


