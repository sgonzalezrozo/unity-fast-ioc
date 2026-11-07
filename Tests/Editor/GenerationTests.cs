using System;
using System.Collections.Generic;
using FastIoC.Editor.Generation;
using FastIoC.Editor.Graph;
using FastIoC.Fixtures;
using NUnit.Framework;

namespace FastIoC.Tests.Editor
{
    /// <summary>Code the generators write for constructors, MonoBehaviour fields and installers.</summary>
    public class GenerationTests
    {
        private const string SERVICE = "global::FastIoC.Fixtures.IService";

        private static readonly Dictionary<Type, int> TypeIds = new()
        {
            [typeof(Consumer)] = 0,
            [typeof(Service)] = 1,
            [typeof(Other)] = 2,
        };

        private static readonly Dictionary<Type, Type> Implementations = new() { [typeof(IService)] = typeof(Service) };

        [TearDown]
        public void TearDown() => InstallerGraphCollector.ClearBindingInfoCache();

        [Test]
        public void Activator_ResolvesPlainLazyAndFuncParameters()
        {
            string code = ActivatorEmitter.GenerateActivators(
                new HashSet<Type> { typeof(Consumer), typeof(Service), typeof(Other) },
                TypeIds, new Dictionary<Type, FastIoC.Core.Lifetime>(), new Dictionary<Type, bool>(), Implementations);

            StringAssert.Contains("var param0 = (IService)context.Resolve(GeneratedTypeIds.FastIoC_Fixtures_Service);", code);
            StringAssert.Contains($"var param1 = new global::System.Lazy<{SERVICE}>(() => ({SERVICE})context.Resolve(GeneratedTypeIds.FastIoC_Fixtures_Service));", code);
            StringAssert.Contains("var param2 = new global::System.Func<global::FastIoC.Fixtures.Other>(() => (global::FastIoC.Fixtures.Other)context.Resolve(GeneratedTypeIds.FastIoC_Fixtures_Other));", code);
            StringAssert.Contains("return new Consumer(param0, param1, param2);", code);
        }

        [Test]
        public void MonoInjector_AssignsInternalFieldsAndSkipsPrivateOnes()
        {
            MonoInjectorEmitter.Result result = MonoInjectorEmitter.GenerateMonoInjectors(
                new HashSet<Type> { typeof(FixtureView) }, TypeIds, Implementations, new Dictionary<Type, bool>(), InternalsAccessPlan.Create());

            StringAssert.Contains("context.TryResolve(GeneratedTypeIds.FastIoC_Fixtures_Service", result.Code);
            StringAssert.Contains($"target.service = ({SERVICE})", result.Code);
            StringAssert.Contains($"target.lazyService = new global::System.Lazy<{SERVICE}>(", result.Code);
            StringAssert.Contains("target.createOther = new global::System.Func<global::FastIoC.Fixtures.Other>(", result.Code);
            CollectionAssert.Contains(result.IgnoredPrivateFields, "FastIoC.Fixtures.FixtureView.hidden");
            CollectionAssert.IsEmpty(result.ReflectionFallbackFields);
        }

        [Test]
        public void InstallerAnalysis_RecordsBindingsAndImplementations()
        {
            Dictionary<Type, List<Type>> bindings = InstallerGraphCollector.AnalyzeInstallerBindings(new List<Type> { typeof(FixtureInstaller) });

            CollectionAssert.AreEquivalent(new[] { typeof(Service), typeof(Other) }, bindings[typeof(FixtureInstaller)]);
            Assert.AreEqual(typeof(Service), InstallerGraphCollector.GetCachedBindingInfo()[typeof(IService)].ImplementationType);
            Assert.AreEqual(typeof(FixtureInstaller), InstallerGraphCollector.GetBindingOwner(typeof(IService)));
        }

        [Test]
        public void TypeIdMemberName_ReplacesDotsAndPluses()
        {
            Assert.AreEqual("FastIoC_Fixtures_Service", CodeGenTypeUtility.GetTypeIdMemberName(typeof(Service)));
        }

        [Test]
        public void CSharpName_IsFullyQualifiedForGenericTypes()
        {
            Assert.AreEqual($"global::System.Lazy<{SERVICE}>", CodeGenTypeUtility.GetCSharpName(typeof(Lazy<IService>)));
        }

        [Test]
        public void EscapeIdentifier_PrefixesKeywordsOnly()
        {
            Assert.AreEqual("@class", CodeGenTypeUtility.EscapeIdentifier("class"));
            Assert.AreEqual("service", CodeGenTypeUtility.EscapeIdentifier("service"));
        }

        // Kind is internal, so the expected kind is passed by name (a public test method can't take an internal type).
        [TestCase(typeof(Lazy<IService>), "Lazy", typeof(IService))]
        [TestCase(typeof(Func<Other>), "Func", typeof(Other))]
        [TestCase(typeof(Func<int, int>), "None", typeof(Func<int, int>))]
        [TestCase(typeof(IService), "None", typeof(IService))]
        public void DeferredDependency_UnwrapsOnlyLazyAndParameterlessFunc(Type type, string expectedKind, Type expectedInner)
        {
            Assert.AreEqual(expectedKind, DeferredDependency.Unwrap(type, out Type inner).ToString());
            Assert.AreEqual(expectedInner, inner);
        }
    }
}
