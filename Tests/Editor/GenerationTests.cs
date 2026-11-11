using System;
using System.Collections.Generic;
using FastIoC.Editor.Generation;
using FastIoC.Editor.Graph;
using FastIoC.Editor.Validation;
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
        public void Activator_UsesCompileTimeDefaultsForUnboundOptionalParameters()
        {
            string code = ActivatorEmitter.GenerateActivators(
                new HashSet<Type> { typeof(DefaultPrimitivesConsumer) },
                new Dictionary<Type, int> { [typeof(DefaultPrimitivesConsumer)] = 0 },
                new Dictionary<Type, FastIoC.Core.Lifetime>(), new Dictionary<Type, bool>(), new Dictionary<Type, Type>());

            StringAssert.Contains("global::System.Int32 param0 = 12;", code);
            StringAssert.Contains("global::System.Single param1 = 0.7f;", code);
            StringAssert.Contains("global::System.Boolean param2 = true;", code);
            StringAssert.Contains("global::FastIoC.Fixtures.FixtureMode param3 = (global::FastIoC.Fixtures.FixtureMode)(2);", code);
            StringAssert.Contains("global::System.Nullable<global::System.TimeSpan> param4 = default(global::System.Nullable<global::System.TimeSpan>);", code);
            StringAssert.Contains("return new DefaultPrimitivesConsumer(param0, param1, param2, param3, param4);", code);
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
            // IFirst and ISecond are in cachedBindingInfo mapping to Both, so constructor/field
            // injection resolves them through Both's TypeId slot.
            InstallerGraphCollector.AnalyzeInstallerBindings(new List<Type> { typeof(SharedInstanceInstaller) });

            // BuildInterfaceImplementationMap uses scanner auto-discovery, which requires
            // [MonoInstaller]. Test the invariant directly via the cache instead.
            Dictionary<Type, FastIoC.API.BindingInfo> cache = InstallerGraphCollector.GetCachedBindingInfo();

            Assert.IsTrue(cache.ContainsKey(typeof(IFirst)), "IFirst must be in cachedBindingInfo");
            Assert.IsTrue(cache.ContainsKey(typeof(ISecond)), "ISecond must be in cachedBindingInfo");
            Assert.AreEqual(typeof(Both), cache[typeof(IFirst)].ImplementationType);
            Assert.AreEqual(typeof(Both), cache[typeof(ISecond)].ImplementationType);

            // Both gets a TypeId; IFirst and ISecond do not (they go through Both's slot).
            HashSet<Type> reachable = new() { typeof(Both) };
            TypeIdEmitter.GenerateTypeIds(reachable, out Dictionary<Type, int> typeIdMap);

            Assert.IsTrue(typeIdMap.ContainsKey(typeof(Both)));
            Assert.IsFalse(typeIdMap.ContainsKey(typeof(IFirst)), "IFirst must not have its own TypeId");
            Assert.IsFalse(typeIdMap.ContainsKey(typeof(ISecond)), "ISecond must not have its own TypeId");

            // All contracts map to the same concrete TypeId.
            int bothId = typeIdMap[typeof(Both)];
            Assert.AreEqual(bothId, typeIdMap[cache[typeof(IFirst)].ImplementationType]);
            Assert.AreEqual(bothId, typeIdMap[cache[typeof(ISecond)].ImplementationType]);
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
        public void GeneratedModule_ExportsGlobalContractsToBridgeSink()
        {
            List<(string contract, string typeIdMember)> exports = new()
            {
                ("global::FastIoC.Fixtures.IService", CodeGenTypeUtility.GetTypeIdMemberName(typeof(Service))),
                ("global::FastIoC.Fixtures.Service", CodeGenTypeUtility.GetTypeIdMemberName(typeof(Service))),
            };

            string moduleCode = GeneratedModuleEmitter.GenerateModule(null, exports);

            StringAssert.Contains("public void ExportGlobalBindings(global::FastIoC.Core.ContextRuntime runtime, global::FastIoC.Core.IGlobalBindingSink sink)", moduleCode);
            StringAssert.Contains("sink.Add(typeof(global::FastIoC.Fixtures.IService), new global::FastIoC.Core.ExternalBinding(runtime, GeneratedTypeIds.FastIoC_Fixtures_Service));", moduleCode);
            StringAssert.Contains("sink.Add(typeof(global::FastIoC.Fixtures.Service), new global::FastIoC.Core.ExternalBinding(runtime, GeneratedTypeIds.FastIoC_Fixtures_Service));", moduleCode);
        }

        [Test]
        public void GeneratedModule_WithoutExports_EmitsEmptyExportMethod()
        {
            string moduleCode = GeneratedModuleEmitter.GenerateModule();

            StringAssert.Contains("public void ExportGlobalBindings(global::FastIoC.Core.ContextRuntime runtime, global::FastIoC.Core.IGlobalBindingSink sink)", moduleCode);
            StringAssert.DoesNotContain("sink.Add(", moduleCode);
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

        // ── TryGetResourcesLoadPath ───────────────────────────────────────────────────────────

        [TestCase("Assets/Resources/A.asset",               ExpectedResult = "A")]
        [TestCase("Assets/Resources/A.prefab",              ExpectedResult = "A")]
        [TestCase("Assets/Foo/Resources/Bar/A.asset",       ExpectedResult = "Bar/A")]
        [TestCase("Assets/MyGame/Resources/Config/X.asset", ExpectedResult = "Config/X")]
        public string TryGetResourcesLoadPath_ExtractsCorrectPath(string assetPath)
        {
            bool ok = AssetPathValidator.TryGetResourcesLoadPath(assetPath, out string loadPath);
            Assert.IsTrue(ok, $"Expected TryGetResourcesLoadPath to return true for '{assetPath}'");
            return loadPath;
        }

        [TestCase("Assets/MyResources/A.asset")]
        [TestCase("Assets/Foo/NotResources/A.asset")]
        [TestCase("Assets/Foo/Bar.prefab")]
        public void TryGetResourcesLoadPath_ReturnsFalseWhenNoResourcesSegment(string assetPath)
        {
            bool ok = AssetPathValidator.TryGetResourcesLoadPath(assetPath, out string loadPath);
            Assert.IsFalse(ok, $"Expected false for '{assetPath}'");
            Assert.IsNull(loadPath);
        }

        [Test]
        public void DetermineLoadingStrategy_GlobalNonResources_ReturnsManifest()
        {
            // An asset outside Resources/ with isGlobal = true should map to Manifest, not throw.
            AssetPathValidator.LoadingStrategy strategy =
                AssetPathValidator.DetermineLoadingStrategy("Assets/SomePath/Foo.asset", isGlobal: true, out _);
            Assert.AreEqual(AssetPathValidator.LoadingStrategy.Manifest, strategy);
        }

        [Test]
        public void DetermineLoadingStrategy_SceneNonResources_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                AssetPathValidator.DetermineLoadingStrategy("Assets/SomePath/Foo.asset", isGlobal: false, out _));
        }

        // ── FromProjectContext ────────────────────────────────────────────────────────────────

        [Test]
        public void FromProjectContext_SetsIsProjectContextBinding()
        {
            HostedInstaller installer = new();
            installer.InstallBindings();

            Dictionary<Type, FastIoC.API.BindingInfo> bindings = installer.GetBindingsForGeneration();

            Assert.IsTrue(bindings[typeof(HostedService)].IsProjectContextBinding,
                "HostedService must have IsProjectContextBinding = true");
            Assert.IsTrue(bindings[typeof(IHostedService)].IsProjectContextBinding,
                "IHostedService must have IsProjectContextBinding = true");
        }

        [Test]
        public void FromProjectContext_IsInstanceAndSingleton()
        {
            HostedInstaller installer = new();
            installer.InstallBindings();

            Dictionary<Type, FastIoC.API.BindingInfo> bindings = installer.GetBindingsForGeneration();
            FastIoC.API.BindingInfo info = bindings[typeof(HostedService)];

            Assert.IsTrue(info.IsInstance);
            Assert.AreEqual("Singleton", info.LifetimeName);
        }

        [Test]
        public void FromProjectContext_WithNonLazy_MarkedNonLazy()
        {
            HostedInstaller installer = new();
            installer.InstallBindings();

            Dictionary<Type, FastIoC.API.BindingInfo> bindings = installer.GetBindingsForGeneration();

            Assert.IsTrue(bindings[typeof(HostedService)].IsNonLazy,
                "HostedService.FromProjectContext().NonLazy() must be NonLazy");
        }

        [Test]
        public void HostedComponentBindings_GeneratedCode_ContainsGetComponentInChildren()
        {
            InstallerGraphCollector.AnalyzeInstallerBindings(new List<Type> { typeof(HostedInstaller) });
            Dictionary<Type, FastIoC.API.BindingInfo> bindings = InstallerGraphCollector.GetCachedBindingInfo();

            HashSet<Type> types = new() { typeof(HostedService), typeof(HostedServiceImpl) };
            TypeIdEmitter.GenerateTypeIds(types, out Dictionary<Type, int> typeIdMap);

            string code = InstanceRegistryPopulator.GenerateInstancePopulationCode(bindings, typeIdMap);

            StringAssert.Contains("GetComponentInChildren", code);
            StringAssert.Contains("FastIoC_Fixtures_HostedService", code);
            StringAssert.Contains("FastIoC_Fixtures_HostedServiceImpl", code);
        }

        [Test]
        public void HostedComponentValidator_SceneInstaller_Throws()
        {
            InstallerGraphCollector.AnalyzeInstallerBindings(new List<Type> { typeof(HostedInstaller) });
            Dictionary<Type, FastIoC.API.BindingInfo> bindings = InstallerGraphCollector.GetCachedBindingInfo();

            // Simulate scene-only installer scope.
            Dictionary<string, List<Type>> sceneOnly = new()
            {
                ["SomeScene"] = new List<Type> { typeof(HostedInstaller) },
            };

            Assert.Throws<InvalidOperationException>(() =>
                FastIoC.Editor.Validation.HostedComponentValidator.Validate(bindings, sceneOnly));
        }

        [Test]
        public void ConstructorCheck_ReportsOnlyClassesGeneratedCodeMustCreate()
        {
            List<string> errors = DependencyDagValidator.FindTypesWithoutPublicConstructor(
                new[] { typeof(PrivateConstructorService), typeof(Service), typeof(IService), typeof(AbstractService) },
                new Dictionary<Type, bool>());

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("FastIoC.Fixtures.PrivateConstructorService", errors[0]);
            StringAssert.Contains("no public constructor", errors[0]);
        }

        [Test]
        public void ConstructorCheck_SkipsInstanceBindings()
        {
            List<string> errors = DependencyDagValidator.FindTypesWithoutPublicConstructor(
                new[] { typeof(PrivateConstructorService) },
                new Dictionary<Type, bool> { [typeof(PrivateConstructorService)] = true });

            Assert.IsEmpty(errors);
        }

        [Test]
        public void Activator_TypeWithoutPublicConstructor_Throws()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ActivatorEmitter.GenerateActivators(
                new HashSet<Type> { typeof(PrivateConstructorService) },
                new Dictionary<Type, int> { [typeof(PrivateConstructorService)] = 0 },
                new Dictionary<Type, FastIoC.Core.Lifetime>(), new Dictionary<Type, bool>(), new Dictionary<Type, Type>()));

            StringAssert.Contains("PrivateConstructorService", ex.Message);
        }
    }
}
