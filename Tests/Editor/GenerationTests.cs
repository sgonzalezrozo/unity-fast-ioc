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
        public void SharedConcrete_AllContractsResolveViaSameTypeId()
        {
            // Bind<IFirst>().To<Both>() and Bind<ISecond>().To<Both>() and Bind<Both>() —
            // only Both enters reachableTypes and gets a TypeId.
            // IFirst and ISecond are in interfaceImplementations and resolve through Both's TypeId.
            InstallerGraphCollector.AnalyzeInstallerBindings(new List<Type> { typeof(SharedInstanceInstaller) });

            Dictionary<Type, Type> implMap = InterfaceImplementationMapBuilder.BuildInterfaceImplementationMap(
                CoreFrameworkTypesRegistry.GetCoreFrameworkTypes());

            Assert.IsTrue(implMap.ContainsKey(typeof(IFirst)), "IFirst must be in interfaceImplementations");
            Assert.IsTrue(implMap.ContainsKey(typeof(ISecond)), "ISecond must be in interfaceImplementations");
            Assert.AreEqual(typeof(Both), implMap[typeof(IFirst)]);
            Assert.AreEqual(typeof(Both), implMap[typeof(ISecond)]);

            // Both gets a TypeId; IFirst and ISecond do not (they go through Both's slot).
            HashSet<Type> reachable = new() { typeof(Both) };
            TypeIdEmitter.GenerateTypeIds(reachable, out Dictionary<Type, int> typeIdMap);

            Assert.IsTrue(typeIdMap.ContainsKey(typeof(Both)));
            Assert.IsFalse(typeIdMap.ContainsKey(typeof(IFirst)), "IFirst must not have its own TypeId");
            Assert.IsFalse(typeIdMap.ContainsKey(typeof(ISecond)), "ISecond must not have its own TypeId");

            // All contracts map to the same concrete TypeId.
            int bothId = typeIdMap[typeof(Both)];
            Assert.AreEqual(bothId, typeIdMap[implMap[typeof(IFirst)]]);
            Assert.AreEqual(bothId, typeIdMap[implMap[typeof(ISecond)]]);
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

        // ── NonLazy ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void NonLazy_MarksBindingAsNonLazy()
        {
            NonLazyHighPriorityInstaller installer = new();
            installer.InstallBindings();

            Dictionary<Type, FastIoC.API.BindingInfo> bindings = installer.GetBindingsForGeneration();

            Assert.IsTrue(bindings[typeof(Analytics)].IsNonLazy,
                "Analytics must be NonLazy after .NonLazy() call");
            Assert.IsFalse(bindings[typeof(Repository)].IsNonLazy,
                "Repository must not be NonLazy without .NonLazy()");
        }

        [Test]
        public void NonLazy_DeclarationOrderIsAssigned()
        {
            NonLazyHighPriorityInstaller installer = new();
            installer.InstallBindings();

            Dictionary<Type, FastIoC.API.BindingInfo> bindings = installer.GetBindingsForGeneration();

            Assert.Less(bindings[typeof(Analytics)].DeclarationOrder,
                        bindings[typeof(Repository)].DeclarationOrder,
                        "Analytics declared before Repository so its order must be lower");
        }

        [Test]
        public void GeneratedModule_ContainsNonLazyResolvesInDeclarationOrder()
        {
            // Build typeIdMap for the NonLazy fixture types.
            HashSet<Type> types = new() { typeof(Analytics), typeof(Other) };
            TypeIdEmitter.GenerateTypeIds(types, out Dictionary<Type, int> typeIdMap);

            // Feed NonLazy info by simulating what the coordinator would pass.
            // Analytics = NonLazy, Other = NonLazy, order: Analytics first.
            List<string> nonLazyNames = new()
            {
                CodeGenTypeUtility.GetTypeIdMemberName(typeof(Analytics)),
                CodeGenTypeUtility.GetTypeIdMemberName(typeof(Other)),
            };

            string moduleCode = GeneratedModuleEmitter.GenerateModule(nonLazyNames);

            int analyticsPos = moduleCode.IndexOf("FastIoC_Fixtures_Analytics", StringComparison.Ordinal);
            int otherPos = moduleCode.IndexOf("FastIoC_Fixtures_Other", StringComparison.Ordinal);

            Assert.Greater(analyticsPos, 0, "Analytics must appear in CreateNonLazyInstances");
            Assert.Greater(otherPos, 0, "Other must appear in CreateNonLazyInstances");
            Assert.Less(analyticsPos, otherPos, "Analytics must appear before Other");
        }

        [Test]
        public void GeneratedModule_BindingWithoutNonLazy_DoesNotAppearInCreateNonLazy()
        {
            string moduleCode = GeneratedModuleEmitter.GenerateModule(new List<string> { "FastIoC_Fixtures_Analytics" });

            // Repository is NOT in the list — must not appear in CreateNonLazyInstances.
            Assert.IsFalse(moduleCode.Contains("FastIoC_Fixtures_Repository"),
                "Repository must not appear in CreateNonLazyInstances");
        }

        [Test]
        public void ValidateNonLazy_SceneInstaller_Throws()
        {
            // Simulate: a NonLazy binding owned by a scene installer (not in global scope).
            // We manually set up cachedBindingInfo via AnalyzeInstallerBindings and then call the validator.
            InstallerGraphCollector.AnalyzeInstallerBindings(new List<Type> { typeof(NonLazyHighPriorityInstaller) });

            Dictionary<Type, FastIoC.API.BindingInfo> bindings = InstallerGraphCollector.GetCachedBindingInfo();

            // Build a fake installersByScene where NonLazyHighPriorityInstaller is scene-specific.
            Dictionary<string, List<Type>> sceneOnly = new()
            {
                ["SomeScene"] = new List<Type> { typeof(NonLazyHighPriorityInstaller) },
            };

            Assert.Throws<InvalidOperationException>(() =>
                GenerationCoordinator.ValidateNonLazyBindings(bindings, sceneOnly));
        }
    }
}
